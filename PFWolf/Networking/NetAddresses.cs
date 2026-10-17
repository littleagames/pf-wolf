using System.Net;

namespace PFWolf.Networking;

/// <summary>Addresses: typed in, shown, found for this machine, and what the port checks said</summary>
internal static class NetAddresses
{
    /// <summary>A typed-in address: "host" or "host:port" (the default port when there's none)</summary>
    internal static (string Host, int Port)? ParseAddress(string text)
    {
        // Forgive what gets pasted or typed around an address: spaces, a scheme, a trailing slash,
        // and the lobby's own "LAN: others join at" / "Internet:" wording
        text = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
        foreach (var prefix in new[] { "LAN:othersjoinat", "Internet:", "udp://", "http://", "https://" })
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                text = text[prefix.Length..];
        int note = text.IndexOf('(');
        if (note >= 0)
            text = text[..note];
        text = text.TrimEnd('/');
        if (text.Length == 0)
            return null;

        // An IPv6 address has colons of its own: [::1]:10645
        if (text.StartsWith('[') && text.IndexOf(']') is > 0 and var close)
        {
            var portPart = text[(close + 1)..].TrimStart(':');
            return (text[1..close], int.TryParse(portPart, out var p6) && p6 is > 0 and < 65536 ? p6 : NetProtocol.DefaultPort);
        }

        int colon = text.LastIndexOf(':');
        if (colon > 0 && text.IndexOf(':') == colon && int.TryParse(text[(colon + 1)..], out var port) && port is > 0 and < 65536)
            return (text[..colon], port);
        return (text, NetProtocol.DefaultPort);
    }

    /// <summary>An address as others type it in: without the port when it's the default one</summary>
    internal static string WithPort(string? address, int port) =>
        address == null ? $"port {port}" : port == NetProtocol.DefaultPort ? address : $"{address}:{port}";

    /// <summary>
    /// This machine's address on the local network, for the lobby to show: the IPv4 address
    /// traffic to the internet actually leaves from (the default route's adapter, which matters
    /// when Ethernet and Wi-Fi are both up), else that of an interface that is up and has a
    /// default gateway; skipping loopback, link-local (169.254.x.x) and virtual adapters.
    /// </summary>
    internal static string? LocalAddress()
    {
        try
        {
            var candidates = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                    && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback
                    && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel)
                .Select(n => n.GetIPProperties())
                .SelectMany(p =>
                {
                    bool hasGateway = p.GatewayAddresses.Any(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                        && !g.Address.Equals(IPAddress.Any));
                    return p.UnicastAddresses.Select(u => (u.Address, hasGateway));
                })
                .ToList();
            return PickLocalAddress(candidates, RoutedAddress())?.ToString();
        }
        catch (System.Net.NetworkInformation.NetworkInformationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Which of the interfaces' addresses others on the local network reach this machine at:
    /// <paramref name="routed"/> (where the default route leaves from) when it's one of them,
    /// else the first with a default gateway, else the first at all. Only IPv4 ones that aren't
    /// loopback or link-local count.
    /// </summary>
    internal static IPAddress? PickLocalAddress(IEnumerable<(IPAddress Address, bool HasGateway)> candidates, IPAddress? routed)
    {
        var usable = candidates
            .Where(c => c.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(c.Address) && !IsLinkLocal(c.Address))
            .ToList();
        if (routed != null && usable.Any(c => c.Address.Equals(routed)))
            return routed;
        return usable.OrderByDescending(c => c.HasGateway).Select(c => c.Address).FirstOrDefault();
    }

    /// <summary>
    /// The local IPv4 address the OS would send internet traffic from, found by connecting a
    /// UDP socket to a public address (which only picks a route; nothing is sent). Null offline.
    /// </summary>
    static IPAddress? RoutedAddress()
    {
        try
        {
            using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
            socket.Connect(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53));
            return (socket.LocalEndPoint as IPEndPoint)?.Address;
        }
        catch (System.Net.Sockets.SocketException)
        {
            return null;
        }
    }

    internal static bool IsLinkLocal(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
    }

    /// <summary>What streamer mode shows in place of an internet address</summary>
    internal const string Hidden = "(hidden)";

    /// <summary>
    /// An address only reachable from the local or a virtual network, so harmless to show:
    /// loopback, 10.x, 172.16-31.x, 192.168.x, link-local, and 100.64-127.x (carrier-grade NAT,
    /// and Tailscale); IPv6 loopback, link-local and unique-local ones too
    /// </summary>
    internal static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal || address.IsIPv6UniqueLocal || address.IsIPv6SiteLocal;
        var b = address.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] is >= 16 and <= 31)
            || (b[0] == 192 && b[1] == 168)
            || IsLinkLocal(address)
            || (b[0] == 100 && b[1] is >= 64 and <= 127);
    }

    static readonly System.Text.RegularExpressions.Regex IPv4Pattern =
        new(@"(?<![\d.])\d{1,3}(?:\.\d{1,3}){3}(?![\d.]*\d)", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Streamer mode: <paramref name="text"/> with every internet (IPv4) address in it replaced by
    /// <see cref="Hidden"/>, and <paramref name="typedHost"/> too (a host name or address typed in
    /// to join), unless it's a private one. Unchanged when <paramref name="hide"/> is off.
    /// </summary>
    internal static string Hide(string text, bool hide, string? typedHost = null)
    {
        if (!hide || string.IsNullOrEmpty(text))
            return text;
        if (!string.IsNullOrEmpty(typedHost) && !(IPAddress.TryParse(typedHost, out var typed) && IsPrivate(typed)))
            text = text.Replace(typedHost, Hidden, StringComparison.OrdinalIgnoreCase);
        return IPv4Pattern.Replace(text, m => IPAddress.TryParse(m.Value, out var a) && IsPrivate(a) ? m.Value : Hidden);
    }

    /// <summary>
    /// Asks a public "what is my IP" service for the address the internet sees this machine
    /// (its router) at. Players outside the local network join there, once the router forwards
    /// the game's UDP port to this machine. Null when offline or the service doesn't answer.
    /// </summary>
    internal static async Task<string?> LookUpPublicAddress()
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var text = (await http.GetStringAsync("https://api.ipify.org").ConfigureAwait(false)).Trim();
            return IPAddress.TryParse(text, out _) ? text : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>What a port check found, and what to do about it</summary>
    /// <param name="here">This machine's address on the local network, for forwarding the port to</param>
    internal static string DescribePortCheck(PortCheck.Result result, PortMapper.Result? mapping, int port, string? here)
    {
        here ??= "this computer";
        bool opened = mapping?.Outcome == PortMapper.Outcome.Opened;
        return result.Outcome switch
        {
            PortCheck.Outcome.Open =>
                $"Port {port} is open{(opened ? $" (your router opened it: {mapping!.Detail})" : "")}. "
                + $"Players on the internet can join at {WithPort(result.PublicAddress!.ToString(), port)}",
            PortCheck.Outcome.Closed when opened =>
                $"Your router opened port {port} ({mapping!.Detail}), but nothing from the internet got in. "
                + "Let PFWolf through Windows Firewall.",
            PortCheck.Outcome.Closed =>
                $"Port {port} is closed. "
                + (mapping == null ? "" : "Your router didn't open it (UPnP off?). ")
                + $"Forward UDP {port} to {here}, and let PFWolf through Windows Firewall.",
            PortCheck.Outcome.InUse =>
                $"Port {port} is in use here. Is a game already being hosted?",
            _ =>
                "Couldn't check: no checking server answered. Is this computer online?",
        };
    }

    /// <summary>The console's line on what the router said about opening the port</summary>
    /// <param name="here">This machine's address on the local network, for forwarding the port to</param>
    internal static string DescribePortMapping(PortMapper.Result result, string? here) => result.Outcome switch
    {
        PortMapper.Outcome.Opened => $"Router: opened UDP {result.Port} for this game ({result.Detail})",
        PortMapper.Outcome.NoRouter => $"Router: not opened ({result.Detail ?? "no UPnP or NAT-PMP answered"}); "
            + $"forward UDP {result.Port} to {here ?? "this computer"} by hand",
        _ => $"Router: wouldn't open UDP {result.Port} ({result.Detail}); forward it by hand",
    };
}
