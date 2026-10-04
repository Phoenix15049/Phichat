using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Phichat.Application.Common.Exceptions;

namespace Phichat.API.Middleware;

/// <summary>
/// Converts exceptions into RFC 7807 ProblemDetails. Expected failures (<see cref="AppException"/>)
/// keep their status code and message; anything else becomes a generic 500 without internals.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            _logger.LogInformation("Request failed with {StatusCode} {Code}: {Message}", ex.StatusCode, ex.Code, ex.Message);

            var retryAfter = (ex as TooManyRequestsException)?.RetryAfter;
            await WriteProblemAsync(context, ex.StatusCode, ex.Code, ex.Message, retryAfter);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            _logger.LogError(ex, "Unhandled exception occurred.");

            var detail = _environment.IsDevelopment() ? ex.Message : "An unexpected error occurred.";
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "server_error", detail);
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string code, string detail, TimeSpan? retryAfter = null)
    {
        if (context.Response.HasStarted)
            return Task.CompletedTask;

        context.Response.Clear();
        context.Response.StatusCode = status;

        if (retryAfter.HasValue)
            context.Response.Headers.RetryAfter = Math.Max(1, Math.Ceiling(retryAfter.Value.TotalSeconds)).ToString("0");

        var problem = new ProblemDetails
        {
            Status = status,
            Title = ReasonPhrases.GetReasonPhrase(status),
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        return context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
