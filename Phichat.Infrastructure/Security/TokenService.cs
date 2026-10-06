using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Phichat.Application.Interfaces;
using Phichat.Domain.Entities;

namespace Phichat.Infrastructure.Security;

/// <summary>
/// Issues and validates tokens with <see cref="JsonWebTokenHandler"/>, the same handler the
/// JwtBearer middleware uses, so issuing and validation always agree.
/// </summary>
public sealed class TokenService : ITokenService
{
    private const string PhoneClaim = "phone_number";

    private readonly JwtOptions _options;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly ILogger<TokenService> _logger;
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    public TokenService(IOptions<JwtOptions> options, ILogger<TokenService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
    }

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(_options.AccessTokenMinutes);

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user, Guid sessionId)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        // Claim types are written as-is (the client reads the full NameIdentifier URI).
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Sid, sessionId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        return (WriteToken(claims, _options.Audience, expires), expires);
    }

    public string CreateRegistrationToken(string phoneNumber)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.RegistrationTokenMinutes);
        var claims = new[] { new Claim(PhoneClaim, phoneNumber) };
        return WriteToken(claims, _options.RegistrationAudience, expires);
    }

    public async Task<string?> ValidateRegistrationTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.RegistrationAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey
        };

        var result = await _handler.ValidateTokenAsync(token, parameters);
        if (!result.IsValid)
        {
            _logger.LogInformation("Registration token rejected: {Reason}", result.Exception?.Message);
            return null;
        }

        var phone = result.Claims.TryGetValue(PhoneClaim, out var value) ? value as string : null;
        return string.IsNullOrWhiteSpace(phone) ? null : phone;
    }

    public string GenerateRefreshToken() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private string WriteToken(IEnumerable<Claim> claims, string audience, DateTime expiresUtc)
    {
        var now = DateTime.UtcNow;

        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresUtc,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256)
        });
    }
}
