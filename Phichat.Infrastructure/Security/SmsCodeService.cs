using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.Interfaces;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;

namespace Phichat.Infrastructure.Security;

public sealed class SmsCodeService : ISmsCodeService
{
    private readonly AppDbContext _db;
    private readonly ISmsSender _smsSender;
    private readonly SmsCodeOptions _options;
    private readonly byte[] _hashKey;

    public SmsCodeService(AppDbContext db, ISmsSender smsSender, IOptions<SmsCodeOptions> options, IOptions<JwtOptions> jwtOptions)
    {
        _db = db;
        _smsSender = smsSender;
        _options = options.Value;

        // Keyed hash, so a leaked database does not let anyone brute-force the 6-digit codes offline.
        _hashKey = SHA256.HashData(Encoding.UTF8.GetBytes("phichat:sms-code:" + jwtOptions.Value.Key));
    }

    public async Task SendCodeAsync(string phoneNumber)
    {
        var now = DateTime.UtcNow;

        var lastSentAt = await _db.PhoneVerifications
            .Where(p => p.PhoneNumber == phoneNumber)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => (DateTime?)p.CreatedAtUtc)
            .FirstOrDefaultAsync();

        var cooldown = TimeSpan.FromSeconds(_options.ResendCooldownSeconds);
        if (lastSentAt.HasValue && now - lastSentAt.Value < cooldown)
        {
            throw new TooManyRequestsException("sms_cooldown",
                "Please wait before requesting another code.",
                cooldown - (now - lastSentAt.Value));
        }

        var sentLastHour = await _db.PhoneVerifications
            .CountAsync(p => p.PhoneNumber == phoneNumber && p.CreatedAtUtc > now.AddHours(-1));

        if (sentLastHour >= _options.MaxCodesPerHour)
        {
            throw new TooManyRequestsException("sms_hourly_limit",
                "Too many codes requested for this phone. Try again later.",
                TimeSpan.FromHours(1));
        }

        // Only the newest code is valid.
        await _db.PhoneVerifications
            .Where(p => p.PhoneNumber == phoneNumber && !p.Consumed)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Consumed, true));

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        _db.PhoneVerifications.Add(new PhoneVerification
        {
            Id = Guid.NewGuid(),
            PhoneNumber = phoneNumber,
            CodeHash = HashCode(phoneNumber, code),
            ExpiresAtUtc = now.AddMinutes(_options.CodeLifetimeMinutes),
            CreatedAtUtc = now
        });
        await _db.SaveChangesAsync();

        await _smsSender.SendAsync(phoneNumber, $"Your PhiChat code: {code}");
    }

    public async Task VerifyCodeAsync(string phoneNumber, string code)
    {
        var now = DateTime.UtcNow;

        var pending = await _db.PhoneVerifications
            .AsNoTracking()
            .Where(p => p.PhoneNumber == phoneNumber && !p.Consumed && p.ExpiresAtUtc > now)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new { p.Id, p.CodeHash })
            .FirstOrDefaultAsync();

        if (pending == null)
            throw InvalidCode();

        // Count the attempt atomically; once the limit is reached the row stops matching.
        var counted = await _db.PhoneVerifications
            .Where(p => p.Id == pending.Id && !p.Consumed && p.Attempts < _options.MaxAttempts)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Attempts, p => p.Attempts + 1));

        if (counted == 0)
        {
            await BurnAsync(pending.Id);
            throw InvalidCode();
        }

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(pending.CodeHash);
        }
        catch (FormatException)
        {
            // Code created by the old hashing scheme; it can no longer be verified.
            await BurnAsync(pending.Id);
            throw InvalidCode();
        }

        var actual = Convert.FromHexString(HashCode(phoneNumber, code));

        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            throw InvalidCode();

        // Consume atomically so the same code cannot be redeemed twice in parallel.
        var consumed = await _db.PhoneVerifications
            .Where(p => p.Id == pending.Id && !p.Consumed)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Consumed, true));

        if (consumed == 0)
            throw InvalidCode();
    }

    private Task BurnAsync(Guid id) =>
        _db.PhoneVerifications
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Consumed, true));

    private string HashCode(string phoneNumber, string code) =>
        Convert.ToHexString(HMACSHA256.HashData(_hashKey, Encoding.UTF8.GetBytes($"{phoneNumber}|{code}")));

    private static BadRequestException InvalidCode() =>
        new("sms_code_invalid", "The code is invalid or has expired.");
}
