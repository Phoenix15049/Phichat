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

    public UserService(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
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
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        Console.WriteLine("Expected: " + user.PasswordHash);
        Console.WriteLine("Actual: " + HashPassword(request.Password));

        if (user == null || user.PasswordHash != HashPassword(request.Password))
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



}
