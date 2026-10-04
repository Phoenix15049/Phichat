namespace Phichat.Application.Interfaces;

public interface ISmsCodeService
{
    /// <summary>Creates and sends a one-time code, enforcing per-phone cooldown and hourly limits.</summary>
    Task SendCodeAsync(string phoneNumber);

    /// <summary>Checks and consumes a code. Throws when the code is wrong, expired or out of attempts.</summary>
    Task VerifyCodeAsync(string phoneNumber, string code);
}
