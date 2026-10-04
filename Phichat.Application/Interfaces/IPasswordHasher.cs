namespace Phichat.Application.Interfaces;

public enum PasswordVerificationResult
{
    Failed,
    Success,

    /// <summary>The password is correct but the stored hash uses an outdated scheme and should be replaced.</summary>
    SuccessRehashNeeded
}

public interface IPasswordHasher
{
    string Hash(string password);
    PasswordVerificationResult Verify(string hashedPassword, string password);
}
