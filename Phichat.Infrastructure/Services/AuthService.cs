using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Auth;
using Phichat.Application.Interfaces;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;

namespace Phichat.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    /// <summary>
    /// A rotated refresh token is still accepted for this long, so two tabs refreshing at the same
    /// moment do not trip reuse detection and sign the user out.
    /// </summary>
    private static readonly TimeSpan RotationGracePeriod = TimeSpan.FromSeconds(30);

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokens;
    private readonly ISmsCodeService _smsCodes;
    private readonly ISessionService _sessions;
    private readonly IClientContext _client;
    private readonly ILogger<AuthService> _logger;

    // Verified against when the user does not exist, so response time does not reveal valid usernames.
    // Static: computed once per process, otherwise the first (hash + verify) cost would itself leak timing.
    private static string? s_dummyHash;

    public AuthService(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokens,
        ISmsCodeService smsCodes,
        ISessionService sessions,
        IClientContext client,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _smsCodes = smsCodes;
        _sessions = sessions;
        _client = client;
        _logger = logger;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        await EnsureUsernameAvailableAsync(request.Username);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            PasswordHash = _passwordHasher.Hash(request.Password)
        };

        await AddUserAsync(user);
        return await IssueTokensAsync(user, familyId: null);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var login = request.Username.Trim();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == login || u.PhoneNumber == login);

        if (user == null)
        {
            s_dummyHash ??= _passwordHasher.Hash(Guid.NewGuid().ToString());
            _passwordHasher.Verify(s_dummyHash, request.Password);
            throw InvalidCredentials();
        }

        var result = _passwordHasher.Verify(user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            throw InvalidCredentials();

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.Hash(request.Password);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Upgraded password hash for user {UserId}", user.Id);
        }

        return await IssueTokensAsync(user, familyId: null);
    }

    public Task RequestSmsCodeAsync(string phoneNumber) =>
        _smsCodes.SendCodeAsync(phoneNumber);

    public async Task<VerifyPhoneResult> VerifyPhoneAsync(VerifyPhoneRequest request)
    {
        await _smsCodes.VerifyCodeAsync(request.PhoneNumber, request.Code);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
        if (user == null)
        {
            return new VerifyPhoneResult
            {
                IsNewUser = true,
                RegistrationToken = _tokens.CreateRegistrationToken(request.PhoneNumber)
            };
        }

        return new VerifyPhoneResult
        {
            IsNewUser = false,
            Auth = await SignInVerifiedPhoneAsync(user)
        };
    }

    public async Task<AuthResult> LoginWithSmsAsync(LoginWithSmsRequest request)
    {
        // The code is checked first, so only the owner of the phone learns whether it has an account.
        await _smsCodes.VerifyCodeAsync(request.PhoneNumber, request.Code);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber)
            ?? throw new NotFoundException("no_account", "No account uses this phone number. Please register.");

        return await SignInVerifiedPhoneAsync(user);
    }

    public async Task<AuthResult> RegisterWithPhoneAsync(RegisterWithPhoneRequest request)
    {
        var phone = await _tokens.ValidateRegistrationTokenAsync(request.RegistrationToken)
            ?? throw new BadRequestException("phone_verification_invalid",
                "Phone verification has expired. Please verify your phone number again.");

        await EnsureUsernameAvailableAsync(request.Username);

        if (await _db.Users.AnyAsync(u => u.PhoneNumber == phone))
            throw new ConflictException("phone_taken", "This phone number is already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            PasswordHash = _passwordHasher.Hash(request.Password),
            PhoneNumber = phone,
            PhoneVerified = true
        };

        await AddUserAsync(user);
        return await IssueTokensAsync(user, familyId: null);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw InvalidRefreshToken();

        var now = DateTime.UtcNow;
        var hash = _tokens.HashRefreshToken(refreshToken);

        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash)
            ?? throw InvalidRefreshToken();

        if (stored.ExpiresAtUtc <= now)
            throw InvalidRefreshToken();

        if (stored.RevokedAtUtc != null)
        {
            var rotatedRecently = stored.ReplacedByTokenHash != null
                && now - stored.RevokedAtUtc.Value <= RotationGracePeriod;

            if (!rotatedRecently)
            {
                // A rotated (or revoked) token came back: assume it was stolen and end the whole session.
                if (await _sessions.RevokeSessionAsync(stored.FamilyId))
                {
                    _logger.LogWarning("Refresh token reuse detected for user {UserId}; session family {FamilyId} revoked",
                        stored.UserId, stored.FamilyId);
                }
                throw InvalidRefreshToken();
            }

            return await IssueTokensAsync(stored.User, stored.FamilyId, SessionStart(stored));
        }

        var result = await IssueTokensAsync(stored.User, stored.FamilyId, SessionStart(stored), saveChanges: false);
        stored.RevokedAtUtc = now;
        stored.ReplacedByTokenHash = _tokens.HashRefreshToken(result.RefreshToken);
        await _db.SaveChangesAsync();

        return result;
    }

    public async Task LogoutAsync(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var hash = _tokens.HashRefreshToken(refreshToken);
        var familyId = await _db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .FirstOrDefaultAsync();

        if (familyId.HasValue)
            await _sessions.RevokeSessionAsync(familyId.Value);
    }

    /// <summary>Tokens issued before sessions were tracked have no start time; their own creation is the best guess.</summary>
    private static DateTime SessionStart(RefreshToken token) =>
        token.SessionStartedAtUtc == default ? token.CreatedAtUtc : token.SessionStartedAtUtc;

    private async Task<AuthResult> SignInVerifiedPhoneAsync(User user)
    {
        if (!user.PhoneVerified)
        {
            user.PhoneVerified = true;
            await _db.SaveChangesAsync();
        }

        return await IssueTokensAsync(user, familyId: null);
    }

    /// <param name="familyId">The session being continued, or null for a new sign-in.</param>
    private async Task<AuthResult> IssueTokensAsync(User user, Guid? familyId, DateTime? sessionStartedAtUtc = null, bool saveChanges = true)
    {
        var now = DateTime.UtcNow;
        var sessionId = familyId ?? Guid.NewGuid();
        var (accessToken, accessExpires) = _tokens.CreateAccessToken(user, sessionId);

        var refreshToken = _tokens.GenerateRefreshToken();
        var refreshExpires = now.Add(_tokens.RefreshTokenLifetime);

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FamilyId = sessionId,
            TokenHash = _tokens.HashRefreshToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpires,
            SessionStartedAtUtc = sessionStartedAtUtc ?? now,
            DeviceName = _client.DeviceName,
            IpAddress = _client.IpAddress
        });

        if (saveChanges)
            await _db.SaveChangesAsync();

        // Housekeeping: drop this user's long-expired tokens.
        await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.ExpiresAtUtc < now.AddDays(-1))
            .ExecuteDeleteAsync();

        return new AuthResult
        {
            Response = new AuthResponse
            {
                Token = accessToken,
                ExpiresAtUtc = accessExpires,
                UserId = user.Id,
                Username = user.Username
            },
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshExpires
        };
    }

    private async Task EnsureUsernameAvailableAsync(string username)
    {
        if (await _db.Users.AnyAsync(u => u.Username == username))
            throw new ConflictException("username_taken", "This username is already taken.");
    }

    private async Task AddUserAsync(User user)
    {
        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Unique index race between the availability check and the insert.
            _db.Entry(user).State = EntityState.Detached;
            _logger.LogInformation(ex, "Registration conflict for username {Username}", user.Username);
            throw new ConflictException("account_conflict", "This username or phone number is already registered.");
        }
    }

    private static UnauthorizedException InvalidCredentials() =>
        new("invalid_credentials", "Invalid username or password.");

    private static UnauthorizedException InvalidRefreshToken() =>
        new("invalid_refresh_token", "Your session has expired. Please sign in again.");
}
