using System.Net;
using Phichat.Application.Interfaces;

namespace Phichat.API.Security;

/// <summary>The browser and address of the current HTTP request, for "Active sessions".</summary>
public sealed class HttpClientContext : IClientContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpClientContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string? DeviceName => DeviceNames.Describe(_accessor.HttpContext?.Request.Headers.UserAgent.ToString());

    // Behind a reverse proxy this needs the forwarded-headers middleware (production setup).
    public string? IpAddress
    {
        get
        {
            var address = _accessor.HttpContext?.Connection.RemoteIpAddress;
            if (address == null) return null;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            return address.ToString();
        }
    }
}

/// <summary>A short, language-neutral device label from a User-Agent, such as "Edge · Windows".</summary>
public static class DeviceNames
{
    public static string? Describe(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;
        var ua = userAgent;

        // Order matters: most browsers also claim to be Chrome and/or Safari.
        string? browser =
            ua.Contains("Edg/") || ua.Contains("EdgA/") || ua.Contains("EdgiOS/") ? "Edge" :
            ua.Contains("OPR/") || ua.Contains("Opera") ? "Opera" :
            ua.Contains("SamsungBrowser/") ? "Samsung Internet" :
            ua.Contains("Firefox/") || ua.Contains("FxiOS/") ? "Firefox" :
            ua.Contains("CriOS/") || ua.Contains("Chrome/") ? "Chrome" :
            ua.Contains("Safari/") && ua.Contains("Version/") ? "Safari" :
            null;

        string? os =
            ua.Contains("Windows NT") ? "Windows" :
            ua.Contains("iPhone") ? "iPhone" :
            ua.Contains("iPad") ? "iPad" :
            ua.Contains("Android") ? "Android" :
            ua.Contains("CrOS") ? "ChromeOS" :
            ua.Contains("Mac OS X") || ua.Contains("Macintosh") ? "macOS" :
            ua.Contains("Linux") ? "Linux" :
            null;

        if (browser == null && os == null) return "Unknown device";
        if (browser == null) return os;
        if (os == null) return browser;
        return $"{browser} · {os}";
    }
}
