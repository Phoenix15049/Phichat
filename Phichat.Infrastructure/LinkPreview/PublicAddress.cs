using System.Net;
using System.Net.Sockets;

namespace Phichat.Infrastructure.LinkPreview;

/// <summary>
/// Decides whether the server may connect to an address on a user's behalf. Only public unicast
/// addresses qualify, so link previews and push deliveries cannot reach the server itself or the internal network (SSRF).
/// </summary>
public static class PublicAddress
{
    /// <summary>
    /// <see cref="SocketsHttpHandler.ConnectCallback"/> that resolves the host and connects only to an allowed
    /// (public) address, so DNS rebinding cannot slip past an earlier check.
    /// </summary>
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? new[] { literal }
            : await Dns.GetHostAddressesAsync(host, ct);

        var target = addresses.FirstOrDefault(IsAllowed)
            ?? throw new HttpRequestException("Destination address is not allowed.");

        // Every resolved address must be public: a host mixing public and private records is refused.
        if (addresses.Any(a => !IsAllowed(a)))
            throw new HttpRequestException("Destination address is not allowed.");

        var socket = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(target, context.DnsEndPoint.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public static bool IsAllowed(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(
                b[0] == 0 ||                                   // 0.0.0.0/8 "this network"
                b[0] == 10 ||                                  // 10.0.0.0/8 private
                b[0] == 127 ||                                 // loopback
                (b[0] == 100 && b[1] >= 64 && b[1] <= 127) ||  // 100.64.0.0/10 carrier-grade NAT
                (b[0] == 169 && b[1] == 254) ||                // link-local (cloud metadata lives here)
                (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||   // 172.16.0.0/12 private
                (b[0] == 192 && b[1] == 0 && b[2] == 0) ||     // 192.0.0.0/24 IETF protocol assignments
                (b[0] == 192 && b[1] == 0 && b[2] == 2) ||     // TEST-NET-1
                (b[0] == 192 && b[1] == 168) ||                // 192.168.0.0/16 private
                (b[0] == 198 && (b[1] == 18 || b[1] == 19)) || // benchmarking
                (b[0] == 198 && b[1] == 51 && b[2] == 100) ||  // TEST-NET-2
                (b[0] == 203 && b[1] == 0 && b[2] == 113) ||   // TEST-NET-3
                b[0] >= 224                                    // multicast, reserved, broadcast
            );
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.IPv6None) || address.Equals(IPAddress.IPv6Any)) return false;
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast) return false;

            var b = address.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC) return false;                 // fc00::/7 unique local
            if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8) return false; // 2001:db8::/32 documentation
            if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B) return false; // 64:ff9b::/96 NAT64 (could reach IPv4 internals)
            if (b[0] == 0x20 && b[1] == 0x02) return false;          // 2002::/16 6to4 (embeds IPv4)
            return true;
        }

        return false;
    }
}
