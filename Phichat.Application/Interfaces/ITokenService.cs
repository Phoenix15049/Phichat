using Phichat.Domain.Entities;

namespace Phichat.Application.Interfaces;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user);

    /// <summary>Short-lived token proving the caller verified <paramref name="phoneNumber"/> by SMS.</summary>
    string CreateRegistrationToken(string phoneNumber);

    /// <summary>Returns the verified phone number, or null when the token is invalid or expired.</summary>
    Task<string?> ValidateRegistrationTokenAsync(string token);

    /// <summary>A new opaque, high-entropy refresh token (only its hash is stored).</summary>
    string GenerateRefreshToken();

    string HashRefreshToken(string refreshToken);

    TimeSpan RefreshTokenLifetime { get; }
}
