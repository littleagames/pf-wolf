using System.Net;
using LiteNetLib;
using LiteNetLib.Utils;
using GameMode = Wolf3D.Program.GameMode;

namespace Wolf3D.Networking;

/// <summary>
/// This machine's part in a game with others: hosting it, or joined to someone who is. Holds
/// the lobby as the host has it (settings, players, chat) and, once the host starts, the game
/// they all start. Nothing happens on its own: <see cref="Poll"/> takes in what has arrived,
/// and the screens read the result (watching <see cref="Changes"/> to know when to redraw).
/// </summary>
internal sealed class NetSession : IDisposable
{
    /// <summary>The session open now, hosting or joined; null when playing alone</summary>
    public static NetSession? Current { get; private set; }

    private readonly NetManager _net;
    private readonly EventBasedNetListener _listener = new();
    private readonly NetIdentity _identity;
    private readonly int _maxPlayers;

    // Host: which slot each connected player's peer has. Client: the host
    private readonly Dictionary<int, int> _slotOfPeer = [];
    private NetPeer? _host;

    // Tells this host from others when it answers on each of its addresses
    private readonly int _hostId = Random.Shared.Next();

    public bool IsHost { get; }

    /// <summary>A client in the lobby: welcomed by the host (before that, still connecting)</summary>
    public bool IsJoined => IsHost || LocalSlot >= 0;

    /// <summary>This machine's player's slot; -1 until the host has welcomed us</summary>
    public int LocalSlot { get; private set; } = -1;

    public LobbySettings Settings { get; private set; }

    /// <summary>Everyone in the lobby, in slot order</summary>
    public List<LobbyPlayer> Players { get; private set; } = [];

    /// <summary>Chat and comings and goings, oldest first (the last few)</summary>
    public List<string> Chat { get; } = [];

    /// <summary>Why the session is over (refused, host gone, connection lost); null while it's open</summary>
    public string? Error { get; private set; }

    /// <summary>The game the host started; null until they do</summary>
    public StartGameInfo? Started { get; private set; }

    /// <summary>Goes up whenever anything above changes, so a screen knows to draw it again</summary>
    public int Changes { get; private set; }

    /// <summary>The host's map, as finding games shows it (the host's screens keep it up to date)</summary>
    public string MapLabel { get; set; } = "";

    /// <summary>The port it hosts on, or the host's</summary>
    public int Port { get; }

    public LobbyPlayer? LocalPlayer => Players.FirstOrDefault(p => p.Slot == LocalSlot);

    private NetSession(bool isHost, NetIdentity identity, int port, int maxPlayers, LobbySettings settings)
    {
        IsHost = isHost;
        _identity = identity;
        Port = port;
        _maxPlayers = maxPlayers;
        Settings = settings;
        _net = new NetManager(_listener)
        {
            AutoRecycle = true,
            UnconnectedMessagesEnabled = isHost,     // a host answers the ones looking for games
            BroadcastReceiveEnabled = isHost,
            DisconnectTimeout = NetProtocol.DisconnectTimeoutMs,
        };

        _listener.ConnectionRequestEvent += OnConnectionRequest;
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnNetworkReceive;
        _listener.NetworkReceiveUnconnectedEvent += OnNetworkReceiveUnconnected;
    }

    /// <summary>
    /// Hosts a game on <paramref name="port"/>, with this machine's player in slot 0. Null, with
    /// the reason in <paramref name="error"/>, if the port can't be used.
    /// </summary>
    public static NetSession? Host(int port, NetIdentity identity, string name, string playerClass, int maxPlayers,
        LobbySettings settings, out string? error)
    {
        Current?.Dispose();
        var session = new NetSession(true, identity, port, maxPlayers, settings);
        if (!session._net.Start(port))
        {
            error = $"Couldn't host on port {port}: it may be in use.";
            return null;
        }

        session.LocalSlot = 0;
        session.Players = [new LobbyPlayer(0, NetProtocol.CleanName(name), playerClass, Ready: true)];
        error = null;
        return Current = session;
    }

    /// <summary>Starts joining the game hosted at <paramref name="address"/>: <see cref="IsJoined"/> once the host lets us in</summary>
    public static NetSession? Join(string address, int port, NetIdentity identity, string name, string playerClass, out string? error)
    {
        Current?.Dispose();
        var session = new NetSession(false, identity, port, 0, new LobbySettings(GameMode.Coop, 0, 0));
        if (!session._net.Start())
        {
            error = "Couldn't open a network connection.";
            return null;
        }

        try
        {
            session._host = session._net.Connect(address, port,
                new Hello(NetProtocol.Version, identity, NetProtocol.CleanName(name), playerClass).Write());
        }
        catch (Exception e) when (e is System.Net.Sockets.SocketException or ArgumentException or FormatException)
        {
            session._net.Stop();
            error = $"Couldn't reach {address}: {e.Message}";
            return null;
        }

        error = null;
        return Current = session;
    }

    /// <summary>Takes in whatever has arrived since the last time</summary>
    public void Poll() => _net.PollEvents();

    public void Dispose()
    {
        _net.Stop();
        if (Current == this)
            Current = null;
    }

    /*
    ===============
    = Lobby changes
    ===============
    */

    /// <summary>The host changes the game's settings</summary>
    public void SetSettings(LobbySettings settings)
    {
        if (!IsHost || settings == Settings)
            return;
        Settings = settings;
        Changed();
        SendLobby();
    }

    /// <summary>This machine's player picks a class or says they're ready</summary>
    public void SetLocal(string playerClass, bool ready)
    {
        if (LocalPlayer is not { } me || (me.PlayerClass == playerClass && me.Ready == ready))
            return;

        if (IsHost)
        {
            ReplacePlayer(me with { PlayerClass = playerClass, Ready = true });
            SendLobby();
        }
        else if (_host != null)
        {
            var w = NetProtocol.Message(NetMessage.LobbyChange);
            w.Put(playerClass);
            w.Put(ready);
            _host.Send(w, DeliveryMethod.ReliableOrdered);
        }
    }

    /// <summary>This machine's player says something to everyone</summary>
    public void Say(string text)
    {
        text = text.Trim();
        if (text.Length == 0 || LocalSlot < 0)
            return;
        if (text.Length > NetProtocol.MaxChatLength)
            text = text[..NetProtocol.MaxChatLength];

        if (IsHost)
            Relay(LocalSlot, text);
        else if (_host != null)
        {
            var w = NetProtocol.Message(NetMessage.Chat);
            w.Put((byte)LocalSlot);
            w.Put(text);
            _host.Send(w, DeliveryMethod.ReliableOrdered);
        }
    }

    /// <summary>Whether the host can start: someone to play with, all of them ready</summary>
    public bool CanStart => IsHost && Players.Count > 1 && Players.All(p => p.Ready);

    /// <summary>The host starts the game for everyone, with the random number table starting at <paramref name="seed"/></summary>
    public void StartGame(byte seed)
    {
        if (!CanStart)
            return;
        Started = new StartGameInfo(Settings, seed, Players.OrderBy(p => p.Slot).ToList());
        var w = NetProtocol.Message(NetMessage.StartGame);
        Started.Write(w);
        _net.SendToAll(w, DeliveryMethod.ReliableOrdered);
        Changed();
    }

    /*
    ===============
    = Network events
    ===============
    */

    private void OnConnectionRequest(ConnectionRequest request)
    {
        if (!IsHost)
        {
            request.Reject();
            return;
        }

        var hello = Hello.Read(request.Data);
        string? refusal =
            hello == null ? "That isn't a PFWolf game."
            : hello.Version != NetProtocol.Version ? $"The host plays network version {NetProtocol.Version}; you play {hello.Version}."
            : _identity.Mismatch(hello.Identity)
            ?? (Started != null ? "The game has already started."
            : Players.Count >= _maxPlayers ? $"The game is full ({_maxPlayers} players)."
            : null);

        if (refusal != null)
        {
            request.Reject(NetRefusal.Write(refusal));
            return;
        }

        int slot = Enumerable.Range(0, _maxPlayers).First(s => Players.All(p => p.Slot != s));
        var peer = request.Accept();
        _slotOfPeer[peer.Id] = slot;
        AddPlayer(new LobbyPlayer(slot, NetProtocol.CleanName(hello!.Name), hello.PlayerClass, Ready: false));
    }

    private void OnPeerConnected(NetPeer peer)
    {
        if (!IsHost)
            return;     // a client waits for the host's Welcome

        if (!_slotOfPeer.TryGetValue(peer.Id, out var slot))
            return;
        var w = NetProtocol.Message(NetMessage.Welcome);
        w.Put((byte)slot);
        peer.Send(w, DeliveryMethod.ReliableOrdered);
        Note($"{NameOf(slot)} joined");
        SendLobby();
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        if (IsHost)
        {
            if (_slotOfPeer.Remove(peer.Id, out var slot))
            {
                Note($"{NameOf(slot)} left");
                Players.RemoveAll(p => p.Slot == slot);
                Changed();
                SendLobby();
            }
            return;
        }

        Error = info.Reason switch
        {
            DisconnectReason.ConnectionRejected => NetRefusal.Read(info.AdditionalData) ?? "The host turned you away.",
            DisconnectReason.ConnectionFailed => "Couldn't reach the host.",
            DisconnectReason.Timeout => "Lost the connection to the host.",
            DisconnectReason.RemoteConnectionClose => "The host left the game.",
            _ => $"Disconnected ({info.Reason}).",
        };
        Changed();
    }

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
    {
        if (reader.AvailableBytes == 0)
            return;
        var type = (NetMessage)reader.GetByte();

        try
        {
            if (IsHost)
                HostReceive(peer, type, reader);
            else
                ClientReceive(type, reader);
        }
        catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException or InvalidOperationException)
        {
            Console.WriteLine($"Network: a bad {type} message ({e.Message})");
        }
    }

    private void HostReceive(NetPeer peer, NetMessage type, NetPacketReader reader)
    {
        if (!_slotOfPeer.TryGetValue(peer.Id, out var slot) || Players.FirstOrDefault(p => p.Slot == slot) is not { } player)
            return;

        switch (type)
        {
            case NetMessage.LobbyChange:
                ReplacePlayer(player with { PlayerClass = reader.GetString(), Ready = reader.GetBool() });
                SendLobby();
                break;

            case NetMessage.Chat:
                reader.GetByte();       // the slot they say they are: the host knows better
                var text = reader.GetString();
                if (text.Length > NetProtocol.MaxChatLength)
                    text = text[..NetProtocol.MaxChatLength];
                Relay(slot, text);
                break;
        }
    }

    private void ClientReceive(NetMessage type, NetPacketReader reader)
    {
        switch (type)
        {
            case NetMessage.Welcome:
                LocalSlot = reader.GetByte();
                Changed();
                break;

            case NetMessage.Lobby:
                Settings = LobbySettings.Read(reader);
                Players = LobbyPlayer.ReadList(reader);
                Changed();
                break;

            case NetMessage.Chat:
                int slot = reader.GetByte();
                Note($"{NameOf(slot)}: {reader.GetString()}");
                break;

            case NetMessage.StartGame:
                Started = StartGameInfo.Read(reader);
                Changed();
                break;
        }
    }

    // A host answers anyone on the network looking for games
    private void OnNetworkReceiveUnconnected(IPEndPoint remote, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        if (!IsHost || reader.AvailableBytes == 0 || (NetMessage)reader.GetByte() != NetMessage.DiscoveryRequest)
            return;
        if (!reader.TryGetString(out var magic) || magic != NetProtocol.Magic)
            return;

        var w = NetProtocol.Message(NetMessage.DiscoveryReply);
        w.Put(NetProtocol.Magic);
        new HostInfo(_hostId, NameOf(0), _identity.GamePack, Players.Count, _maxPlayers, Started != null, Settings.Mode, MapLabel).Write(w);
        _net.SendUnconnectedMessage(w, remote);
    }

    /*
    ===============
    = Lobby state
    ===============
    */

    private void AddPlayer(LobbyPlayer player)
    {
        Players.Add(player);
        Players.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        Changed();
    }

    private void ReplacePlayer(LobbyPlayer player)
    {
        int i = Players.FindIndex(p => p.Slot == player.Slot);
        if (i >= 0 && Players[i] != player)
        {
            Players[i] = player;
            Changed();
        }
    }

    // The host sends everyone the lobby as it stands
    private void SendLobby()
    {
        if (!IsHost)
            return;
        var w = NetProtocol.Message(NetMessage.Lobby);
        Settings.Write(w);
        LobbyPlayer.WriteList(w, Players);
        _net.SendToAll(w, DeliveryMethod.ReliableOrdered);
    }

    // The host passes a player's chat line on to everyone (and shows it)
    private void Relay(int slot, string text)
    {
        Note($"{NameOf(slot)}: {text}");
        var w = NetProtocol.Message(NetMessage.Chat);
        w.Put((byte)slot);
        w.Put(text);
        _net.SendToAll(w, DeliveryMethod.ReliableOrdered);
    }

    private string NameOf(int slot) => Players.FirstOrDefault(p => p.Slot == slot)?.Name ?? $"Player {slot + 1}";

    /// <summary>Adds a line to the chat (comings and goings, and the like)</summary>
    internal void Note(string line)
    {
        const int Kept = 20;
        Chat.Add(line);
        if (Chat.Count > Kept)
            Chat.RemoveAt(0);
        Changed();
    }

    private void Changed() => Changes++;
}
