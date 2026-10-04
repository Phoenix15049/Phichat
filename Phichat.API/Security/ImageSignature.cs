namespace Phichat.API.Security;

/// <summary>Detects common raster image formats from their leading bytes.</summary>
public static class ImageSignature
{
    /// <returns>".jpg", ".png", ".gif" or ".webp", or null when the content is not one of them.</returns>
    public static async Task<string?> DetectExtensionAsync(IFormFile file)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        return Detect(header.AsSpan(0, read));
    }

    public static string? Detect(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
            return ".jpg";

        if (h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ".png";

        if (h.Length >= 6 && (h[..6].SequenceEqual("GIF87a"u8) || h[..6].SequenceEqual("GIF89a"u8)))
            return ".gif";

        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8))
            return ".webp";

        return null;
    }
}
