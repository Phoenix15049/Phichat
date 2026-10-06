using System.Numerics;
using System.Security.Cryptography;

namespace Phichat.Infrastructure.Security;

/// <summary>Validation and key ids for ECDH P-256 identity public keys (base64 SPKI).</summary>
public static class IdentityKeyMath
{
    // DER header of SubjectPublicKeyInfo { id-ecPublicKey, prime256v1 } followed by an
    // uncompressed point (0x04 || X || Y): 26 + 65 = 91 bytes in total.
    private static readonly byte[] P256SpkiPrefix = Convert.FromHexString(
        "3059301306072A8648CE3D020106082A8648CE3D030107034200" + "04");

    /// <summary>
    /// Returns the SPKI bytes when <paramref name="base64Spki"/> is a valid P-256 public key in
    /// canonical form (named curve, uncompressed point - what WebCrypto exports), otherwise null.
    /// The canonical form matters: key ids are hashes of these exact bytes on both sides.
    /// </summary>
    public static byte[]? ParseP256PublicKey(string? base64Spki)
    {
        if (string.IsNullOrWhiteSpace(base64Spki) || base64Spki.Length > 256) return null;

        byte[] spki;
        try { spki = Convert.FromBase64String(base64Spki); }
        catch (FormatException) { return null; }

        // The fixed header pins the algorithm and curve; the rest must be exactly X || Y.
        if (spki.Length != P256SpkiPrefix.Length + 64 || !spki.AsSpan().StartsWith(P256SpkiPrefix)) return null;

        return IsOnP256Curve(spki.AsSpan(P256SpkiPrefix.Length)) ? spki : null;
    }

    /// <summary>base64url (no padding) of the first 16 bytes of SHA-256(spki). Must match the client.</summary>
    public static string KeyIdOf(byte[] spki)
    {
        var hash = SHA256.HashData(spki);
        return Convert.ToBase64String(hash, 0, 16).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // P-256 (secp256r1) domain parameters: y^2 = x^3 - 3x + b over GF(p).
    private static readonly BigInteger P = BigInteger.Parse(
        "0FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF", System.Globalization.NumberStyles.HexNumber);
    private static readonly BigInteger B = BigInteger.Parse(
        "05AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B", System.Globalization.NumberStyles.HexNumber);

    /// <summary>True when the 64-byte X||Y coordinates are a point on P-256 (and below the field prime).</summary>
    public static bool IsOnP256Curve(ReadOnlySpan<byte> xy)
    {
        if (xy.Length != 64) return false;

        var x = new BigInteger(xy[..32], isUnsigned: true, isBigEndian: true);
        var y = new BigInteger(xy[32..], isUnsigned: true, isBigEndian: true);
        if (x >= P || y >= P) return false;

        var left = BigInteger.ModPow(y, 2, P);
        var right = ((BigInteger.ModPow(x, 3, P) - 3 * x + B) % P + P) % P;
        return left == right;
    }
}
