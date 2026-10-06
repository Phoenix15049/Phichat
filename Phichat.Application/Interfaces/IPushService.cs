using Phichat.Application.DTOs.Settings;

namespace Phichat.Application.Interfaces;

public interface IPushSubscriptionService
{
    /// <summary>The VAPID public key (base64url) the browser subscribes with.</summary>
    string PublicKey { get; }

    Task SubscribeAsync(Guid userId, Guid sessionId, PushSubscriptionRequest request);

    Task UnsubscribeAsync(Guid userId, string endpoint);
}

/// <summary>Queues push notifications; they are sent in the background.</summary>
public interface IPushNotifier
{
    /// <summary>
    /// A new message for <paramref name="recipientId"/>. Sent only to the recipient's devices that are not
    /// connected right now, unless the chat is muted.
    /// </summary>
    void NewMessage(Guid recipientId, Guid senderId, Guid chatId);
}

/// <summary>Sessions with a live real-time connection (they get messages without push).</summary>
public interface IConnectedSessions
{
    bool IsConnected(Guid sessionId);
}
