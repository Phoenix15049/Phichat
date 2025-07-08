using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Phichat.Application.DTOs.Auth;
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
            PasswordHash = HashPassword(request.Password),
            PublicKey = request.PublicKey
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return GenerateToken(user);
    }

    public async Task<SendMessageRequest> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
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
}
