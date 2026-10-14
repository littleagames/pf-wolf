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
    /// This machine's address on the local network, for the lobby to show: the IPv4 address of
    /// the interface that is up and has a default gateway (the one actually on the network),
    /// skipping loopback, link-local (169.254.x.x) and virtual adapters.
    /// </summary>
    internal static string? LocalAddress()
    {
        try
        {
            var best = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                    && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback
                    && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel)
                .Select(n => n.GetIPProperties())
                .Select(p => new
                {
                    HasGateway = p.GatewayAddresses.Any(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                        && !g.Address.Equals(IPAddress.Any)),
                    Address = p.UnicastAddresses
                        .Select(u => u.Address)
                        .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                            && !IPAddress.IsLoopback(a) && !IsLinkLocal(a)),
                })
                .Where(x => x.Address != null)
                .OrderByDescending(x => x.HasGateway)
                .FirstOrDefault();
            return best?.Address?.ToString();
        }
        catch (System.Net.NetworkInformation.NetworkInformationException)
        {
            return null;
        }
    }

    internal static bool IsLinkLocal(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
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
