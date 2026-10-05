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
        // Resolved here rather than by LiteNetLib, which only says "UnknownHost" without the address
        IPEndPoint endPoint;
        try
        {
            endPoint = new IPEndPoint(NetUtils.ResolveAddress(address), port);
        }
        catch (Exception e) when (e is System.Net.Sockets.SocketException or ArgumentException)
        {
            error = $"Couldn't find the address \"{address}\". Type an IP address like 192.168.1.12, or a host name.";
            return null;
        }
        return Join(endPoint, identity, name, playerClass, out error);
    }

    /// <summary>Starts joining the game hosted at <paramref name="endPoint"/></summary>
    public static NetSession? Join(IPEndPoint endPoint, NetIdentity identity, string name, string playerClass, out string? error)
    {
        int port = endPoint.Port;
        Current?.Dispose();
        var session = new NetSession(false, identity, port, 0, new LobbySettings(GameMode.Coop, 0, 0));
        if (!session._net.Start())
        {
            error = "Couldn't open a network connection.";
            return null;
        }

        try
        {
            session._host = session._net.Connect(endPoint,
                new Hello(NetProtocol.Version, identity, NetProtocol.CleanName(name), playerClass).Write());
        }
        catch (Exception e) when (e is System.Net.Sockets.SocketException or ArgumentException or FormatException)
        {
            session._net.Stop();
            error = $"Couldn't reach {endPoint}: {e.Message}";
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
    = Playing

    Once the game has started, each player is known by their place in Started.Players (their
    index), as Program.players has them. Each frame, every machine sends its player's controls
    for a step a little ahead (SubmitLocal); the host gathers everyone's for a step and sends
    them all out together (a TicBundle), and every machine plays that step with them
    (TryTake). Someone who leaves has their bit cleared in the bundles from then on, so every
    machine takes them out at the same step.
    ===============
    */

    // Steps are keyed by level and step together
    private static long Key(int level, int step) => ((long)level << 32) | (uint)step;

    private readonly Dictionary<long, Entities.TicCmd?[]> _gathering = [];   // host: controls in so far
    private readonly Dictionary<long, TicBundle> _bundles = [];              // bundles to play
    private readonly Dictionary<long, uint> _ownSums = [];                   // host: its own checksums
    private readonly Dictionary<long, Dictionary<int, uint>> _theirSums = [];   // host: others', waiting for its own
    private readonly HashSet<int> _outOfSync = [];

    // By player index: still in the game, and (host) the first step their controls are needed
    // for (a player joining mid-game sends theirs from the step after they come in)
    private readonly List<bool> _present = [];
    private readonly List<long> _neededFrom = [];
    private readonly Dictionary<int, int> _indexOfSlot = [];

    // Host: players who've left, for the next bundle to say; players joining, for the next
    // bundle to bring in; and whom to send the game to once they're in
    private byte _leaving;
    private readonly Queue<NetJoin> _joining = new();
    private readonly Dictionary<int, NetPeer> _joinPeers = [];

    /// <summary>A player joining mid-game: the game as it stands, from the host; null until it comes</summary>
    public JoinStateInfo? JoinState { get; private set; }

    /// <summary>Lines to show while playing (someone left, someone is out of sync), oldest first; taken by the game</summary>
    public Queue<string> Notices { get; } = new();

    /// <summary>The game's players, by index (in Started.Players order, with anyone who joined since)</summary>
    public int PlayerCount => Started?.Players.Count ?? 0;

    /// <summary>This machine's player's index</summary>
    public int LocalIndex => _indexOfSlot.TryGetValue(LocalSlot, out var index) ? index
        : Started?.Players.FindLastIndex(p => p.Slot == LocalSlot) ?? -1;

    /// <summary>Sets up for playing the game started (BeginNetGame), or joined (BeginJoinedGame)</summary>
    public void BeginPlaying()
    {
        _present.Clear();
        _neededFrom.Clear();
        _indexOfSlot.Clear();
        for (int i = 0; i < PlayerCount; i++)
        {
            _present.Add(true);
            _neededFrom.Add(0);
            _indexOfSlot[Started!.Players[i].Slot] = i;     // the latest to have the slot
        }
        _gathering.Clear();
        _bundles.Clear();
        _forgottenBefore = 0;
    }

    /// <summary>Forgets steps from levels before <paramref name="level"/> (a new level has begun)</summary>
    public void ForgetBefore(int level)
    {
        long first = Key(level, 0);
        _forgottenBefore = Math.Max(_forgottenBefore, first);
        foreach (var key in _gathering.Keys.Where(k => k < first).ToList())
            _gathering.Remove(key);
        foreach (var key in _bundles.Keys.Where(k => k < first).ToList())
            _bundles.Remove(key);
        foreach (var key in _ownSums.Keys.Where(k => k < first).ToList())
            _ownSums.Remove(key);
        foreach (var key in _theirSums.Keys.Where(k => k < first).ToList())
            _theirSums.Remove(key);
        if (IsHost)
            SendReadySteps();       // the new level's steps may have been waiting behind them
    }

    // Steps before this (levels already left) are over: anything still coming for them, such as
    // the controls everyone sent ahead for steps the level ended before, is dropped. Kept, a
    // step gathered from what arrives late would never fill, and hold up every step after it
    private long _forgottenBefore;

    /// <summary>This machine's player's controls for a step</summary>
    public void SubmitLocal(int level, int step, in Entities.TicCmd cmd)
    {
        if (IsHost)
            Gather(level, step, LocalIndex, cmd);
        else if (_host != null)
        {
            var w = NetProtocol.Message(NetMessage.Cmd);
            w.Put(level);
            w.Put(step);
            TicCmdCodec.Write(w, cmd);
            _host.Send(w, DeliveryMethod.ReliableOrdered);
        }
    }

    /// <summary>Everyone's controls for a step, once they're here (taking them); null until then</summary>
    public TicBundle? TryTake(int level, int step) =>
        _bundles.Remove(Key(level, step), out var bundle) ? bundle : null;

    /// <summary>Whether everyone's controls for a step are here, to be taken</summary>
    public bool HasBundle(int level, int step) => _bundles.ContainsKey(Key(level, step));

    /// <summary>The names of the players a step is still waiting on (for the "waiting for" message)</summary>
    public List<string> WaitingFor(int level, int step)
    {
        if (!IsHost)
            return [Started?.Players.FirstOrDefault(p => p.Slot == 0)?.Name ?? "the host"];
        var key = Key(level, step);
        _gathering.TryGetValue(key, out var cmds);
        return Enumerable.Range(0, PlayerCount)
            .Where(i => Needed(i, key) && (cmds == null || i >= cmds.Length || cmds[i] == null))
            .Select(i => Started!.Players[i].Name)
            .ToList();
    }

    /// <summary>
    /// The game's state summed up after a step (Program.NetPlay.cs), for the host to compare
    /// every machine's: they should never differ
    /// </summary>
    public void ReportChecksum(int level, int step, uint sum)
    {
        if (IsHost)
        {
            var key = Key(level, step);
            _ownSums[key] = sum;
            if (_theirSums.Remove(key, out var theirs))
                foreach (var (index, theirSum) in theirs)
                    CompareSum(index, level, step, sum, theirSum);
        }
        else if (_host != null)
        {
            var w = NetProtocol.Message(NetMessage.Checksum);
            w.Put(level);
            w.Put(step);
            w.Put(sum);
            _host.Send(w, DeliveryMethod.ReliableOrdered);
        }
    }

    /// <summary>
    /// Host: sends a player who has just joined the game as it stands after the step they came
    /// in at (Program.NetJoin.cs), so they can play on from the next
    /// </summary>
    public void SendJoinState(int index, int level, int step, byte[] state)
    {
        if (!IsHost || Started == null || !_joinPeers.Remove(index, out var peer))
            return;
        var w = NetProtocol.Message(NetMessage.JoinState);
        Started.Write(w);
        w.Put(level);
        w.Put(step);
        w.Put(state);          // last, as it is: its length can be over 64K, more than PutBytesWithLength holds
        peer.Send(w, DeliveryMethod.ReliableOrdered);
    }

    // Host: whether a player's controls are needed for a step
    private bool Needed(int index, long key) => index < _present.Count && _present[index] && key >= _neededFrom[index];

    // Host: a player's controls for a step; once everyone still in has sent theirs, out they go
    private void Gather(int level, int step, int index, in Entities.TicCmd cmd)
    {
        if (index < 0 || index >= PlayerCount)
            return;
        var key = Key(level, step);
        if (key < _forgottenBefore)
            return;
        if (!_gathering.TryGetValue(key, out var cmds) || cmds.Length < PlayerCount)
        {
            var grown = new Entities.TicCmd?[PlayerCount];
            cmds?.CopyTo(grown, 0);
            _gathering[key] = cmds = grown;
        }
        cmds[index] = cmd;
        SendReadySteps();
    }

    // Host: sends every step that has everyone's controls, in order
    private void SendReadySteps()
    {
        foreach (var key in _gathering.Keys.OrderBy(k => k).ToList())
        {
            var cmds = _gathering[key];
            if (Enumerable.Range(0, PlayerCount).Any(i => Needed(i, key) && (i >= cmds.Length || cmds[i] == null)))
                return;     // not yet: nor any after it, which have to go out after it

            // Anyone joining comes in at this step, and plays from the next
            NetJoin? join = null;
            if (_joining.TryDequeue(out var joining))
            {
                join = joining;
                _neededFrom[joining.Index] = key + 1;
            }

            var all = new Entities.TicCmd[PlayerCount];
            for (int i = 0; i < all.Length; i++)
                all[i] = TicCmdCodec.RoundTrip(i < cmds.Length && Needed(i, key) ? cmds[i] ?? default : default);

            var bundle = new TicBundle((int)(key >> 32), (int)(uint)key, _leaving, join, all);
            _leaving = 0;
            _gathering.Remove(key);
            _bundles[key] = bundle;

            var w = NetProtocol.Message(NetMessage.Tics);
            bundle.Write(w);
            _net.SendToAll(w, DeliveryMethod.ReliableOrdered);
        }
    }
    /// <summary>Host: how many of the others' checksums have been compared with its own, and how many matched</summary>
    public int SumsCompared { get; private set; }
    public int SumsMatched { get; private set; }

    // Host: a player's checksum against its own for the same step
    private void CompareSum(int index, int level, int step, uint ours, uint theirs)
    {
        SumsCompared++;
        if (ours == theirs)
            SumsMatched++;
        if (ours == theirs || !_outOfSync.Add(index))
            return;     // fine, or already said
        var line = $"{Started!.Players[index].Name} is out of sync (level {level}, step {step})";
        Console.WriteLine($"Network: {line}: host {ours:x8}, theirs {theirs:x8}");
        Announce(line);
    }

    // Host: a line every machine shows
    private void Announce(string line)
    {
        Notices.Enqueue(line);
        var w = NetProtocol.Message(NetMessage.Notice);
        w.Put(line);
        _net.SendToAll(w, DeliveryMethod.ReliableOrdered);
    }

    // Host: a player has gone mid-game; the game goes on without them
    private void PlayerGone(int slot)
    {
        if (Started == null || !_indexOfSlot.Remove(slot, out var index) || !_present[index])
            return;
        _present[index] = false;
        _leaving |= (byte)(1 << index);     // the next step out says so
        _joinPeers.Remove(index);
        Announce($"{Started.Players[index].Name} left the game");
        SendReadySteps();       // steps waiting only on them can go
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
            ?? (Players.Count >= _maxPlayers ? $"The game is full ({_maxPlayers} players)."
            : Started != null && PlayerCount >= MaxIndices ? "No more players can join this game."
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

        // Mid-game: they come in at the next step to go out, as the next player index
        if (Started != null && Players.FirstOrDefault(p => p.Slot == slot) is { } joiner)
        {
            int index = PlayerCount;
            Started = Started with { Players = [.. Started.Players, joiner with { Ready = true }] };
            _present.Add(true);
            _neededFrom.Add(long.MaxValue);     // until their step is set
            _indexOfSlot[slot] = index;
            _joinPeers[index] = peer;
            _joining.Enqueue(new NetJoin(index, joiner.Name, joiner.PlayerClass));
            Announce($"{joiner.Name} is joining the game");
        }
    }

    /// <summary>The most players a game can have had, counting those who've left (the bundles' bit masks hold 8)</summary>
    private const int MaxIndices = 8;

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        if (IsHost)
        {
            if (_slotOfPeer.Remove(peer.Id, out var slot))
            {
                PlayerGone(slot);       // mid-game: out of it, at the same step everywhere
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

            case NetMessage.Cmd when Started != null:
            {
                int level = reader.GetInt(), step = reader.GetInt();
                Gather(level, step, _indexOfSlot.GetValueOrDefault(slot, -1), TicCmdCodec.Read(reader));
                break;
            }

            case NetMessage.Checksum when Started != null:
            {
                int level = reader.GetInt(), step = reader.GetInt();
                uint sum = reader.GetUInt();
                int index = _indexOfSlot.GetValueOrDefault(slot, -1);
                if (index < 0)
                    break;
                var key = Key(level, step);
                if (key < _forgottenBefore)
                    break;      // (a level this machine has left: never to be compared)
                if (_ownSums.TryGetValue(key, out var ours))
                    CompareSum(index, level, step, ours, sum);
                else
                {
                    if (!_theirSums.TryGetValue(key, out var theirs))
                        _theirSums[key] = theirs = [];
                    theirs[index] = sum;
                }
                break;
            }
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
                var said = $"{NameOf(slot)}: {reader.GetString()}";
                Note(said);
                if (Started != null)
                    Notices.Enqueue(said);      // over the view, mid-game
                break;

            case NetMessage.StartGame:
                Started = StartGameInfo.Read(reader);
                Changed();
                break;

            case NetMessage.Tics:
                var bundle = TicBundle.Read(reader);
                if (Key(bundle.Level, bundle.Step) >= _forgottenBefore)
                    _bundles[Key(bundle.Level, bundle.Step)] = bundle;
                break;

            case NetMessage.Notice:
                Notices.Enqueue(reader.GetString());
                break;

            case NetMessage.JoinState:
            {
                var start = StartGameInfo.Read(reader);
                int level = reader.GetInt(), step = reader.GetInt();
                JoinState = new JoinStateInfo(start, level, step, reader.GetRemainingBytes());
                Started = start;
                Changed();
                break;
            }
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
        if (Started != null)
            Notices.Enqueue($"{NameOf(slot)}: {text}");     // over the view, mid-game
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
