using System.Threading.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Phichat.Application.Common.Exceptions;

namespace Phichat.API.Hubs;

/// <summary>
/// Hub calls do not go through the HTTP rate limiter, so a client could otherwise flood the chat
/// (and the database) over one open connection. Limits are per user, across all their connections.
/// </summary>
public sealed class HubRateLimitFilter : IHubFilter, IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetFixedWindowLimiter(key, name => new FixedWindowRateLimiterOptions
        {
            // A person types and sends far below this; a burst of queued messages after a reconnect still fits.
            PermitLimit = name.StartsWith("send:", StringComparison.Ordinal) ? 40 : 200,
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 0
        }));

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var user = invocationContext.Context.UserIdentifier ?? invocationContext.Context.ConnectionId;
        var kind = invocationContext.HubMethodName == nameof(ChatHub.SendMessage) ? "send:" : "other:";

        using var lease = _limiter.AttemptAcquire(kind + user);
        if (!lease.IsAcquired)
            throw new TooManyRequestsException("rate_limited", "Too many requests. Please wait a moment and try again.");

        return await next(invocationContext);
    }

    public void Dispose() => _limiter.Dispose();
}
