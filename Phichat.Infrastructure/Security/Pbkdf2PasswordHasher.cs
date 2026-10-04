using System.Security.Cryptography;
using System.Text;
using Phichat.Application.Interfaces;

namespace Phichat.Infrastructure.Security;

/// <summary>
/// Salted PBKDF2-HMAC-SHA512 password hashing.
/// Stored format: <c>PBKDF2${algorithm}${iterations}${salt-base64}${hash-base64}</c>.
/// Hashes made with a weaker algorithm or fewer iterations still verify and report a rehash.
/// Hashes from the old unsalted SHA-256 scheme still verify, but report
/// <see cref="PasswordVerificationResult.SuccessRehashNeeded"/> so callers upgrade them on login.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Prefix = "PBKDF2";
    private const string Algorithm = "SHA512";

    // OWASP Password Storage Cheat Sheet recommendation for PBKDF2-HMAC-SHA512.
    public const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, HashSize);

        return string.Join('$', Prefix, Algorithm, Iterations,
            Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public PasswordVerificationResult Verify(string hashedPassword, string password)
    {
        if (string.IsNullOrEmpty(hashedPassword) || password is null)
            return PasswordVerificationResult.Failed;

        if (!hashedPassword.StartsWith(Prefix + "$", StringComparison.Ordinal))
            return VerifyLegacy(hashedPassword, password);

        var parts = hashedPassword.Split('$');
        if (parts.Length != 5 || !int.TryParse(parts[2], out var iterations) || iterations <= 0)
            return PasswordVerificationResult.Failed;

        HashAlgorithmName algorithm;
        switch (parts[1])
        {
            case "SHA512": algorithm = HashAlgorithmName.SHA512; break;
            case "SHA256": algorithm = HashAlgorithmName.SHA256; break;
            default: return PasswordVerificationResult.Failed;
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, algorithm, expected.Length);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            return PasswordVerificationResult.Failed;

        return parts[1] != Algorithm || iterations < Iterations
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }

    /// <summary>Old scheme: base64(SHA256(password)) with no salt.</summary>
    private static PasswordVerificationResult VerifyLegacy(string hashedPassword, string password)
    {
        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(hashedPassword);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }

        if (expected.Length != 32)
            return PasswordVerificationResult.Failed;

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return CryptographicOperations.FixedTimeEquals(actual, expected)
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Failed;
    }
}
