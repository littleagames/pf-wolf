using System.Net;
using LiteNetLib;

namespace PFWolf.Networking;

/// <summary>
/// Finds games hosted on the local network: asks the whole network every second or so (a
/// broadcast to <see cref="NetProtocol.DefaultPort"/>) and lists the hosts that answer, until
/// they stop answering. Like <see cref="NetSession"/>, it only takes in answers in <see cref="Poll"/>.
/// </summary>
internal sealed class LanBrowser : IDisposable
{
    /// <summary>A host that answered: where it is, what it said, and when</summary>
    public sealed record FoundGame(IPEndPoint Address, HostInfo Info, long SeenAt);

    private const int AskEveryMs = 1000;
    private const int ForgetAfterMs = 3500;

    private readonly EventBasedNetListener _listener = new();
    private readonly NetManager _net;
    private readonly Dictionary<string, FoundGame> _found = [];
    private long _lastAsked = -AskEveryMs;     // so the first Poll asks

    /// <summary>Goes up whenever the list changes</summary>
    public int Changes { get; private set; }

    public bool Started { get; }

    public LanBrowser()
    {
        _net = new NetManager(_listener) { AutoRecycle = true, UnconnectedMessagesEnabled = true };
        _listener.NetworkReceiveUnconnectedEvent += OnReply;
        Started = _net.Start();
    }

    /// <summary>The games found, in the order they were first found</summary>
    public IReadOnlyList<FoundGame> Games => _found.Values.ToList();

    /// <summary>Asks again if it's time, takes in the answers, and forgets hosts that have gone quiet</summary>
    public void Poll()
    {
        long now = Environment.TickCount64;
        if (Started && now - _lastAsked >= AskEveryMs)
        {
            _lastAsked = now;
            var w = NetProtocol.Message(NetMessage.DiscoveryRequest);
            w.Put(NetProtocol.Magic);
            _net.SendBroadcast(w, NetProtocol.DefaultPort);
            // and this machine itself, which a broadcast can miss (a firewall that hasn't let
            // the game in yet still lets it talk to itself)
            _net.SendUnconnectedMessage(w, new IPEndPoint(IPAddress.Loopback, NetProtocol.DefaultPort));
        }

        _net.PollEvents();

        foreach (var key in _found.Where(f => now - f.Value.SeenAt > ForgetAfterMs).Select(f => f.Key).ToList())
        {
            _found.Remove(key);
            Changes++;
        }
    }

    private void OnReply(IPEndPoint remote, NetPacketReader reader, UnconnectedMessageType type)
    {
        try
        {
            if ((NetMessage)reader.GetByte() != NetMessage.DiscoveryReply || reader.GetString() != NetProtocol.Magic)
                return;
            var info = HostInfo.Read(reader);

            // One entry a host, at the best of the addresses it answers on
            var key = info.HostId.ToString();
            var address = remote;
            if (_found.TryGetValue(key, out var before) && Rank(before.Address) < Rank(remote))
                address = before.Address;
            bool news = before == null || before.Info != info || !before.Address.Equals(address);
            _found[key] = new FoundGame(address, info, Environment.TickCount64);
            if (news)
                Changes++;
        }
        catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException)
        {
            // not one of ours
        }
    }

    // How good an address is to join by: the network's IPv4 one, then this machine's own, then IPv6
    private static int Rank(IPEndPoint address) =>
        address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? (IPAddress.IsLoopback(address.Address) ? 1 : 0)
            : 2;

    public void Dispose() => _net.Stop();
}
