namespace Phichat.Application.Interfaces;

/// <summary>
/// Page metadata for a link preview. The sender's client puts it inside the encrypted message,
/// so recipients never contact the linked site.
/// </summary>
public class LinkPreviewDto
{
    public string Url { get; set; } = default!;
    public string? SiteName { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    /// <summary>The preview image as base64 (the client shrinks it to a thumbnail).</summary>
    public string? ImageBase64 { get; set; }
    public string? ImageType { get; set; }
}

public interface ILinkPreviewService
{
    /// <summary>Null when the URL is not allowed, unreachable, or has nothing to preview.</summary>
    Task<LinkPreviewDto?> GetAsync(string url, CancellationToken cancellationToken);
}
