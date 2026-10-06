using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Phichat.Application.Interfaces;

namespace Phichat.Infrastructure.LinkPreview;

/// <summary>
/// Fetches Open Graph / HTML metadata for a link preview, guarded against SSRF:
/// http(s) on ports 80/443 only, every connection checked against <see cref="PublicAddress"/>
/// after DNS resolution (so DNS rebinding cannot slip through), redirects followed manually and
/// re-checked, small size limits and short timeouts. URLs are never logged.
/// </summary>
public sealed partial class LinkPreviewService : ILinkPreviewService, IDisposable
{
    private const int MaxHtmlBytes = 512 * 1024;
    private const int MaxImageBytes = 1024 * 1024;
    private const int MaxRedirects = 3;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(30);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    static LinkPreviewService()
    {
        // Legacy page encodings such as windows-1256 (common on Persian sites).
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public LinkPreviewService(IMemoryCache cache)
    {
        _cache = cache;

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(4),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            UseCookies = false,
            UseProxy = false,
            ConnectCallback = PublicAddress.ConnectAsync
        };

        _http = new HttpClient(handler) { Timeout = RequestTimeout };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; PhiChatLinkPreview/1.0)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.5");
    }

    public void Dispose() => _http.Dispose();

    /// <summary>Only absolute http(s) URLs on the default ports, without credentials.</summary>
    public static Uri? NormalizeUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 2048) return null;
        if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        if (!string.IsNullOrEmpty(uri.UserInfo)) return null;
        if (!uri.IsDefaultPort) return null;
        if (string.IsNullOrEmpty(uri.Host)) return null;
        return uri;
    }

    public async Task<LinkPreviewDto?> GetAsync(string url, CancellationToken cancellationToken)
    {
        var uri = NormalizeUrl(url);
        if (uri == null) return null;

        var cacheKey = "lp:" + uri.AbsoluteUri;
        if (_cache.TryGetValue(cacheKey, out LinkPreviewDto? cached)) return cached;

        LinkPreviewDto? preview = null;
        try
        {
            preview = await FetchAsync(uri, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException or SocketException or DecoderFallbackException)
        {
            preview = null;
        }

        _cache.Set(cacheKey, preview, CacheFor);
        return preview;
    }

    private async Task<LinkPreviewDto?> FetchAsync(Uri uri, CancellationToken ct)
    {
        var (finalUri, response) = await GetFollowingRedirectsAsync(uri, ct);
        if (response == null) return null;

        using (response)
        {
            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)) return null;

            var bytes = await ReadLimitedAsync(response.Content, MaxHtmlBytes, ct);
            var html = Decode(bytes, response.Content.Headers.ContentType?.CharSet);
            var meta = ParseMeta(html);

            string? Pick(params string[] keys) => keys.Select(k => meta.GetValueOrDefault(k)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            var title = Pick("og:title", "twitter:title");
            if (title == null)
            {
                var titleTag = TitleTag().Match(html);
                if (titleTag.Success) title = Clean(titleTag.Groups[1].Value);
            }
            var description = Pick("og:description", "twitter:description", "description");
            var siteName = Pick("og:site_name") ?? finalUri.Host;
            var imageRef = Pick("og:image", "og:image:url", "twitter:image", "twitter:image:src");

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description)) return null;

            var preview = new LinkPreviewDto
            {
                Url = uri.AbsoluteUri,
                SiteName = Truncate(siteName, 80),
                Title = Truncate(title, 200),
                Description = Truncate(description, 300)
            };

            if (imageRef != null && Uri.TryCreate(finalUri, imageRef, out var imageUri) && NormalizeUrl(imageUri.AbsoluteUri) is { } safeImage)
            {
                (preview.ImageBase64, preview.ImageType) = await TryFetchImageAsync(safeImage, ct);
            }

            return preview;
        }
    }

    private async Task<(string?, string?)> TryFetchImageAsync(Uri uri, CancellationToken ct)
    {
        try
        {
            var (_, response) = await GetFollowingRedirectsAsync(uri, ct);
            if (response == null) return (null, null);
            using (response)
            {
                var type = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
                if (type is not ("image/jpeg" or "image/png" or "image/webp" or "image/gif")) return (null, null);
                if (response.Content.Headers.ContentLength > MaxImageBytes) return (null, null);

                var bytes = await ReadLimitedAsync(response.Content, MaxImageBytes, ct);
                return bytes.Length == 0 ? (null, null) : (Convert.ToBase64String(bytes), type);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException or SocketException)
        {
            return (null, null);
        }
    }

    /// <summary>GET with manual redirects; each hop is re-validated (scheme, port, credentials, address).</summary>
    private async Task<(Uri, HttpResponseMessage?)> GetFollowingRedirectsAsync(Uri uri, CancellationToken ct)
    {
        var current = uri;
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            var response = await _http.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, ct);
            var status = (int)response.StatusCode;

            if (status is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                response.Dispose();
                var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                current = NormalizeUrl(next.AbsoluteUri) ?? throw new HttpRequestException("Redirect target is not allowed.");
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                response.Dispose();
                return (current, null);
            }
            return (current, response);
        }
        return (current, null);
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, int limit, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        var buffer = new byte[limit];
        var total = 0;
        while (total < limit)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, limit - total), ct);
            if (read == 0) break;
            total += read;
        }
        return buffer[..total];
    }

    private static string Decode(byte[] bytes, string? charset)
    {
        Encoding encoding = Encoding.UTF8;
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try { encoding = Encoding.GetEncoding(charset.Trim('"', '\'')); } catch (ArgumentException) { }
        }
        return encoding.GetString(bytes);
    }

    /// <summary>Lower-cased property/name -> content of every &lt;meta&gt; tag (first one wins).</summary>
    public static Dictionary<string, string> ParseMeta(string html)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match tag in MetaTag().Matches(html))
        {
            string? key = null, content = null;
            foreach (Match attr in Attribute().Matches(tag.Value))
            {
                var name = attr.Groups[1].Value.ToLowerInvariant();
                var value = attr.Groups[3].Success ? attr.Groups[3].Value : attr.Groups[4].Value;
                if (name is "property" or "name") key ??= value.Trim().ToLowerInvariant();
                else if (name == "content") content = value;
            }
            if (key != null && content != null && !result.ContainsKey(key)) result[key] = Clean(content);
        }
        return result;
    }

    private static string Clean(string value) => Whitespace().Replace(WebUtility.HtmlDecode(value), " ").Trim();

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..(max - 1)].TrimEnd() + "…";

    [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MetaTag();

    [GeneratedRegex(@"([a-zA-Z:-]+)\s*=\s*(""([^""]*)""|'([^']*)')")]
    private static partial Regex Attribute();

    [GeneratedRegex(@"<title[^>]*>([^<]{1,500})</title>", RegexOptions.IgnoreCase)]
    private static partial Regex TitleTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
