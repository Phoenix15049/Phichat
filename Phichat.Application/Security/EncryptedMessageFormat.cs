using System.Text.RegularExpressions;

namespace Phichat.Application.Security;

/// <summary>
/// Wire format of an end-to-end encrypted message body:
/// <c>v2:{senderKeyId}:{recipientKeyId}:{base64(iv || ciphertext || tag)}</c>.
/// The server cannot read the content; it only checks that the body is well formed and was
/// encrypted for the participants' current identity keys.
/// </summary>
public static partial class EncryptedMessageFormat
{
    public const string Version = "v2";

    /// <summary>Key ids are base64url (no padding) of 16 bytes.</summary>
    public const int KeyIdLength = 22;

    // 12-byte IV + 16-byte GCM tag is the smallest possible payload.
    private const int MinPayloadBytes = 28;

    [GeneratedRegex(@"^v2:([A-Za-z0-9_-]{22}):([A-Za-z0-9_-]{22}):([A-Za-z0-9+/]+={0,2})$")]
    private static partial Regex Pattern();

    [GeneratedRegex(@"^[A-Za-z0-9_-]{22}$")]
    private static partial Regex KeyIdPattern();

    public static bool IsValidKeyId(string? keyId) => keyId != null && KeyIdPattern().IsMatch(keyId);

    public static bool TryParse(string? body, out string senderKeyId, out string recipientKeyId)
    {
        senderKeyId = recipientKeyId = "";
        if (string.IsNullOrEmpty(body)) return false;

        var match = Pattern().Match(body);
        if (!match.Success) return false;

        var payload = match.Groups[3].Value;
        if (payload.Length % 4 != 0) return false;

        var padding = payload.EndsWith("==") ? 2 : payload.EndsWith('=') ? 1 : 0;
        if (payload.Length / 4 * 3 - padding < MinPayloadBytes) return false;

        senderKeyId = match.Groups[1].Value;
        recipientKeyId = match.Groups[2].Value;
        return true;
    }
}
