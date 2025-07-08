using Phichat.Application.DTOs.Auth;

namespace Phichat.Application.Interfaces;

public interface IUserService
{
    Task<SendMessageRequest> RegisterAsync(RegisterRequest request);
    Task<SendMessageRequest> LoginAsync(LoginRequest request);
}
