using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Data;
using Phichat.Infrastructure.LinkPreview;

namespace Phichat.Infrastructure.Push;

/// <summary>Accepts push jobs from request handlers without waiting for delivery.</summary>
public sealed class PushQueue : IPushNotifier
{
    internal sealed record Job(Guid RecipientId, Guid SenderId, Guid ChatId);

    // Bounded so a burst cannot grow memory without limit; notifications are best effort.
    private readonly Channel<Job> _channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10_000)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true
    });

    internal ChannelReader<Job> Reader => _channel.Reader;

    public void NewMessage(Guid recipientId, Guid senderId, Guid chatId)
    {
        if (recipientId == senderId) return;
        _channel.Writer.TryWrite(new Job(recipientId, senderId, chatId));
    }
}

/// <summary>
/// Sends queued notifications to the recipient's devices that have no live connection. The payload
/// names the chat only (never message content, which the server cannot read anyway) and is
/// encrypted for the browser (RFC 8291).
/// </summary>
public sealed class PushDispatcher : BackgroundService
{
    private static readonly TimeSpan TimeToLive = TimeSpan.FromDays(1);

    private readonly PushQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly IConnectedSessions _connected;
    private readonly VapidKeys _vapid;
    private readonly ILogger<PushDispatcher> _logger;
    private readonly HttpClient _http;

    public PushDispatcher(PushQueue queue, IServiceScopeFactory scopes, IConnectedSessions connected, VapidKeys vapid, ILogger<PushDispatcher> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _connected = connected;
        _vapid = vapid;
        _logger = logger;

        _http = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            UseCookies = false,
            ConnectCallback = PublicAddress.ConnectAsync
        })
        { Timeout = TimeSpan.FromSeconds(10) };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await DeliverAsync(job, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Push delivery failed for user {UserId}", job.RecipientId);
            }
        }
    }

    public override void Dispose()
    {
        _http.Dispose();
        base.Dispose();
    }

    private async Task DeliverAsync(PushQueue.Job job, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await db.ChatMutes.AnyAsync(m => m.UserId == job.RecipientId && m.ChatId == job.ChatId, ct))
            return;

        var subscriptions = (await db.PushSubscriptions.AsNoTracking()
                .Where(p => p.UserId == job.RecipientId)
                .ToListAsync(ct))
            .Where(p => !_connected.IsConnected(p.SessionId))
            .ToList();
        if (subscriptions.Count == 0) return;

        var sender = await db.Users.AsNoTracking()
            .Where(u => u.Id == job.SenderId)
            .Select(u => new { u.Username, u.DisplayName })
            .FirstOrDefaultAsync(ct);
        if (sender == null) return;
        var senderName = sender.DisplayName ?? "@" + sender.Username;

        // A private chat is identified by the sender; any other chat id is a group.
        string? groupTitle = null;
        if (job.ChatId != job.SenderId)
        {
            groupTitle = await db.Groups.AsNoTracking().Where(g => g.Id == job.ChatId).Select(g => g.Title).FirstOrDefaultAsync(ct);
            if (groupTitle == null) return;
        }

        foreach (var subscription in subscriptions)
        {
            var show = subscription.ShowSender;
            var payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                t = "msg",
                lang = subscription.Lang,
                name = show ? groupTitle ?? senderName : null,
                from = show && groupTitle != null ? senderName : null,
                url = !show ? "/chat" : groupTitle != null ? "/g/" + job.ChatId : "/u/" + sender.Username,
                tag = "chat-" + job.ChatId.ToString("N")
            });

            var gone = await SendAsync(subscription.Endpoint, subscription.P256dh, subscription.Auth, payload, ct);
            if (gone)
                await db.PushSubscriptions.Where(p => p.Id == subscription.Id).ExecuteDeleteAsync(ct);
        }
    }

    /// <returns>True when the push service says the subscription no longer exists.</returns>
    private async Task<bool> SendAsync(string endpoint, string p256dh, string auth, byte[] payload, CancellationToken ct)
    {
        var uri = PushEndpoints.Parse(endpoint);
        if (uri == null) return true;

        var body = WebPushCrypto.Encrypt(payload, Base64UrlEncoder.DecodeBytes(p256dh.TrimEnd('=')), Base64UrlEncoder.DecodeBytes(auth.TrimEnd('=')));

        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentEncoding.Add("aes128gcm");
        request.Headers.TryAddWithoutValidation("TTL", ((int)TimeToLive.TotalSeconds).ToString());
        request.Headers.TryAddWithoutValidation("Urgency", "high");
        request.Headers.TryAddWithoutValidation("Authorization", _vapid.AuthorizationFor(uri));

        using var response = await _http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return false;

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            return true;

        // The endpoint is a capability URL, so only its host is logged.
        _logger.LogWarning("Push service {Host} answered {Status}", uri.Host, (int)response.StatusCode);
        return false;
    }
}
