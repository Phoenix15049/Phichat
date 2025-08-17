using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.IdentityModel.Tokens;
using Phichat.Application.DTOs.Auth;
using Phichat.Application.DTOs.User;
using Phichat.Application.Interfaces;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Phichat.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly ISmsSender _smsSender;
    public UserService(AppDbContext context, IConfiguration config, ISmsSender smsSender)
    {
        _context = context;
        _config = config;
        _smsSender = smsSender;
    }   

    public async Task<SendMessageRequest> RegisterAsync(RegisterRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            throw new Exception("Username already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            PasswordHash = HashPassword(request.Password)
        };


        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return GenerateToken(user);
    }

    public async Task<SendMessageRequest> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username || u.PhoneNumber == request.Username);

        // First check null
        if (user == null)
            throw new Exception("Invalid credentials.");

        // Then compare hashes
        if (user.PasswordHash != HashPassword(request.Password))
            throw new Exception("Invalid credentials.");

        return GenerateToken(user);
    }


    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        return Convert.ToBase64String(sha256.ComputeHash(bytes));
    }

    private SendMessageRequest GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new SendMessageRequest
        {
            Username = user.Username,
            Token = new JwtSecurityTokenHandler().WriteToken(token)
        };
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return null;

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username
        };

    }

    public async Task UpdateEncryptedPrivateKeyAsync(Guid userId, string encryptedPrivateKey, string newPassword)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user is null)
            throw new Exception("User not found");

        user.PasswordHash = HashPassword(newPassword); // ← اضافه شده

        await _context.SaveChangesAsync();
    }


    public async Task UpdateLastSeenAsync(Guid userId, DateTime utcNow)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return;
        user.LastSeenUtc = utcNow;
        await _context.SaveChangesAsync();
    }






    public async Task<SendMessageRequest> RegisterWithPhoneAsync(RegisterWithPhoneRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            throw new Exception("Username already exists.");

        if (await _context.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber))
            throw new Exception("Phone already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            PasswordHash = HashPassword(request.Password),
            PhoneNumber = request.PhoneNumber,
            PhoneVerified = false
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return GenerateToken(user);
    }

    public async Task RequestSmsCodeAsync(string phoneNumber)
    {
        // create 6-digit code
        var code = CreateNumericCode(6);
        var hash = HashString($"{phoneNumber}|{code}");

        // expire old active codes (optional)
        var active = _context.PhoneVerifications
            .Where(p => p.PhoneNumber == phoneNumber && !p.Consumed && p.ExpiresAtUtc > DateTime.UtcNow);
        foreach (var a in active) a.Consumed = true;

        var pv = new PhoneVerification
        {
            Id = Guid.NewGuid(),
            PhoneNumber = phoneNumber,
            CodeHash = hash,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.PhoneVerifications.Add(pv);
        await _context.SaveChangesAsync();

        await _smsSender.SendAsync(phoneNumber, $"Your PhiChat code: {code}");
    }

    public async Task<SendMessageRequest> LoginWithSmsAsync(LoginWithSmsRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
        if (user == null)
            throw new Exception("No account for this phone. Please register.");

        var now = DateTime.UtcNow;
        var pv = await _context.PhoneVerifications
            .Where(p => p.PhoneNumber == request.PhoneNumber && !p.Consumed && p.ExpiresAtUtc > now)
            .OrderByDescending(p => p.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (pv == null) throw new Exception("No valid code. Request a new one.");

        var expected = pv.CodeHash;
        var actual = HashString($"{request.PhoneNumber}|{request.Code}");

        pv.Attempts++;
        if (expected != actual)
        {
            await _context.SaveChangesAsync();
            throw new Exception("Invalid code.");
        }

        pv.Consumed = true;
        user.PhoneVerified = true;
        await _context.SaveChangesAsync();

        return GenerateToken(user);
    }

    // --- helpers ---
    private string CreateNumericCode(int length)
    {
        var sb = new StringBuilder(length);
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        for (int i = 0; i < length; i++)
        {
            rng.GetBytes(bytes);
            var val = BitConverter.ToUInt32(bytes, 0) % 10;
            sb.Append(val);
        }
        return sb.ToString();
    }

    private string HashString(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(sha256.ComputeHash(bytes));
    }


}
