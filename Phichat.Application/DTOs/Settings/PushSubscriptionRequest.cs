namespace Phichat.Application.DTOs.Settings;

/// <summary>A browser PushSubscription (endpoint and keys) plus notification preferences.</summary>
public class PushSubscriptionRequest
{
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public string Lang { get; set; } = "fa";
    public bool ShowSender { get; set; } = true;
}

public class PushUnsubscribeRequest
{
    public string Endpoint { get; set; } = "";
}
