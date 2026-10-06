using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Settings;
using Phichat.Application.Interfaces;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;

namespace Phichat.Infrastructure.Push;

public sealed class PushSubscriptionService : IPushSubscriptionService
{
    /// <summary>Oldest subscriptions beyond this many per user are dropped.</summary>
    private const int MaxSubscriptionsPerUser = 10;

    private readonly AppDbContext _db;
    private readonly VapidKeys _vapid;

    public PushSubscriptionService(AppDbContext db, VapidKeys vapid)
    {
        _db = db;
        _vapid = vapid;
    }

    public string PublicKey => _vapid.PublicKeyBase64Url;

    public async Task SubscribeAsync(Guid userId, Guid sessionId, PushSubscriptionRequest request)
    {
        if (PushEndpoints.Parse(request.Endpoint) == null)
            throw new BadRequestException("invalid_push_endpoint", "This push service is not supported.");

        if (!TryDecode(request.P256dh, out var p256dh) || !WebPushCrypto.IsValidPublicKey(p256dh) ||
            !TryDecode(request.Auth, out var auth) || auth.Length != 16)
            throw new BadRequestException("invalid_push_keys", "The push subscription keys are not valid.");

        var hash = HashEndpoint(request.Endpoint);
        var existing = await _db.PushSubscriptions.FirstOrDefaultAsync(p => p.EndpointHash == hash);
        if (existing == null)
        {
            existing = new PushSubscription
            {
                Id = Guid.NewGuid(),
                Endpoint = request.Endpoint,
                EndpointHash = hash,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.PushSubscriptions.Add(existing);
        }

        // The same browser may switch accounts: the subscription follows the session that registered it last.
        existing.UserId = userId;
        existing.SessionId = sessionId;
        existing.P256dh = request.P256dh;
        existing.Auth = request.Auth;
        existing.Lang = request.Lang;
        existing.ShowSender = request.ShowSender;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The same endpoint registered concurrently; the other request stored it.
            return;
        }

        var stale = await _db.PushSubscriptions
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip(MaxSubscriptionsPerUser)
            .Select(p => p.Id)
            .ToListAsync();
        if (stale.Count > 0)
            await _db.PushSubscriptions.Where(p => stale.Contains(p.Id)).ExecuteDeleteAsync();
    }

    public async Task UnsubscribeAsync(Guid userId, string endpoint)
    {
        var hash = HashEndpoint(endpoint ?? "");
        await _db.PushSubscriptions.Where(p => p.UserId == userId && p.EndpointHash == hash).ExecuteDeleteAsync();
    }

    public static string HashEndpoint(string endpoint) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)));

    private static bool TryDecode(string value, out byte[] bytes)
    {
        try
        {
            bytes = Base64UrlEncoder.DecodeBytes(value.TrimEnd('='));
            return true;
        }
        catch (FormatException)
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }
}

/// <summary>
/// Push endpoints come from the browser, so the server only posts to the known browser push services
/// (and <see cref="LinkPreview.PublicAddress"/> still checks every connection).
/// </summary>
public static class PushEndpoints
{
    private static readonly string[] AllowedHosts =
    {
        "fcm.googleapis.com",            // Chrome, Opera, Samsung Internet
        "android.googleapis.com",
        "push.services.mozilla.com",     // Firefox (updates.push.services.mozilla.com)
        "notify.windows.com",            // Edge (*.notify.windows.com)
        "push.apple.com"                 // Safari (web.push.apple.com)
    };

    public static Uri? Parse(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > 2048) return null;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)) return null;

        var host = uri.IdnHost.ToLowerInvariant();
        return AllowedHosts.Any(allowed => host == allowed || host.EndsWith("." + allowed, StringComparison.Ordinal))
            ? uri
            : null;
    }
}
