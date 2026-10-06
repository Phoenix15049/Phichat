using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Phichat.Infrastructure.Push;

/// <summary>The server's VAPID identity (RFC 8292): an ECDSA P-256 key that signs push requests.</summary>
public sealed class VapidKeys
{
    public byte[] PublicKey { get; }
    private readonly byte[] _privateKey;
    private readonly string _subject;

    public VapidKeys(byte[] publicKey, byte[] privateKey, string subject)
    {
        if (publicKey.Length != 65 || publicKey[0] != 0x04 || privateKey.Length != 32)
            throw new ArgumentException("VAPID keys must be a 65-byte uncompressed P-256 point and a 32-byte scalar.");

        PublicKey = publicKey;
        _privateKey = privateKey;
        _subject = subject;

        // Fails early on a malformed key pair.
        using var check = CreateSigner();
    }

    public string PublicKeyBase64Url => Base64UrlEncoder.Encode(PublicKey);

    /// <summary>The <c>Authorization: vapid t=..., k=...</c> header value for a push endpoint.</summary>
    public string AuthorizationFor(Uri endpoint)
    {
        var header = Base64UrlEncoder.Encode("""{"typ":"JWT","alg":"ES256"}""");
        var claims = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new
        {
            aud = endpoint.GetLeftPart(UriPartial.Authority),
            exp = DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds(),
            sub = _subject
        }));

        var signingInput = header + "." + claims;
        using var signer = CreateSigner();
        // .NET signs in the IEEE P1363 (r || s) format that JWS expects.
        var signature = signer.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256);

        return $"vapid t={signingInput}.{Base64UrlEncoder.Encode(signature)}, k={PublicKeyBase64Url}";
    }

    private ECDsa CreateSigner() => ECDsa.Create(new ECParameters
    {
        Curve = ECCurve.NamedCurves.nistP256,
        D = _privateKey,
        Q = new ECPoint { X = PublicKey[1..33], Y = PublicKey[33..65] }
    });

    /// <summary>Keys from configuration, otherwise from (or newly written to) <paramref name="fallbackFile"/>.</summary>
    public static VapidKeys Load(PushOptions options, string fallbackFile)
    {
        if (!string.IsNullOrWhiteSpace(options.VapidPublicKey) && !string.IsNullOrWhiteSpace(options.VapidPrivateKey))
        {
            return new VapidKeys(
                Base64UrlEncoder.DecodeBytes(options.VapidPublicKey),
                Base64UrlEncoder.DecodeBytes(options.VapidPrivateKey),
                options.Subject);
        }

        if (File.Exists(fallbackFile))
        {
            var stored = JsonSerializer.Deserialize<StoredKeys>(File.ReadAllText(fallbackFile))
                ?? throw new InvalidOperationException($"{fallbackFile} is not a valid VAPID key file.");
            return new VapidKeys(Base64UrlEncoder.DecodeBytes(stored.PublicKey), Base64UrlEncoder.DecodeBytes(stored.PrivateKey), options.Subject);
        }

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ecdsa.ExportParameters(includePrivateParameters: true);
        var publicKey = new byte[65];
        publicKey[0] = 0x04;
        p.Q.X!.CopyTo(publicKey, 1);
        p.Q.Y!.CopyTo(publicKey, 33);

        Directory.CreateDirectory(Path.GetDirectoryName(fallbackFile)!);
        File.WriteAllText(fallbackFile, JsonSerializer.Serialize(new StoredKeys(
            Base64UrlEncoder.Encode(publicKey), Base64UrlEncoder.Encode(p.D!))));

        return new VapidKeys(publicKey, p.D!, options.Subject);
    }

    private sealed record StoredKeys(string PublicKey, string PrivateKey);
}
