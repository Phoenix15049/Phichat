namespace Phichat.API.Security;

/// <summary>
/// Response headers for user-uploaded files. Images (except SVG), audio and video may render inline;
/// everything else is forced to download, and a sandboxing CSP stops any file from running scripts
/// on the API origin (where the refresh-token cookie lives).
/// </summary>
public static class UploadedFileHeaders
{
    public static void Apply(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self'; media-src 'self'; style-src 'unsafe-inline'; sandbox";
        headers["Cross-Origin-Resource-Policy"] = "cross-origin";

        var contentType = context.Response.ContentType ?? string.Empty;
        if (!IsInlineSafe(contentType))
            headers.ContentDisposition = "attachment";
    }

    private static bool IsInlineSafe(string contentType)
    {
        if (contentType.StartsWith("image/svg", StringComparison.OrdinalIgnoreCase))
            return false;

        return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);
    }
}
