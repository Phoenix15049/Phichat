using Microsoft.AspNetCore.SignalR;
using Phichat.Application.Common.Exceptions;

namespace Phichat.API.Hubs;

/// <summary>
/// SignalR hides exception messages from clients unless they are <see cref="HubException"/>s.
/// Expected failures are rethrown as HubException ("code: message") so the client can show them.
/// </summary>
public sealed class AppExceptionHubFilter : IHubFilter
{
    private readonly ILogger<AppExceptionHubFilter> _logger;

    public AppExceptionHubFilter(ILogger<AppExceptionHubFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (AppException ex)
        {
            _logger.LogInformation("Hub method {Method} failed with {Code}: {Message}",
                invocationContext.HubMethodName, ex.Code, ex.Message);
            throw new HubException($"{ex.Code}: {ex.Message}");
        }
    }
}
