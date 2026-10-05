using System.Net;
using System.Security.Cryptography;
using Wolf3D.Configuration;
using Wolf3D.Extensions;
using Wolf3D.Fonts;
using Wolf3D.Networking;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                    MULTIPLAYER

    The main menu's Multiplayer screen: host a game, join one (found on the local network,
    or at an address typed in), and wait in its lobby until the host starts it. Then every
    machine starts the same game, with everyone in it (Program.players, in slot order) and
    this machine's player as the local one.

    =============================================================================
    */

    /// <summary>Whether the game running is one with others over the network</summary>
    internal static bool netgame;

    // The random number table's starting place for each level of a network game (the host's pick)
    static byte netseed;

    static MultiplayerConfig? multiplayerConfig;
    static MultiplayerConfig MpConfig => multiplayerConfig ??= MultiplayerConfig.Read(MpConfigPath);
    static string MpConfigPath => _gameEngineManager.GetConfigFilePath(MultiplayerConfig.FileName);

    // --host / --join from the command line: go straight there once the game has started up
    static bool pendingHost;
    static string? pendingJoin;
    static int hostPort = NetProtocol.DefaultPort;

    /// <summary>The command line's --host, --join, --port and --name</summary>
    internal static void SetNetParams(GameParams gameParams)
    {
        pendingHost = gameParams.Host;
        pendingJoin = string.IsNullOrWhiteSpace(gameParams.Join) ? null : gameParams.Join.Trim();
        if (gameParams.Port is > 0 and < 65536 and var port)
            hostPort = port;
        if (!string.IsNullOrWhiteSpace(gameParams.Name))
        {
            MpConfig.Name = NetProtocol.CleanName(gameParams.Name);
            MpConfig.Write(MpConfigPath);
        }
    }

    /// <summary>Whether the command line asked to host or join</summary>
    internal static bool HasPendingNetStart => pendingHost || pendingJoin != null;

    /// <summary>
    /// Hosts or joins as the command line asked (once): true if a game was started from the
    /// lobby, for the demo loop to play
    /// </summary>
    internal static bool RunPendingNetStart()
    {
        if (!HasPendingNetStart)
            return false;

        bool host = pendingHost;
        var join = pendingJoin;
        pendingHost = false;
        pendingJoin = null;

        StartCPMusic(MENUSONG);
        StartGame = 0;
        if (host)
            HostGame();
        else if (join != null && ParseAddress(join) is { } at)
            ConnectTo(at.Host, at.Port);

        MenuFadeOut();
        return startgame || loadedgame;     // (joining a game being played takes it up as a loaded one)
    }

    /*
    ===============
    = What this machine runs
    ===============
    */

    static NetIdentity? localIdentity;

    /// <summary>
    /// What this machine runs, for the host to check a joining player against: the build (the
    /// exe itself), the game pack and its data files, and the mods, by name and by content
    /// </summary>
    static NetIdentity LocalIdentity()
    {
        if (localIdentity != null)
            return localIdentity;

        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(typeof(Program).Assembly.ManifestModule.ModuleVersionId.ToByteArray());
        HashFile(md5, "pfwolf.pk3");
        foreach (var mod in _assetManager.LoadedMods)
        {
            if (Directory.Exists(mod.FullPath))
            {
                foreach (var file in Directory.EnumerateFiles(mod.FullPath, "*", SearchOption.AllDirectories)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    md5.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetRelativePath(mod.FullPath, file).ToLowerInvariant()));
                    HashFile(md5, file);
                }
            }
            else
                HashFile(md5, mod.FullPath);
        }

        var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "?";
        return localIdentity = new NetIdentity(version, _gameEngineManager.GamePackId, _gameEngineManager.GameReleaseId ?? "",
            string.Join(", ", CurrentSavedMods()), Convert.ToHexString(md5.GetHashAndReset()));
    }

    static void HashFile(IncrementalHash hash, string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                hash.AppendData(buffer, 0, read);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes($"unreadable {Path.GetFileName(path)}"));
        }
    }

    /*
    ===============
    = The Multiplayer menu
    ===============
    */

    internal static CP_itemtype[] MultiplayerMenu = [];
    internal static CP_iteminfo MultiplayerItems = null!;

    /// <summary>The main menu's Multiplayer: host a game, join one, or change your name</summary>
    internal static int CP_Multiplayer(int _)
    {
        (MultiplayerMenu, MultiplayerItems) = LoadMenu("multiplayer");
        DrawMultiplayerMenu();
        MenuFadeIn();
        WaitKeyUp();

        int which;
        do
        {
            which = HandleMenu(MultiplayerItems, MultiplayerMenu, null);
            switch (SelectedId(MultiplayerMenu, which))
            {
                case "host":
                    HostGame();
                    break;
                case "join":
                    JoinGame();
                    break;
                case "name":
                    EditName();
                    break;
            }

            if (StartGame != 0)
                return 0;       // a game started: out to it
            if (which >= 0)
            {
                DrawMultiplayerMenu();
                MenuFadeIn();
            }
        }
        while (which >= 0);

        MenuFadeOut();
        return 0;
    }

    static void DrawMultiplayerMenu()
    {
        DrawMenuComponents("multiplayer");
        DrawMenu(MultiplayerItems, MultiplayerMenu);
        DrawMenuChoice(MultiplayerItems, MultiplayerMenu, "name", FitText(MpConfig.Name, MenuChoiceWidth, MENU_FONT));
        DrawMenuGun(MultiplayerItems);
        _videoManager.Update();
    }

    static void EditName()
    {
        var language = _assetManager.GetText("en-us");
        var name = AskLine("$STR_MP_ENTERNAME".ToLanguageText(language), MpConfig.Name, NetProtocol.MaxNameLength);
        if (name == null)
            return;
        MpConfig.Name = NetProtocol.CleanName(name);
        MpConfig.Write(MpConfigPath);
    }

    /// <summary>
    /// Asks for a line of text in a box over the screen: what was typed, or null if Esc was
    /// pressed (or nothing was typed)
    /// </summary>
    static string? AskLine(string prompt, string start, int maxChars)
    {
        const int X = 32, Y = 80, W = 256, H = 40;
        DrawWindow(X, Y, W, H, "BKGDCOLOR");
        DrawOutline(X, Y, W, H, "DEACTIVE", "HIGHLIGHT");
        TextAt(X + 8, Y + 6, new TextStyle(SMALL_FONT, "READHCOLOR")).Print(prompt);
        _videoManager.Bar(X + 6, Y + 20, W - 12, 12, "BKGDCOLOR");
        _videoManager.Update();

        string input = start;
        bool ok = US_LineInput(X + 8, Y + 22, ref input, start, true, maxChars, W - 20, new TextStyle(SMALL_FONT, "HIGHLIGHT", "BKGDCOLOR"));
        _inputManager.ClearKeysDown();
        WaitKeyUp();
        return ok && input.Trim().Length > 0 ? input.Trim() : null;
    }

    /// <summary>A typed-in address: "host" or "host:port" (the default port when there's none)</summary>
    static (string Host, int Port)? ParseAddress(string text)
    {
        text = text.Trim();
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

    /*
    ===============
    = Hosting
    ===============
    */

    static void HostGame()
    {
        var language = _assetManager.GetText("en-us");
        var episodes = PlayableEpisodes();
        var settings = new LobbySettings(GameMode.Coop, episodes.Count > 0 ? episodes[0] : 0,
            Math.Min(2, _gameEngineManager.GetGameInfo().Skills.Count - 1));     // the third skill, as the menus start on

        var session = NetSession.Host(hostPort, LocalIdentity(), MpConfig.Name, newGamePlayerClass ?? DefaultPlayerClass,
            MAXPLAYERS, settings, out var error);
        if (session == null)
        {
            ShowNetMessage($"{"$STR_MP_HOSTFAILED".ToLanguageText(language)}\n\n{WrapForMessage(error ?? "")}");
            return;
        }

        session.Note($"LAN: others join at {WithPort(LocalAddress(), session.Port)}");
        publicAddressLookup = LookUpPublicAddress();
        Lobby(session);
    }

    static string WithPort(string? address, int port) =>
        address == null ? $"port {port}" : port == NetProtocol.DefaultPort ? address : $"{address}:{port}";

    /// <summary>
    /// This machine's address on the local network, for the lobby to show: the IPv4 address of
    /// the interface that is up and has a default gateway (the one actually on the network),
    /// skipping loopback, link-local (169.254.x.x) and virtual adapters.
    /// </summary>
    static string? LocalAddress()
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

    static bool IsLinkLocal(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
    }

    // The host's internet address, looked up in the background while the lobby is up
    static Task<string?>? publicAddressLookup;

    /// <summary>
    /// Asks a public "what is my IP" service for the address the internet sees this machine
    /// (its router) at. Players outside the local network join there, once the router forwards
    /// the game's UDP port to this machine. Null when offline or the service doesn't answer.
    /// </summary>
    static async Task<string?> LookUpPublicAddress()
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

    /*
    ===============
    = Joining
    ===============
    */

    static CP_itemtype[] JoinMenu = [];
    static CP_iteminfo JoinItems = null!;
    static LanBrowser? lanBrowser;

    // A row of the join screen: a game found, or typing in an address
    sealed record JoinRow(LanBrowser.FoundGame? Game);

    static void JoinGame()
    {
        using var browser = new LanBrowser();
        lanBrowser = browser;
        try
        {
            browser.Poll();
            int seen = -1;
            short curpos = 0;
            bool fadeIn = true;

            while (true)
            {
                (JoinMenu, JoinItems) = LoadMenu("join-game", Math.Min(curpos, (short)0));
                JoinItems.curpos = (short)Math.Clamp(curpos, 0, Math.Max(0, JoinMenu.Length - 1));
                if (JoinMenu[JoinItems.curpos].active == 0)
                    JoinItems.curpos = (short)Math.Max(0, Array.FindIndex(JoinMenu, item => item.active != 0));
                seen = browser.Changes;

                DrawMenuComponents("join-game");
                DrawMenu(JoinItems, JoinMenu);
                DrawMenuGun(JoinItems);
                _videoManager.Update();
                if (fadeIn)
                {
                    MenuFadeIn();
                    WaitKeyUp();
                    fadeIn = false;
                }

                int which = HandleMenu(JoinItems, JoinMenu, null, idle: () =>
                {
                    browser.Poll();
                    return browser.Changes != seen;     // the list changed: build it again
                });
                curpos = JoinItems.curpos;

                if (which == -2)
                    continue;
                if (which < 0)
                    break;

                if (JoinMenu[which].data is JoinRow { Game: { } game })
                    ConnectTo(game.Address.Address.ToString(), game.Address.Port);
                else
                {
                    var language = _assetManager.GetText("en-us");
                    var typed = AskLine("$STR_MP_ENTERADDRESS".ToLanguageText(language), MpConfig.LastAddress, 40);
                    if (typed != null && ParseAddress(typed) is { } at)
                    {
                        MpConfig.LastAddress = typed;
                        MpConfig.Write(MpConfigPath);
                        ConnectTo(at.Host, at.Port);
                    }
                }

                if (StartGame != 0)
                    return;
                fadeIn = true;
            }

            MenuFadeOut();
        }
        finally
        {
            lanBrowser = null;
        }
    }

    /// <summary>`net_find`: looks for games on the local network for two seconds and lists them</summary>
    static void Cmd_NetFind(string[] args)
    {
        using var browser = new LanBrowser();
        if (!browser.Started)
        {
            _consoleManager.Print("Couldn't open a network connection to look with");
            return;
        }
        var until = Environment.TickCount64 + 2000;
        while (Environment.TickCount64 < until)
        {
            browser.Poll();
            Thread.Sleep(10);
        }

        _consoleManager.Print(browser.Games.Count == 0 ? "No games found" : $"{browser.Games.Count} game(s) found:");
        foreach (var game in browser.Games)
            _consoleManager.Print($"  {game.Info.HostName} at {game.Address}: {game.Info.GamePack}, {game.Info.Mode}, {game.Info.Map}, "
                + $"{game.Info.Players}/{game.Info.MaxPlayers}{(game.Info.InGame ? ", started" : "")}");
    }

    /// <summary>The join screen's rows (its items-source): the games found, then typing an address</summary>
    static CP_itemtype[] BuildJoinRows()
    {
        var language = _assetManager.GetText("en-us");
        var rows = new List<CP_itemtype>();
        var games = lanBrowser?.Games ?? [];

        foreach (var game in games.Take(7))
        {
            var info = game.Info;
            var mode = info.Mode == GameMode.Deathmatch ? "$STR_MP_DEATHMATCH" : "$STR_MP_COOP";
            var text = $"{info.HostName} {info.Players}/{info.MaxPlayers} {mode.ToLanguageText(language)}{(info.InGame ? " (playing)" : "")}, {info.Map}";
            bool canJoin = info.Players < info.MaxPlayers
                && string.Equals(info.GamePack, _gameEngineManager.GamePackId, StringComparison.OrdinalIgnoreCase);
            rows.Add(new CP_itemtype((short)(canJoin ? 1 : 0), FitText(text, 240, MENU_FONT), null, new JoinRow(game)));
        }

        if (games.Count == 0)
            rows.Add(new CP_itemtype(0, "$STR_MP_LOOKING".ToLanguageText(language), null));
        rows.Add(new CP_itemtype(1, "$STR_MP_ADDRESS".ToLanguageText(language), null, new JoinRow(null)));
        return rows.ToArray();
    }

    /// <summary>Joins the game at an address: waits to be let in, then its lobby</summary>
    static void ConnectTo(string host, int port)
    {
        var language = _assetManager.GetText("en-us");
        var session = NetSession.Join(host, port, LocalIdentity(), MpConfig.Name, newGamePlayerClass ?? DefaultPlayerClass, out var error);
        if (session == null)
        {
            ShowNetMessage(WrapForMessage(error ?? ""));
            return;
        }

        var shown = port == NetProtocol.DefaultPort ? host : $"{host}:{port}";
        DrawMenuComponents("join-game");
        Message("$STR_MP_CONNECTING".ToLanguageText(language).Replace("{ADDRESS}", FitText(shown, 200, SMALL_FONT)));
        MenuFadeIn();

        // LiteNetLib gives up on its own after its connect attempts; Esc gives up sooner
        _inputManager.ClearKeysDown();
        while (!session.IsJoined && session.Error == null)
        {
            session.Poll();
            _inputManager.ProcessEvents();
            if (_inputManager.IsKeyDown(ScanCodes.sc_Escape))
            {
                _inputManager.ClearKeysDown();
                session.Dispose();
                MenuFadeOut();
                return;
            }
            GameEngineManager.DelayMs(10);
        }

        if (session.Error != null)
        {
            session.Dispose();
            MenuFadeOut();
            ShowNetMessage(WrapForMessage(session.Error));
            return;
        }

        MenuFadeOut();
        Lobby(session);
    }

    /// <summary>A message over the menu background, until a key</summary>
    static void ShowNetMessage(string text)
    {
        DrawMenuComponents("multiplayer");
        Message(text);
        MenuFadeIn();
        _inputManager.ClearKeysDown();
        _inputManager.Ack();
        MenuFadeOut();
    }

    /*
    ===============
    = The lobby
    ===============
    */

    static CP_itemtype[] LobbyMenu = [];
    static CP_iteminfo LobbyItems = null!;

    // The lobby panel's rows: up to MAXPLAYERS players, then the last chat lines
    const int LobbyPanelX = 24, LobbyPanelY = 137, LobbyPanelRow = 9, LobbyChatLines = 1;

    /// <summary>
    /// Waits in a game's lobby: the host sets the game up and starts it once everyone's ready;
    /// the others pick a class, say they're ready, and wait. Leaves the session closed, unless
    /// the game started (then StartGame is set, for the menus to leave to it).
    /// </summary>
    static void Lobby(NetSession session)
    {
        var language = _assetManager.GetText("en-us");
        (LobbyMenu, LobbyItems) = LoadMenu("lobby");
        if (FindMenuItem(LobbyMenu, "go") is { } go)
            go.text = (session.IsHost ? "$STR_MP_START" : "$STR_MP_READY").ToLanguageText(language);

        bool fadeIn = true;
        while (true)
        {
            // Turned away or left behind, or the game has started (the host's own Start included)
            if (session.Error != null)
            {
                MenuFadeOut();
                session.Dispose();
                ShowNetMessage(WrapForMessage(session.Error));
                return;
            }

            // Joined a game already being played: take it up where the host has got to
            if (session.JoinState is { } joined)
            {
                MenuFadeOut();
                if (BeginJoinedGame(session, joined) is { } problem)
                {
                    session.Dispose();
                    ShowNetMessage(WrapForMessage(problem));
                }
                return;
            }

            if (session.Started is { } started)
            {
                MenuFadeOut();
                BeginNetGame(session, started);
                return;
            }

            // The host's internet address has come back: say where players outside the network join
            if (publicAddressLookup is { IsCompleted: true } lookup)
            {
                publicAddressLookup = null;
                if (lookup.Result is { } address)
                    session.Note($"Internet: {WithPort(address, session.Port)} (forward UDP {session.Port} on your router)");
            }

            int seen = session.Changes;
            DrawLobby(session);
            if (fadeIn)
            {
                MenuFadeIn();
                WaitKeyUp();
                fadeIn = false;
            }

            int which = HandleMenu(LobbyItems, LobbyMenu, null, (w, delta) => StepLobbyChoice(session, w, delta),
                idle: () =>
                {
                    session.Poll();
                    return session.Changes != seen || publicAddressLookup is { IsCompleted: true };
                });

            if (which == -2)
                continue;       // something changed: draw it again

            var id = which < 0 ? "leave" : SelectedId(LobbyMenu, which);
            switch (id)
            {
                case "leave":
                    if (session.Players.Count > 1 && Confirm("$STR_MP_LEAVECONFIRM".ToLanguageText(language)) == 0)
                        break;
                    MenuFadeOut();
                    session.Dispose();
                    return;

                case "go":
                    if (session.IsHost)
                    {
                        if (session.CanStart)
                            session.StartGame((byte)US_RndT());
                        else
                        {
                            Message("$STR_MP_NOTREADY".ToLanguageText(language));
                            _inputManager.ClearKeysDown();
                            _inputManager.Ack();
                        }
                    }
                    else if (session.LocalPlayer is { } me)
                    {
                        ShootSnd();
                        session.SetLocal(me.PlayerClass, !me.Ready);
                    }
                    break;

                case "rules" when session.IsHost && session.Settings.Mode == GameMode.Deathmatch:
                    MenuFadeOut();
                    EditRules(session);
                    fadeIn = true;
                    break;

                case "chat":
                    var line = AskLine("$STR_MP_SAY".ToLanguageText(language), "", NetProtocol.MaxChatLength);
                    if (line != null)
                        session.Say(line);
                    break;

                case "mode":
                case "episode":
                case "skill":
                case "class":
                    StepLobbyChoice(session, which, 1);
                    break;
            }
        }
    }

    static void DrawLobby(NetSession session)
    {
        var language = _assetManager.GetText("en-us");
        var gameInfo = _gameEngineManager.GetGameInfo();
        var settings = session.Settings;
        var me = session.LocalPlayer;

        // Only the host changes the game; a class to pick only when there's more than one
        bool host = session.IsHost;
        FindMenuItem(LobbyMenu, "mode")?.active = (short)(host ? 1 : 0);
        FindMenuItem(LobbyMenu, "episode")?.active = (short)(host && PlayableEpisodes().Count > 1 ? 1 : 0);
        FindMenuItem(LobbyMenu, "skill")?.active = (short)(host ? 1 : 0);
        FindMenuItem(LobbyMenu, "class")?.active = (short)(PlayerClasses().Count > 1 ? 1 : 0);
        FindMenuItem(LobbyMenu, "rules")?.active = (short)(host && settings.Mode == GameMode.Deathmatch ? 1 : 0);
        if (LobbyMenu[LobbyItems.curpos].active == 0)
            LobbyItems.curpos = (short)Math.Max(0, Array.FindIndex(LobbyMenu, item => item.active != 0));

        DrawMenuComponents("lobby");
        DrawMenu(LobbyItems, LobbyMenu);

        DrawLobbyValue("mode", (settings.Mode == GameMode.Deathmatch ? "$STR_MP_DEATHMATCH" : "$STR_MP_COOP").ToLanguageText(language));
        DrawLobbyValue("episode", EpisodeLabel(settings.Episode));
        DrawLobbyValue("skill", gameInfo.Skills.Values.ElementAtOrDefault(settings.Skill)?.Name.ToLanguageText(language) ?? "?");
        DrawLobbyValue("class", me == null ? "" : PlayerClassLabel(me.PlayerClass));
        DrawLobbyValue("rules", RulesSummary(settings));
        if (!host)
            DrawMenuCheckbox(LobbyItems, LobbyMenu, "go", me?.Ready == true);

        // Who's in, and the last thing or two said
        var panel = new TextStyle(SMALL_FONT, "TEXTCOLOR");
        int y = LobbyPanelY;
        for (int slot = 0; slot < MAXPLAYERS; slot++)
        {
            var p = session.Players.FirstOrDefault(pl => pl.Slot == slot);
            string text = p == null ? "-"
                : $"{p.Name}{(slot == 0 ? " (host)" : "")}  {PlayerClassLabel(p.PlayerClass)}  {(slot == 0 ? "" : p.Ready ? "ready" : "not ready")}";
            var style = p != null && p.Slot == session.LocalSlot ? new TextStyle(SMALL_FONT, "HIGHLIGHT") : panel;
            TextAt(LobbyPanelX, y, style).Print(FitText($"{slot + 1}. {text}", 272, SMALL_FONT));
            y += LobbyPanelRow;
        }

        foreach (var line in session.Chat.TakeLast(LobbyChatLines))
        {
            TextAt(LobbyPanelX, y, new TextStyle(SMALL_FONT, "READCOLOR")).Print(FitText(line, 272, SMALL_FONT));
            y += LobbyPanelRow;
        }

        session.MapLabel = EpisodeLabel(settings.Episode);
        DrawMenuGun(LobbyItems);
        _videoManager.Update();
    }

    // A lobby row's value, to the right of its name (cleared first)
    static void DrawLobbyValue(string id, string value)
    {
        int index = Array.FindIndex(LobbyMenu, item => item.id == id);
        if (index < 0)
            return;
        int x = LobbyItems.x + LobbyItems.indent + 88;
        int y = LobbyItems.y + index * LobbyItems.rowHeight;
        _videoManager.Bar(x, y, 300 - x, LobbyItems.rowHeight, "BKGDCOLOR");
        TextAt(x, y + 2, new TextStyle(SMALL_FONT, MenuItemColor(LobbyMenu[index], false))).Print(FitText(value, 300 - x, SMALL_FONT));
    }

    /// <summary>A deathmatch's rules in a few words, for the lobby: "20 frags, 10 min, monsters"</summary>
    static string RulesSummary(LobbySettings settings)
    {
        if (settings.Mode != GameMode.Deathmatch)
            return "-";
        var parts = new List<string>
        {
            settings.FragLimit > 0 ? $"{settings.FragLimit} frags" : "no frag limit",
        };
        if (settings.TimeLimit > 0)
            parts.Add($"{settings.TimeLimit} min");
        if (settings.Monsters)
            parts.Add("monsters");
        return string.Join(", ", parts);
    }

    static readonly int[] FragLimits = [0, 5, 10, 15, 20, 25, 30, 50, 100];
    static readonly int[] TimeLimits = [0, 5, 10, 15, 20, 30, 45, 60];

    static CP_itemtype[] RulesMenu = [];
    static CP_iteminfo RulesItems = null!;

    /// <summary>The host sets a deathmatch's rules (left/right, or picking them; Esc goes back)</summary>
    static void EditRules(NetSession session)
    {
        (RulesMenu, RulesItems) = LoadMenu("net-rules");
        DrawRules(session);
        MenuFadeIn();
        WaitKeyUp();

        int which;
        do
        {
            int seen = session.Changes;
            which = HandleMenu(RulesItems, RulesMenu, null, (w, delta) => StepRule(session, w, delta),
                idle: () =>
                {
                    session.Poll();         // others can still join meanwhile
                    return false;
                });
            if (which >= 0)
                StepRule(session, which, 1);
        }
        while (which >= 0);

        MenuFadeOut();
    }

    static void StepRule(NetSession session, int which, int delta)
    {
        var settings = session.Settings;
        RulesItems.curpos = (short)which;
        settings = SelectedId(RulesMenu, which) switch
        {
            "fraglimit" => settings with { FragLimit = FragLimits[StepMenuChoice(Math.Max(0, Array.IndexOf(FragLimits, settings.FragLimit)), FragLimits.Length, delta, wrap: true)] },
            "timelimit" => settings with { TimeLimit = TimeLimits[StepMenuChoice(Math.Max(0, Array.IndexOf(TimeLimits, settings.TimeLimit)), TimeLimits.Length, delta, wrap: true)] },
            "monsters" => settings with { Monsters = !settings.Monsters },
            "itemrespawn" => settings with { ItemRespawn = !settings.ItemRespawn },
            _ => settings,
        };
        if (settings == session.Settings)
            return;
        session.SetSettings(settings);
        _audioManager.Play("menu/move1");
        DrawRules(session);
    }

    static void DrawRules(NetSession session)
    {
        var language = _assetManager.GetText("en-us");
        var settings = session.Settings;
        DrawMenuComponents("net-rules");
        DrawMenu(RulesItems, RulesMenu);
        var none = "$STR_MP_NONE".ToLanguageText(language);
        DrawMenuChoice(RulesItems, RulesMenu, "fraglimit", settings.FragLimit > 0 ? settings.FragLimit.ToString() : none);
        DrawMenuChoice(RulesItems, RulesMenu, "timelimit", settings.TimeLimit > 0 ? $"{settings.TimeLimit} min" : none);
        DrawMenuCheckbox(RulesItems, RulesMenu, "monsters", settings.Monsters);
        DrawMenuCheckbox(RulesItems, RulesMenu, "itemrespawn", settings.ItemRespawn);
        DrawMenuGun(RulesItems);
        _videoManager.Update();
    }

    /// <summary>Steps a lobby setting (left/right, or picking it)</summary>
    static void StepLobbyChoice(NetSession session, int which, int delta)
    {
        var id = SelectedId(LobbyMenu, which);
        var settings = session.Settings;
        LobbyItems.curpos = (short)which;

        switch (id)
        {
            case "mode" when session.IsHost:
                session.SetSettings(settings with { Mode = settings.Mode == GameMode.Coop ? GameMode.Deathmatch : GameMode.Coop });
                break;

            case "episode" when session.IsHost:
                var episodes = PlayableEpisodes();
                if (episodes.Count == 0)
                    return;
                int at = Math.Max(0, episodes.IndexOf(settings.Episode));
                session.SetSettings(settings with { Episode = episodes[StepMenuChoice(at, episodes.Count, delta, wrap: true)] });
                break;

            case "skill" when session.IsHost:
                int skills = _gameEngineManager.GetGameInfo().Skills.Count;
                session.SetSettings(settings with { Skill = StepMenuChoice(settings.Skill, skills, delta, wrap: true) });
                break;

            case "class" when session.LocalPlayer is { } me:
                var classes = PlayerClasses();
                int current = Math.Max(0, classes.FindIndex(c => string.Equals(c, me.PlayerClass, StringComparison.OrdinalIgnoreCase)));
                var next = classes[StepMenuChoice(current, classes.Count, delta, wrap: true)];
                newGamePlayerClass = next;      // the next game played alone starts as it too
                session.SetLocal(next, me.Ready);
                break;

            default:
                return;
        }
        _audioManager.Play("menu/move1");
    }

    /// <summary>The episodes a game can start on: their places in game-info's, leaving out locked ones (the shareware's)</summary>
    static List<int> PlayableEpisodes() =>
        _gameEngineManager.GetGameInfo().Episodes.Values
            .Select((episode, i) => (episode, i))
            .Where(e => !e.episode.Locked && _gameEngineManager.GetGameInfo().Maps.ContainsKey(e.episode.StartMap))
            .Select(e => e.i)
            .ToList();

    static string EpisodeLabel(int episode)
    {
        var language = _assetManager.GetText("en-us");
        var info = _gameEngineManager.GetGameInfo().Episodes.Values.ElementAtOrDefault(episode);
        return info == null ? "?" : info.Name.ToLanguageText(language).Replace('\n', ' ');
    }

    static string PlayerClassLabel(string playerClass)
    {
        var language = _assetManager.GetText("en-us");
        var info = _gameEngineManager.GetGameInfo().PlayerClasses
            .FirstOrDefault(c => string.Equals(c.Key, playerClass, StringComparison.OrdinalIgnoreCase)).Value;
        return (info?.Name ?? _inventoryManager.GetStringProperty(playerClass, "tag") ?? playerClass).ToLanguageText(language);
    }

    /*
    ===============
    = Starting the game
    ===============
    */

    /// <summary>
    /// Starts the game the host started, here: its episode and skill, everyone in it in slot
    /// order (each with their class), this machine's player the local one, and the levels'
    /// random numbers starting where the host said, so every machine begins alike.
    /// </summary>
    static void BeginNetGame(NetSession session, StartGameInfo start)
    {
        var gameInfo = _gameEngineManager.GetGameInfo();
        var episode = gameInfo.Episodes.Values.ElementAtOrDefault(start.Settings.Episode) ?? gameInfo.Episodes.Values.First();
        var mapInfo = gameInfo.Maps[episode.StartMap];

        string ClassOrDefault(string playerClass) => FindPlayerClass(playerClass) ?? DefaultPlayerClass;

        // The game as one player's, then everyone in it
        NewGame((short)Math.Clamp(start.Settings.Skill, 0, gameInfo.Skills.Count - 1), episode, mapInfo,
            ClassOrDefault(start.Players[0].PlayerClass));

        players.Clear();
        for (int i = 0; i < start.Players.Count; i++)
        {
            var p = new Entities.PlayerState { Number = i, Name = start.Players[i].Name };
            players.Add(p);
            using (ActAs(p))
                StartPlayer(ClassOrDefault(start.Players[i].PlayerClass));
        }
        consoleplayer = Math.Max(0, start.Players.FindIndex(p => p.Slot == session.LocalSlot));
        SetActing(localplayer);
        SetGameMode(start.Settings.Mode);

        godmode = noclip = ammocheat = 0;     // no cheats carried in from playing alone
        netgame = true;
        netseed = start.Seed;
        var rules = start.Settings;
        netrules = new NetRules(rules.Monsters || rules.Mode == GameMode.Coop, rules.FragLimit, rules.TimeLimit, rules.ItemRespawn);
        matchover = false;
        showscoreboard = false;
        netlevel = 0;
        netLeft = false;
        session.BeginPlaying();
        StartGame = 1;
    }

    /// <summary>A game with others has ended (or never started): back to playing alone</summary>
    internal static void EndNetGame()
    {
        var error = NetSession.Current?.Error;
        netgame = false;
        netLeft = false;
        NetSession.Current?.Dispose();
        if (error != null)
            ShowNetMessage(WrapForMessage(error));     // the host left, or the connection was lost
    }
}
