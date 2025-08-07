using Phichat.Application.DTOs.Auth;
using Phichat.Application.DTOs.User;

namespace Phichat.Application.Interfaces;

public interface IUserService
{
    Task<SendMessageRequest> RegisterAsync(RegisterRequest request);
    Task<SendMessageRequest> LoginAsync(LoginRequest request);
    Task<UserDto?> GetUserByIdAsync(Guid userId);
    Task UpdateEncryptedPrivateKeyAsync(Guid userId, string encryptedPrivateKey, string newPassword);


}
