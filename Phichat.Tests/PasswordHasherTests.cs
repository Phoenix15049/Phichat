using System.Security.Cryptography;
using System.Text;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Security;

namespace Phichat.Tests;

public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_verify_succeeds()
    {
        var hash = _hasher.Hash("correct horse");
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify(hash, "correct horse"));
    }

    [Fact]
    public void Wrong_password_fails()
    {
        var hash = _hasher.Hash("correct horse");
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify(hash, "battery staple"));
    }

    [Fact]
    public void Same_password_gets_different_salts()
    {
        Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));
    }

    [Fact]
    public void Legacy_unsalted_sha256_verifies_and_asks_for_rehash()
    {
        var legacy = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("old-pass")));

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, _hasher.Verify(legacy, "old-pass"));
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify(legacy, "wrong"));
    }

    [Fact]
    public void Older_pbkdf2_parameters_verify_and_ask_for_rehash()
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2("pw", salt, 1000, HashAlgorithmName.SHA256, 32);
        var stored = $"PBKDF2$SHA256$1000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, _hasher.Verify(stored, "pw"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("PBKDF2$MD5$1000$AAAA$AAAA")]
    [InlineData("PBKDF2$SHA512$abc$AAAA$AAAA")]
    public void Malformed_hashes_fail_without_throwing(string stored)
    {
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify(stored, "pw"));
    }
}
