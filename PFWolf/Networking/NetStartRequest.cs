using PFWolf.Configuration;

namespace PFWolf.Networking;

/// <summary>
/// The command line's --host, --join and --port: go straight to hosting or joining once the
/// game has started up, on that port. (--name is saved to multiplayer.cfg by the caller.)
/// </summary>
internal sealed class NetStartRequest
{
    bool pendingHost;
    string? pendingJoin;

    /// <summary>The port a game is hosted on, and checked: --port, else the default</summary>
    internal int HostPort { get; private set; } = NetProtocol.DefaultPort;

    internal void SetParams(GameParams gameParams)
    {
        pendingHost = gameParams.Host;
        pendingJoin = string.IsNullOrWhiteSpace(gameParams.Join) ? null : gameParams.Join.Trim();
        if (gameParams.Port is > 0 and < 65536 and var port)
            HostPort = port;
    }

    /// <summary>Whether the command line asked to host or join</summary>
    internal bool HasPending => pendingHost || pendingJoin != null;

    /// <summary>What it asked (once): host, or join at an address (null when it asked neither)</summary>
    internal (bool Host, string? Join) Take()
    {
        var taken = (pendingHost, pendingJoin);
        pendingHost = false;
        pendingJoin = null;
        return taken;
    }
}
