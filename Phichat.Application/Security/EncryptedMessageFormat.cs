using System.Text.RegularExpressions;

namespace Phichat.Application.Security;

/// <summary>
/// Wire formats of end-to-end encrypted message bodies. The server cannot read the content; it only
/// checks that the body is well formed and was encrypted for the participants' current identity keys.
/// <list type="bullet">
/// <item>Private chat: <c>v2:{senderKeyId}:{recipientKeyId}:{base64(iv || ciphertext || tag)}</c>.</item>
/// <item>Group: <c>g1:{senderKeyId}:{keyId}.{wrappedKey},...:{base64(iv || ciphertext || tag)}</c> - the
/// content is encrypted once with a random message key, which is wrapped for every member's key
/// (base64 of iv || encrypted key || tag, 60 bytes).</item>
/// </list>
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

    public const string GroupVersion = "g1";

    /// <summary>base64 (no padding) of a 12-byte IV, a 32-byte key and a 16-byte tag.</summary>
    public const int WrappedKeyLength = 80;

    [GeneratedRegex(@"^[A-Za-z0-9+/]+={0,2}$")]
    private static partial Regex Base64Pattern();

    /// <summary>Parses a group message body; the recipient key ids are distinct.</summary>
    public static bool TryParseGroup(string? body, out string senderKeyId, out List<string> recipientKeyIds)
    {
        senderKeyId = "";
        recipientKeyIds = new List<string>();
        if (string.IsNullOrEmpty(body) || !body.StartsWith(GroupVersion + ":", StringComparison.Ordinal)) return false;

        var parts = body.Split(':');
        if (parts.Length != 4 || !IsValidKeyId(parts[1])) return false;

        foreach (var entry in parts[2].Split(','))
        {
            if (entry.Length != KeyIdLength + 1 + WrappedKeyLength || entry[KeyIdLength] != '.') return false;

            var keyId = entry[..KeyIdLength];
            if (!IsValidKeyId(keyId) || !Base64Pattern().IsMatch(entry[(KeyIdLength + 1)..])) return false;
            recipientKeyIds.Add(keyId);
        }

        if (recipientKeyIds.Distinct(StringComparer.Ordinal).Count() != recipientKeyIds.Count) return false;

        var payload = parts[3];
        if (payload.Length % 4 != 0 || !Base64Pattern().IsMatch(payload)) return false;
        var padding = payload.EndsWith("==") ? 2 : payload.EndsWith('=') ? 1 : 0;
        if (payload.Length / 4 * 3 - padding < MinPayloadBytes) return false;

        senderKeyId = parts[1];
        return true;
    }

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
