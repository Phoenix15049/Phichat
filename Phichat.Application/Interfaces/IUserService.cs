using Phichat.Application.DTOs.Auth;
using Phichat.Application.DTOs.User;

namespace Phichat.Application.Interfaces;

public interface IUserService
{
    Task<SendMessageRequest> RegisterAsync(RegisterRequest request);
    Task<SendMessageRequest> LoginAsync(LoginRequest request);


    Task<SendMessageRequest> RegisterWithPhoneAsync(RegisterWithPhoneRequest request);
    Task RequestSmsCodeAsync(string phoneNumber);
    Task<SendMessageRequest> LoginWithSmsAsync(LoginWithSmsRequest request);



    Task<UserDto?> GetUserByIdAsync(Guid userId);
    Task UpdateEncryptedPrivateKeyAsync(Guid userId, string encryptedPrivateKey, string newPassword);
    Task UpdateLastSeenAsync(Guid userId, DateTime utcNow);

}
