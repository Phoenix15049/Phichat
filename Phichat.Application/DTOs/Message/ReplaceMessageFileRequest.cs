using Microsoft.AspNetCore.Http;

namespace Phichat.Application.DTOs.Message;

public class ReplaceMessageFileRequest
{
    public string EncryptedText { get; set; } = string.Empty;
    public IFormFile File { get; set; } = default!;
}
