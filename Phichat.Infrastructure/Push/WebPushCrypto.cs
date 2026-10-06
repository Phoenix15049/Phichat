using System.Buffers.Binary;
using System.Security.Cryptography;
using Phichat.Infrastructure.Security;

namespace Phichat.Infrastructure.Push;

/// <summary>
/// Message encryption for Web Push (RFC 8291, the "aes128gcm" content coding of RFC 8188), so the
/// push service relaying a notification cannot read it.
/// </summary>
public static class WebPushCrypto
{
    private const int RecordSize = 4096;

    /// <summary>Encrypts <paramref name="plaintext"/> for a subscription's public key and auth secret.</summary>
    public static byte[] Encrypt(byte[] plaintext, byte[] userAgentPublicKey, byte[] authSecret) =>
        Encrypt(plaintext, userAgentPublicKey, authSecret, ephemeralKey: null, salt: null);

    /// <param name="ephemeralKey">Fixed sender key, for test vectors only.</param>
    /// <param name="salt">Fixed salt, for test vectors only.</param>
    public static byte[] Encrypt(byte[] plaintext, byte[] userAgentPublicKey, byte[] authSecret, ECParameters? ephemeralKey, byte[]? salt)
    {
        if (!IsValidPublicKey(userAgentPublicKey))
            throw new ArgumentException("The subscription key must be an uncompressed P-256 point.", nameof(userAgentPublicKey));
        if (authSecret.Length != 16)
            throw new ArgumentException("The auth secret must be 16 bytes.", nameof(authSecret));
        // A single record: content + delimiter + tag must fit.
        if (plaintext.Length > RecordSize - 17)
            throw new ArgumentException("Push payload is too large.", nameof(plaintext));

        using var sender = ephemeralKey.HasValue
            ? ECDiffieHellman.Create(ephemeralKey.Value)
            : ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var senderPublic = ExportUncompressed(sender);

        using var receiver = ImportPublicKey(userAgentPublicKey);
        var sharedSecret = sender.DeriveRawSecretAgreement(receiver.PublicKey);

        // IKM = HKDF(salt: auth_secret, ikm: ecdh_secret, info: "WebPush: info" 0x00 ua_public as_public, 32)
        var keyInfo = Concat("WebPush: info\0"u8.ToArray(), userAgentPublicKey, senderPublic);
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, authSecret, keyInfo);

        salt ??= RandomNumberGenerator.GetBytes(16);
        var contentKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, "Content-Encoding: aes128gcm\0"u8.ToArray());
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, "Content-Encoding: nonce\0"u8.ToArray());

        // The only (and so last) record: content followed by the 0x02 padding delimiter.
        var padded = new byte[plaintext.Length + 1];
        plaintext.CopyTo(padded, 0);
        padded[^1] = 0x02;

        var ciphertext = new byte[padded.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(contentKey, tag.Length))
            aes.Encrypt(nonce, padded, ciphertext, tag);

        // Header: salt (16) || record size (4, big endian) || key id length (1) || key id (sender public key).
        var header = new byte[16 + 4 + 1 + senderPublic.Length];
        salt.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), RecordSize);
        header[20] = (byte)senderPublic.Length;
        senderPublic.CopyTo(header, 21);

        return Concat(header, ciphertext, tag);
    }

    /// <summary>Whether the bytes are a valid uncompressed P-256 public key.</summary>
    /// <remarks>Checked explicitly: the Windows provider accepts points that are not on the curve.</remarks>
    public static bool IsValidPublicKey(byte[] key) =>
        key.Length == 65 && key[0] == 0x04 && IdentityKeyMath.IsOnP256Curve(key.AsSpan(1));

    private static ECDiffieHellman ImportPublicKey(byte[] key) => ECDiffieHellman.Create(new ECParameters
    {
        Curve = ECCurve.NamedCurves.nistP256,
        Q = new ECPoint { X = key[1..33], Y = key[33..65] }
    });

    private static byte[] ExportUncompressed(ECDiffieHellman key)
    {
        var q = key.ExportParameters(false).Q;
        var result = new byte[65];
        result[0] = 0x04;
        q.X!.CopyTo(result, 1);
        q.Y!.CopyTo(result, 33);
        return result;
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(p => p.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }
        return result;
    }
}
