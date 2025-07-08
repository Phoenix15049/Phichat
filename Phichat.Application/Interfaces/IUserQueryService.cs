using Phichat.Application.DTOs.User;

public interface IUserQueryService
{
    Task<UserPublicKeyResponse?> GetByUsernameAsync(string username);
    Task<UserPublicKeyResponse?> GetByIdAsync(Guid userId);
}
