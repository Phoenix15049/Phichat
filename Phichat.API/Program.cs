using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Phichat.API.Middleware;
using Phichat.Application.DTOs.Auth;
using Phichat.Application.Interfaces;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;
using Phichat.Infrastructure.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .MinimumLevel.Information()
    .CreateLogger();


var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();


builder.Services.AddControllers();


builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();


builder.Services.AddEndpointsApiExplorer();


builder.Services.AddSwaggerGen();


builder.Services.AddScoped<IUserQueryService, UserQueryService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IMessageService, MessageService>();


builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});


builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})


.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var rawHeader = context.Request.Headers["Authorization"].FirstOrDefault();

            Console.WriteLine(" Raw Authorization Header: " + rawHeader);

            if (!string.IsNullOrWhiteSpace(rawHeader) && rawHeader.StartsWith("Bearer "))
            {
                var token = rawHeader.Substring("Bearer ".Length).Trim();

                Console.WriteLine(" Extracted Token: " + token);

                if (token.Contains('.'))
                {
                    Console.WriteLine(" Token contains dots.");
                }
                else
                {
                    Console.WriteLine(" Token does NOT contain dots.");
                }

                context.Token = token;
            }

            return Task.CompletedTask;
        },

        OnAuthenticationFailed = context =>
        {
            Console.WriteLine(" JWT Auth Failed:");
            Console.WriteLine(context.Exception.ToString());
            return Task.CompletedTask;
        }
    };



    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});


var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseHttpsRedirection();


app.UseMiddleware<ExceptionHandlingMiddleware>();


app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();


app.Run();