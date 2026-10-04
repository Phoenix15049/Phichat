using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Phichat.API.Hubs;
using Phichat.API.Middleware;
using Phichat.API.Security;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Data;
using Phichat.Infrastructure.Security;
using Phichat.Infrastructure.Services;
using Serilog;
using System.Security.Claims;


Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .MinimumLevel.Information()
    .CreateLogger();


// Upload folders are not tracked in git, so create them on a fresh checkout.
// wwwroot must exist before the builder is created, otherwise static files are not served from it.
var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
Directory.CreateDirectory(uploadsRoot);
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars"));

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();


// ---------- Configuration (fail fast on missing secrets) ----------

// Secrets come from user-secrets (Development) or environment variables (Jwt__Key, ConnectionStrings__DefaultConnection).
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.Key) || Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32)
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 bytes. Set it with " +
        "`dotnet user-secrets set \"Jwt:Key\" \"<random-secret>\" --project Phichat.API` " +
        "or the Jwt__Key environment variable.");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. Set it in appsettings.Development.json, " +
        "user-secrets or the ConnectionStrings__DefaultConnection environment variable.");

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<SmsCodeOptions>(builder.Configuration.GetSection(SmsCodeOptions.SectionName));
builder.Services.Configure<RefreshTokenCookieOptions>(builder.Configuration.GetSection(RefreshTokenCookieOptions.SectionName));


// ---------- MVC, SignalR, validation ----------

builder.Services.AddControllers();

builder.Services.AddSignalR(options =>
{
    // Encrypted text messages are capped at 64 KB of base64; leave headroom for the envelope.
    options.MaximumReceiveMessageSize = 128 * 1024;
    options.AddFilter<AppExceptionHubFilter>();
});

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// ---------- Application services ----------

builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<ISmsSender, ConsoleSmsSender>();
builder.Services.AddScoped<ISmsCodeService, SmsCodeService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IChatKeyService, ChatKeyService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddSingleton<PresenceTracker>();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});


// ---------- Authentication ----------

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Browsers cannot set headers on WebSocket requests, so the hub receives the token in the query string.
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments(ChatHub.Path))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});


// ---------- Rate limiting ----------

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString("0");

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "Too many requests. Please wait a moment and try again."
        };
        problem.Extensions["code"] = "rate_limited";

        await context.HttpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: token);
    };

    static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    static RateLimitPartition<string> PerIp(HttpContext context, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = window,
            QueueLimit = 0
        });

    options.AddPolicy(RateLimitPolicies.Auth, context => PerIp(context, 10, TimeSpan.FromMinutes(1)));
    options.AddPolicy(RateLimitPolicies.Sms, context => PerIp(context, 5, TimeSpan.FromMinutes(15)));
    options.AddPolicy(RateLimitPolicies.Refresh, context => PerIp(context, 30, TimeSpan.FromMinutes(1)));
    options.AddPolicy(RateLimitPolicies.Lookup, context => PerIp(context, 30, TimeSpan.FromMinutes(1)));

    options.AddPolicy(RateLimitPolicies.Upload, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ClientIp(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});


// ---------- CORS ----------

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 } origins
    ? origins
    : new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});



var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx => UploadedFileHeaders.Apply(ctx.Context)
});

// Message attachments. Unknown types are served as downloads; nothing uploaded can run as a page on this origin.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads",
    ContentTypeProvider = new FileExtensionContentTypeProvider(),
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream",
    OnPrepareResponse = ctx => UploadedFileHeaders.Apply(ctx.Context)
});

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.MapHub<ChatHub>(ChatHub.Path, options =>
{
    // Drop connections whose access token expired; the client reconnects with a refreshed token.
    options.CloseOnAuthenticationExpiration = true;
});

app.Run();
