using Microsoft.EntityFrameworkCore;
using Phichat.Application.DTOs.User;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Data;

namespace Phichat.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;

    public UserService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl,
                LastSeenUtc = u.LastSeenUtc
            })
            .FirstOrDefaultAsync();
    }

    public async Task UpdateLastSeenAsync(Guid userId, DateTime utcNow)
    {
        await _context.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastSeenUtc, utcNow));
    }
}
