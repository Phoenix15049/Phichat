using Microsoft.EntityFrameworkCore;
using Phichat.Application.DTOs.User;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Data;

public class UserQueryService : IUserQueryService
{
    private readonly AppDbContext _context;

    public UserQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserPublicKeyResponse?> GetByUsernameAsync(string username)
    {
        var user = await _context.Users
            .Where(u => u.Username == username)
            .Select(u => new UserPublicKeyResponse
            {
                UserId = u.Id,
                Username = u.Username
            }).FirstOrDefaultAsync();

        return user;
    }

    public async Task<UserPublicKeyResponse?> GetByIdAsync(Guid userId)
    {
        var user = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new UserPublicKeyResponse
            {
                UserId = u.Id,
                Username = u.Username
            }).FirstOrDefaultAsync();

        return user;
    }
}
