using System.Net;
using SDL2;
using PFWolf.Configuration;
using PFWolf.Extensions;
using PFWolf.Fonts;
using PFWolf.Networking;

namespace PFWolf.Ui;

/// <summary>What the multiplayer screens need from the game</summary>
/// <param name="PlayerClass">The class this machine's player plays as: the one last picked, else the default</param>
/// <param name="ClassChosen">A class picked in the lobby: the next game played alone starts as it too</param>
/// <param name="PlayerClasses">The classes there are to pick from</param>
/// <param name="Identity">What this machine runs, for the host to check joining players against</param>
/// <param name="BeginNetGame">Starts the game the host started, here</param>
/// <param name="BeginJoinedGame">Takes up a game being played, where the host has got to: null, or what went wrong</param>
/// <param name="Started">Whether a game has started (StartGame), for the menus to leave to it</param>
/// <param name="EpisodeOpen">Whether an episode can be played: not starts-locked, or unlocked here (null: all can)</param>
internal sealed record MultiplayerHooks(Func<string> PlayerClass, Action<string> ClassChosen, Func<List<string>> PlayerClasses,
    Func<NetIdentity> Identity, Action<NetSession, StartGameInfo> BeginNetGame, Func<NetSession, JoinStateInfo, string?> BeginJoinedGame,
    Func<bool> Started, Func<Assets.EpisodeInfo, bool>? EpisodeOpen = null);

/*
=============================================================================

                                    MULTIPLAYER

The main menu's Multiplayer screen: host a game, join one (found on the local network,
or at an address typed in), and wait in its lobby until the host starts it. Then every
machine starts the same game, with everyone in it (GameSession.players, in slot order) and
this machine's player as the local one (NetGames.BeginNetGame).

=============================================================================
*/

/// <param name="game">The random numbers the host's seed comes from</param>
internal sealed class MultiplayerMenu(GameEngineManager engine, AssetManager assets, InputManager input, AudioManager audio, VideoManager video,
    InventoryManager inventory, ConsoleManager console, MenuDrawing draw, MenuLoop loop, MenuInput menuInput, MenuDefs defs, UiText ui,
    LineInput lineInput, GameSession game, NetStartRequest netStart, MultiplayerHooks hooks)
{
    MultiplayerConfig? multiplayerConfig;
    internal MultiplayerConfig MpConfig => multiplayerConfig ??= MultiplayerConfig.Read(MpConfigPath);
    internal string MpConfigPath => engine.GetConfigFilePath(MultiplayerConfig.FileName);

    /*
    ===============
    = The Multiplayer menu
    ===============
    */

    internal ControlPanelItem[] Items = [];
    internal ControlPanelMenuInfo MultiplayerItems = null!;

    /// <summary>The main menu's Multiplayer: host a game, join one, or change your name</summary>
    internal int Run()
    {
        (Items, MultiplayerItems) = defs.LoadMenu("multiplayer");
        DrawMultiplayerMenu();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();

        int which;
        do
        {
            which = loop.HandleMenu(MultiplayerItems, Items, null);
            switch (MenuDefs.SelectedId(Items, which))
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
                case "portcheck":
                    CheckPort();
                    break;
                case "openport":
                    MpConfig.OpenPort = !MpConfig.OpenPort;
                    MpConfig.Write(MpConfigPath);
                    break;
                case "streamer":
                    MpConfig.StreamerMode = !MpConfig.StreamerMode;
                    MpConfig.Write(MpConfigPath);
                    break;
            }

            if (hooks.Started())
                return 0;       // a game started: out to it
            if (which >= 0)
            {
                DrawMultiplayerMenu();
                draw.MenuFadeIn();
            }
        }
        while (which >= 0);

        draw.MenuFadeOut();
        return 0;
    }

    void DrawMultiplayerMenu()
    {
        defs.DrawMenuComponents("multiplayer");
        draw.DrawMenu(MultiplayerItems, Items);
        // The values line up just past the longer of their rows' names
        int column = Items.Where(item => item.Id is "name" or "openport" or "streamer")
            .Select(item => ui.TextWidth(item.Text, MultiplayerItems.Font ?? UiFonts.MenuFont) + 8)
            .DefaultIfEmpty(88).Max();
        var language = assets.GetText("en-us");
        DrawRowValue(MultiplayerItems, Items, "name", MpConfig.Name, column);
        DrawRowValue(MultiplayerItems, Items, "openport", (MpConfig.OpenPort ? "$STR_MP_ON" : "$STR_MP_OFF").ToLanguageText(language), column);
        DrawRowValue(MultiplayerItems, Items, "streamer", (MpConfig.StreamerMode ? "$STR_MP_ON" : "$STR_MP_OFF").ToLanguageText(language), column);
        draw.DrawMenuGun(MultiplayerItems);
        video.Update();
    }

    /// <summary>The Multiplayer menu's "Check Internet Port": can players on the internet reach a game hosted here?</summary>
    void CheckPort()
    {
        var language = assets.GetText("en-us");
        defs.DrawMenuComponents("multiplayer");
        draw.Message("$STR_MP_CHECKING".ToLanguageText(language).Replace("{PORT}", netStart.HostPort.ToString()));
        draw.MenuFadeIn();

        // Checking takes a few seconds (several servers, at worst): keep the window alive, Esc gives up
        using var cancel = new CancellationTokenSource();
        var check = Task.Run(() => CheckPortAsync(netStart.HostPort, cancel.Token));
        input.ClearKeysDown();
        while (!check.IsCompleted)
        {
            input.ProcessEvents();
            if (input.IsKeyDown(ScanCodes.Escape))
            {
                cancel.Cancel();
                input.ClearKeysDown();
                draw.MenuFadeOut();
                return;
            }
            GameEngineManager.DelayMs(10);
        }

        draw.MenuFadeOut();
        var (result, mapping) = check.Result;
        ShowNetMessage(MenuDrawing.WrapForMessage(NetAddresses.DescribePortCheck(result, mapping, netStart.HostPort, NetAddresses.LocalAddress())));
    }

    /// <summary>
    /// The check as hosting would find it: the router asked to open the port first (when that's
    /// on), then the port checked, then the router's forward taken down again
    /// </summary>
    async Task<(PortCheck.Result Result, PortMapper.Result? Mapping)> CheckPortAsync(int port, CancellationToken cancel)
    {
        try
        {
            var mapping = MpConfig.OpenPort ? await PortMapper.OpenAsync(port).ConfigureAwait(false) : null;
            var result = await PortCheck.RunAsync(port, cancel).ConfigureAwait(false);
            return (result, mapping);
        }
        finally
        {
            PortMapper.Close();
        }
    }

    /// <summary>`net_portcheck [port]`: can players on the internet reach a game hosted here?</summary>
    internal void NetPortCheck(string[] args)
    {
        int port = args.Length > 1 && int.TryParse(args[1], out var p) && p is > 0 and < 65536 ? p : netStart.HostPort;
        console.Print($"Checking UDP port {port} from the internet...");
        var (result, mapping) = CheckPortAsync(port, CancellationToken.None).GetAwaiter().GetResult();
        if (mapping != null)
            console.Print(Shown(NetAddresses.DescribePortMapping(mapping, NetAddresses.LocalAddress())));
        console.Print(Shown(NetAddresses.DescribePortCheck(result, mapping, port, NetAddresses.LocalAddress())));
        if (result.Server != null)
            console.Print($"  (checked with {result.Server}; the router sent us out on port {result.MappedPort})");
    }

    // The host name or address last typed in to join, for streamer mode to hide wherever it's shown
    string? typedHost;

    /// <summary>Text as it goes on screen or into the console: internet addresses hidden in streamer mode</summary>
    internal string Shown(string text) => NetAddresses.Hide(text, MpConfig.StreamerMode, typedHost);


    void EditName()
    {
        var language = assets.GetText("en-us");
        var name = AskLine("$STR_MP_ENTERNAME".ToLanguageText(language), MpConfig.Name, NetProtocol.MaxNameLength);
        if (name == null)
            return;
        MpConfig.Name = NetProtocol.CleanName(name);
        MpConfig.Write(MpConfigPath);
    }

    /// <summary>
    /// Asks for a line of text in a box over the screen: what was typed, or null if Esc was
    /// pressed (or nothing was typed); <paramref name="masked"/> shows * for what's typed
    /// </summary>
    string? AskLine(string prompt, string start, int maxChars, bool masked = false)
    {
        const int X = 32, Y = 80, W = 256, H = 40;
        draw.DrawWindow(X, Y, W, H, "BKGDCOLOR");
        draw.DrawOutline(X, Y, W, H, "DEACTIVE", "HIGHLIGHT");
        ui.TextAt(X + 8, Y + 6, new TextStyle(UiFonts.SmallFont, "READHCOLOR")).Print(prompt);
        video.Bar(X + 6, Y + 20, W - 12, 12, "BKGDCOLOR");
        video.Update();

        string typed = start;
        bool ok = lineInput.Run(X + 8, Y + 22, ref typed, start, true, maxChars, W - 20, new TextStyle(UiFonts.SmallFont, "HIGHLIGHT", "BKGDCOLOR"), masked);
        input.ClearKeysDown();
        menuInput.WaitKeyUp();
        return ok && typed.Trim().Length > 0 ? typed.Trim() : null;
    }



    /*
    ===============
    = Hosting
    ===============
    */

    internal void HostGame()
    {
        var language = assets.GetText("en-us");
        var episodes = PlayableEpisodes();
        var settings = new LobbySettings(GameMode.Coop, episodes.Count > 0 ? episodes[0] : 0,
            Math.Min(2, engine.GetGameInfo().Skills.Count - 1),     // the third skill, as the menus start on
            Map: ArenaMaps(GameMode.Deathmatch).FirstOrDefault() ?? "");

        var session = NetSession.Host(netStart.HostPort, hooks.Identity(), MpConfig.Name, hooks.PlayerClass(),
            LocalColor(), PlayerCommands.MaxPlayers, settings, out var error);
        if (session == null)
        {
            ShowNetMessage($"{"$STR_MP_HOSTFAILED".ToLanguageText(language)}\n\n{MenuDrawing.WrapForMessage(error ?? "")}");
            return;
        }

        hostLan = NetAddresses.WithPort(NetAddresses.LocalAddress(), session.Port);
        hostPublic = null;
        hostRouter = null;
        hostNote = null;
        publicAddressLookup = NetAddresses.LookUpPublicAddress();
        portMapping = MpConfig.OpenPort ? PortMapper.OpenAsync(session.Port) : null;
        if (portMapping == null)
            hostRouter = new PortMapper.Result(PortMapper.Outcome.NoRouter, session.Port, "off in the Multiplayer menu");
        console.Print($"LAN: others join at {hostLan}");
        if (hostRouter != null)
            console.Print(Shown(NetAddresses.DescribePortMapping(hostRouter, NetAddresses.LocalAddress())));
        UpdateHostNote(session);
        Lobby(session);
    }

    // Asking the router to open the port, while the lobby is up
    Task<PortMapper.Result>? portMapping;

    // Where others join a game hosted here, as found so far: the lobby has room for one line of
    // notes, so it's one line, filled in as the answers come back (the details go to the console)
    string hostLan = "";
    string? hostPublic, hostNote;
    PortMapper.Result? hostRouter;

    void UpdateHostNote(NetSession session)
    {
        var line = $"LAN {hostLan}";
        if (hostPublic != null)
            line += $", net {hostPublic}";
        if (hostRouter != null)
            line += !MpConfig.OpenPort ? " (UPnP off)"
                : hostRouter.Outcome == PortMapper.Outcome.Opened ? " (UPnP ok)"
                : " (no UPnP)";
        session.ReplaceNote(hostNote, line);
        hostNote = line;
    }


    // The host's internet address, looked up in the background while the lobby is up
    Task<string?>? publicAddressLookup;


    /*
    ===============
    = Joining
    ===============
    */

    ControlPanelItem[] JoinMenu = [];
    ControlPanelMenuInfo JoinItems = null!;
    LanBrowser? lanBrowser;

    // A row of the join screen: a game found, or typing in an address
    sealed record JoinRow(LanBrowser.FoundGame? Game);

    void JoinGame()
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
                (JoinMenu, JoinItems) = defs.LoadMenu("join-game", Math.Min(curpos, (short)0));
                JoinItems.CurPos = (short)Math.Clamp(curpos, 0, Math.Max(0, JoinMenu.Length - 1));
                if (JoinMenu[JoinItems.CurPos].Active == 0)
                    JoinItems.CurPos = (short)Math.Max(0, Array.FindIndex(JoinMenu, item => item.Active != 0));
                seen = browser.Changes;

                defs.DrawMenuComponents("join-game");
                draw.DrawMenu(JoinItems, JoinMenu);
                draw.DrawMenuGun(JoinItems);
                video.Update();
                if (fadeIn)
                {
                    draw.MenuFadeIn();
                    menuInput.WaitKeyUp();
                    fadeIn = false;
                }

                int which = loop.HandleMenu(JoinItems, JoinMenu, null, idle: () =>
                {
                    browser.Poll();
                    return browser.Changes != seen;     // the list changed: build it again
                });
                curpos = JoinItems.CurPos;

                if (which == -2)
                    continue;
                if (which < 0)
                    break;

                if (JoinMenu[which].Data is JoinRow { Game: { } game })
                    ConnectTo(game.Address);
                else
                {
                    var language = assets.GetText("en-us");
                    var typed = AskLine("$STR_MP_ENTERADDRESS".ToLanguageText(language), MpConfig.LastAddress, 40, masked: MpConfig.StreamerMode);
                    if (typed != null && NetAddresses.ParseAddress(typed) is { } at)
                    {
                        MpConfig.LastAddress = typed;
                        MpConfig.Write(MpConfigPath);
                        ConnectTo(at.Host, at.Port);
                    }
                }

                if (hooks.Started())
                    return;
                fadeIn = true;
            }

            draw.MenuFadeOut();
        }
        finally
        {
            lanBrowser = null;
        }
    }

    /// <summary>`net_find`: looks for games on the local network for two seconds and lists them</summary>
    internal void NetFind(string[] args)
    {
        using var browser = new LanBrowser();
        if (!browser.Started)
        {
            console.Print("Couldn't open a network connection to look with");
            return;
        }
        var until = Environment.TickCount64 + 2000;
        while (Environment.TickCount64 < until)
        {
            browser.Poll();
            Thread.Sleep(10);
        }

        console.Print(browser.Games.Count == 0 ? "No games found" : $"{browser.Games.Count} game(s) found:");
        foreach (var game in browser.Games)
            console.Print(Shown($"  {game.Info.HostName} at {game.Address}:{game.Info.GamePack}, {game.Info.Mode}, {game.Info.Map}, "
                + $"{game.Info.Players}/{game.Info.MaxPlayers}{(game.Info.InGame ? ", started" : "")}"));
    }

    /// <summary>The join screen's rows (its items-source): the games found, then typing an address</summary>
    internal ControlPanelItem[] BuildJoinRows()
    {
        var language = assets.GetText("en-us");
        var rows = new List<ControlPanelItem>();
        var games = lanBrowser?.Games ?? [];

        foreach (var game in games.Take(7))
        {
            var info = game.Info;
            var mode = info.Mode.LabelKey();
            var text = $"{info.HostName} {info.Players}/{info.MaxPlayers} {mode.ToLanguageText(language)}{(info.InGame ? " (playing)" : "")}, {info.Map}";
            bool canJoin = info.Players < info.MaxPlayers
                && string.Equals(info.GamePack, engine.GamePackId, StringComparison.OrdinalIgnoreCase);
            rows.Add(new ControlPanelItem((short)(canJoin ? 1 : 0), ui.FitText(text, 240, UiFonts.MenuFont), null, new JoinRow(game)));
        }

        if (games.Count == 0)
            rows.Add(new ControlPanelItem(0, "$STR_MP_LOOKING".ToLanguageText(language), null));
        rows.Add(new ControlPanelItem(1, "$STR_MP_ADDRESS".ToLanguageText(language), null, new JoinRow(null)));
        return rows.ToArray();
    }

    /// <summary>Joins the game at an address: waits to be let in, then its lobby</summary>
    internal void ConnectTo(string host, int port)
    {
        typedHost = host;
        ConnectTo(NetSession.Join(host, port, hooks.Identity(), MpConfig.Name, hooks.PlayerClass(), LocalColor(), out var error),
            error, NetAddresses.WithPort(host, port));
    }

    /// <summary>Joins a game found on the local network, at the address it answered from</summary>
    void ConnectTo(IPEndPoint at)
    {
        typedHost = null;
        ConnectTo(NetSession.Join(at, hooks.Identity(), MpConfig.Name, hooks.PlayerClass(), LocalColor(), out var error),
            error, NetAddresses.WithPort(at.Address.ToString(), at.Port));
    }

    /// <summary>The colors players pick from (game-info player-colors)</summary>
    List<string> PlayerColors() => engine.GetGameInfo().PlayerColors;

    /// <summary>The color this machine's player is seen in: multiplayer.cfg's, if the game offers it, else the game's first; empty for none</summary>
    string LocalColor()
    {
        var colors = PlayerColors();
        return colors.FirstOrDefault(c => c.Equals(MpConfig.Color, StringComparison.OrdinalIgnoreCase)) ?? colors.FirstOrDefault() ?? "";
    }

    /// <summary>A color's name for the lobby: "Red" for red; "-" for none</summary>
    string ColorLabel(string color) =>
        string.IsNullOrEmpty(color) ? "-" : char.ToUpperInvariant(color[0]) + color[1..].Replace('-', ' ');

    void ConnectTo(NetSession? session, string? error, string shown)
    {
        var language = assets.GetText("en-us");
        if (session == null)
        {
            ShowNetMessage(MenuDrawing.WrapForMessage(error ?? ""));
            return;
        }

        defs.DrawMenuComponents("join-game");
        draw.Message("$STR_MP_CONNECTING".ToLanguageText(language).Replace("{ADDRESS}", ui.FitText(Shown(shown), 200, UiFonts.SmallFont)));
        draw.MenuFadeIn();

        // LiteNetLib gives up on its own after its connect attempts; Esc gives up sooner
        input.ClearKeysDown();
        while (!session.IsJoined && session.Error == null)
        {
            session.Poll();
            input.ProcessEvents();
            if (input.IsKeyDown(ScanCodes.Escape))
            {
                input.ClearKeysDown();
                session.Dispose();
                draw.MenuFadeOut();
                return;
            }
            GameEngineManager.DelayMs(10);
        }

        if (session.Error != null)
        {
            session.Dispose();
            draw.MenuFadeOut();
            ShowNetMessage(MenuDrawing.WrapForMessage(session.Error));
            return;
        }

        draw.MenuFadeOut();
        Lobby(session);
    }

    /// <summary>A message over the menu background, until a key (internet addresses hidden in streamer mode)</summary>
    internal void ShowNetMessage(string text)
    {
        defs.DrawMenuComponents("multiplayer");
        draw.Message(Shown(text));
        draw.MenuFadeIn();
        input.ClearKeysDown();
        input.Ack();
        draw.MenuFadeOut();
    }

    /*
    ===============
    = The lobby
    ===============
    */

    ControlPanelItem[] LobbyMenu = [];
    ControlPanelMenuInfo LobbyItems = null!;

    // The lobby panel's rows: up to PlayerCommands.MaxPlayers players, then the last chat lines
    const int LobbyPanelX = 24, LobbyPanelY = 138, LobbyPanelRow = 9, LobbyChatLines = 1;

    /// <summary>
    /// Waits in a game's lobby: the host sets the game up and starts it once everyone's ready;
    /// the others pick a class, say they're ready, and wait. Leaves the session closed, unless
    /// the game started (then StartGame is set, for the menus to leave to it).
    /// </summary>
    void Lobby(NetSession session)
    {
        var language = assets.GetText("en-us");
        (LobbyMenu, LobbyItems) = defs.LoadMenu("lobby");
        if (MenuDefs.FindMenuItem(LobbyMenu, "go") is { } go)
            go.Text = (session.IsHost ? "$STR_MP_START" : "$STR_MP_READY").ToLanguageText(language);

        bool fadeIn = true;
        while (true)
        {
            // Turned away or left behind, or the game has started (the host's own Start included)
            if (session.Error != null)
            {
                draw.MenuFadeOut();
                session.Dispose();
                ShowNetMessage(MenuDrawing.WrapForMessage(session.Error));
                return;
            }

            // Joined a game already being played: take it up where the host has got to
            if (session.JoinState is { } joined)
            {
                draw.MenuFadeOut();
                if (hooks.BeginJoinedGame(session, joined) is { } problem)
                {
                    session.Dispose();
                    ShowNetMessage(MenuDrawing.WrapForMessage(problem));
                }
                return;
            }

            if (session.Started is { } started)
            {
                draw.MenuFadeOut();
                hooks.BeginNetGame(session, started);
                return;
            }

            // The host's internet address has come back: say where players outside the network join
            if (publicAddressLookup is { IsCompleted: true } lookup)
            {
                publicAddressLookup = null;
                if (lookup.Result is { } address)
                {
                    hostPublic = NetAddresses.WithPort(address, session.Port);
                    console.Print(Shown($"Internet: others join at {hostPublic}"));
                    UpdateHostNote(session);
                }
            }

            // The router has answered (or not) about opening the port
            if (portMapping is { IsCompleted: true } mapped)
            {
                portMapping = null;
                hostRouter = mapped.Result;
                console.Print(Shown(NetAddresses.DescribePortMapping(hostRouter, NetAddresses.LocalAddress())));
                UpdateHostNote(session);
            }

            int seen = session.Changes;
            DrawLobby(session);
            if (fadeIn)
            {
                draw.MenuFadeIn();
                menuInput.WaitKeyUp();
                fadeIn = false;
            }

            int which = loop.HandleMenu(LobbyItems, LobbyMenu, null, (w, delta) => StepLobbyChoice(session, w, delta),
                idle: () =>
                {
                    session.Poll();
                    return session.Changes != seen || publicAddressLookup is { IsCompleted: true }
                        || portMapping is { IsCompleted: true };
                });

            if (which == -2)
                continue;       // something changed: draw it again

            var id = which < 0 ? "leave" : MenuDefs.SelectedId(LobbyMenu, which);
            switch (id)
            {
                case "leave":
                    if (session.Players.Count > 1 && loop.Confirm("$STR_MP_LEAVECONFIRM".ToLanguageText(language)) == 0)
                        break;
                    draw.MenuFadeOut();
                    session.Dispose();
                    return;

                case "go":
                    if (session.IsHost)
                    {
                        if (session.CanStart)
                            session.StartGame((byte)game.Random.RndT());
                        else
                        {
                            draw.Message("$STR_MP_NOTREADY".ToLanguageText(language));
                            input.ClearKeysDown();
                            input.Ack();
                        }
                    }
                    else if (session.LocalPlayer is { } me)
                    {
                        loop.ShootSnd();
                        session.SetLocal(me.PlayerClass, me.Color, !me.Ready);
                    }
                    break;

                case "rules" when session.IsHost && session.Settings.Mode != GameMode.Coop:
                    draw.MenuFadeOut();
                    EditRules(session);
                    fadeIn = true;
                    break;

                case "copy" when session.IsHost:
                    CopyHostAddress();
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
                case "color":
                    StepLobbyChoice(session, which, 1);
                    break;
            }
        }
    }

    /// <summary>
    /// The lobby's "Copy Address": where others join (the internet address once it's known, else
    /// the LAN one) onto the clipboard, to paste into a private message. Streamer mode hides it in
    /// the message that says so too.
    /// </summary>
    void CopyHostAddress()
    {
        var language = assets.GetText("en-us");
        var address = hostPublic ?? hostLan;
        bool copied = address.Length > 0 && !address.StartsWith("port ") && SDL.SDL_SetClipboardText(address) == 0;
        draw.Message(copied
            ? "$STR_MP_COPIED".ToLanguageText(language).Replace("{ADDRESS}", Shown(address) == address ? address : "your address")
            : "$STR_MP_COPYFAILED".ToLanguageText(language));
        input.ClearKeysDown();
        input.Ack();
    }

    void DrawLobby(NetSession session)
    {
        var language = assets.GetText("en-us");
        var gameInfo = engine.GetGameInfo();
        var settings = session.Settings;
        var me = session.LocalPlayer;

        // Only the host changes the game; a class to pick only when there's more than one
        bool host = session.IsHost;
        bool arenas = PicksArena(settings);
        MenuDefs.FindMenuItem(LobbyMenu, "mode")?.Active = (short)(host ? 1 : 0);
        if (MenuDefs.FindMenuItem(LobbyMenu, "episode") is { } episodeRow)
        {
            // A deathmatch is played on an arena: the episode row picks it instead
            episodeRow.Text = (arenas ? "$STR_MP_MAP" : "$STR_MP_EPISODE").ToLanguageText(language);
            episodeRow.Active = (short)(host && (arenas ? ArenaMaps(settings.Mode).Count : PlayableEpisodes().Count) > 1 ? 1 : 0);
        }
        MenuDefs.FindMenuItem(LobbyMenu, "skill")?.Active = (short)(host ? 1 : 0);
        MenuDefs.FindMenuItem(LobbyMenu, "class")?.Active = (short)(hooks.PlayerClasses().Count > 1 ? 1 : 0);
        bool teams = settings.Mode.IsTeamGame();
        if (MenuDefs.FindMenuItem(LobbyMenu, "color") is { } colorRow)
        {
            // In a team game you're seen in your team's color: the color row picks the team instead
            colorRow.Text = (teams ? "$STR_MP_TEAM" : "$STR_MP_COLOR").ToLanguageText(language);
            colorRow.Active = (short)(teams || PlayerColors().Count > 1 ? 1 : 0);
        }
        MenuDefs.FindMenuItem(LobbyMenu, "rules")?.Active = (short)(host && settings.Mode != GameMode.Coop ? 1 : 0);
        MenuDefs.FindMenuItem(LobbyMenu, "copy")?.Active = (short)(host ? 1 : 0);
        if (LobbyMenu[LobbyItems.CurPos].Active == 0)
            LobbyItems.CurPos = (short)Math.Max(0, Array.FindIndex(LobbyMenu, item => item.Active != 0));

        defs.DrawMenuComponents("lobby");
        draw.DrawMenu(LobbyItems, LobbyMenu);

        DrawLobbyValue("mode", settings.Mode.LabelKey().ToLanguageText(language));
        DrawLobbyValue("episode", arenas ? MapLabel(settings.Map) : EpisodeLabel(settings.Episode));
        DrawLobbyValue("skill", gameInfo.Skills.Values.ElementAtOrDefault(settings.Skill)?.Name.ToLanguageText(language) ?? "?");
        DrawLobbyValue("class", me == null ? "" : PlayerClassLabel(me.PlayerClass));
        DrawLobbyValue("color", me == null ? "" : teams ? GameModes.TeamLabel(me.Team) : ColorLabel(me.Color));
        DrawLobbyValue("rules", RulesSummary(settings));
        if (!host)
            draw.DrawMenuCheckbox(LobbyItems, LobbyMenu, "go", me?.Ready == true);

        // Who's in, and the last thing or two said
        var panel = new TextStyle(UiFonts.SmallFont, "TEXTCOLOR");
        int y = LobbyPanelY;
        for (int slot = 0; slot < PlayerCommands.MaxPlayers; slot++)
        {
            var p = session.Players.FirstOrDefault(pl => pl.Slot == slot);
            string text = p == null ? "-"
                : $"{p.Name}{(slot == 0 ? " (host)" : "")}  {PlayerClassLabel(p.PlayerClass)}"
                  + (teams ? $" ({GameModes.TeamLabel(p.Team)})" : string.IsNullOrEmpty(p.Color) ? "" : $" ({ColorLabel(p.Color)})")
                  + $"  {(slot == 0 ? "" : p.Ready ? "ready" : "not ready")}";
            var style = p != null && p.Slot == session.LocalSlot ? new TextStyle(UiFonts.SmallFont, "HIGHLIGHT") : panel;
            ui.TextAt(LobbyPanelX, y, style).Print(ui.FitText($"{slot + 1}. {text}", 272, UiFonts.SmallFont));
            y += LobbyPanelRow;
        }

        foreach (var line in session.Chat.TakeLast(LobbyChatLines))
        {
            ui.TextAt(LobbyPanelX, y, new TextStyle(UiFonts.SmallFont, "READCOLOR")).Print(ui.FitText(Shown(line), 272, UiFonts.SmallFont));
            y += LobbyPanelRow;
        }

        session.MapLabel = arenas ? MapLabel(settings.Map) : EpisodeLabel(settings.Episode);
        draw.DrawMenuGun(LobbyItems);
        video.Update();
    }

    // A lobby row's value, to the right of its name (cleared first)
    void DrawLobbyValue(string id, string value) => DrawRowValue(LobbyItems, LobbyMenu, id, value);

    // A row's value in the small font, to the right of its name (cleared first): the lobby's and
    // the Multiplayer menu's, which have room for more than a menu-font choice column
    void DrawRowValue(ControlPanelMenuInfo info, ControlPanelItem[] items, string id, string value, int column = 88)
    {
        int index = Array.FindIndex(items, item => item.Id == id);
        if (index < 0)
            return;
        int x = info.X + info.Indent + column;
        int y = info.Y + index * info.RowHeight;
        video.Bar(x, y, 300 - x, info.RowHeight, "BKGDCOLOR");
        ui.TextAt(x, y + 2, new TextStyle(UiFonts.SmallFont, MenuDrawing.MenuItemColor(items[index], false))).Print(ui.FitText(value, 300 - x, UiFonts.SmallFont));
    }

    /// <summary>A match's rules in a few words, for the lobby: "20 frags, 10 min, monsters"</summary>
    string RulesSummary(LobbySettings settings)
    {
        if (settings.Mode == GameMode.Coop)
            return "-";
        var what = settings.Mode.ScoreName().ToLowerInvariant();
        var parts = new List<string>
        {
            settings.ScoreLimit > 0
                ? settings.Mode == GameMode.Heist ? $"lead by {settings.ScoreLimit}" : $"{settings.ScoreLimit} {what}"
                : settings.Mode == GameMode.Horde ? "endless" : $"no {what} limit",
        };
        if (settings.Mode == GameMode.PropHunt)
            parts.Add($"{settings.HideSeconds}s/{settings.HuntMinutes}m");     // hide, hunt: the lobby's row is short
        if (settings.TimeLimit > 0 && settings.Mode != GameMode.Horde)
            parts.Add($"{settings.TimeLimit} min");
        if (settings.Monsters)
            parts.Add("monsters");
        return string.Join(", ", parts);
    }

    // What the rules screen's limit row is called, for the mode
    static string ScoreLimitKey(GameMode mode) => mode switch
    {
        GameMode.Deathmatch or GameMode.TeamDeathmatch => "$STR_MP_FRAGLIMIT",
        GameMode.CaptureTheFlag => "$STR_MP_CAPTURELIMIT",
        GameMode.Heist => "$STR_MP_LOOTLIMIT",
        GameMode.Domination or GameMode.KingOfTheHill => "$STR_MP_POINTLIMIT",
        GameMode.Horde => "$STR_MP_WAVELIMIT",
        GameMode.PropHunt => "$STR_MP_ROUNDLIMIT",
        _ => "$STR_MP_SCORELIMIT",
    };

    readonly int[] TimeLimits = [0, 5, 10, 15, 20, 30, 45, 60];
    readonly int[] HideTimes = [10, 15, 20, 30, 45, 60];       // Prop Hunt, seconds
    readonly int[] HuntTimes = [1, 2, 3, 4, 5, 7, 10];         // and minutes
    ControlPanelItem[] RulesMenu = [];
    ControlPanelMenuInfo RulesItems = null!;

    /// <summary>The host sets a deathmatch's rules (left/right, or picking them; Esc goes back)</summary>
    void EditRules(NetSession session)
    {
        (RulesMenu, RulesItems) = defs.LoadMenu("net-rules");
        DrawRules(session);
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();

        int which;
        do
        {
            int seen = session.Changes;
            which = loop.HandleMenu(RulesItems, RulesMenu, null, (w, delta) => StepRule(session, w, delta),
                idle: () =>
                {
                    session.Poll();         // others can still join meanwhile
                    return false;
                });
            if (which >= 0)
                StepRule(session, which, 1);
        }
        while (which >= 0);

        draw.MenuFadeOut();
    }

    void StepRule(NetSession session, int which, int delta)
    {
        var settings = session.Settings;
        RulesItems.CurPos = (short)which;
        settings = MenuDefs.SelectedId(RulesMenu, which) switch
        {
            "fraglimit" => settings with { ScoreLimit = StepLimit(settings.Mode.ScoreLimits(), settings.ScoreLimit, delta) },
            "timelimit" => settings with { TimeLimit = TimeLimits[MenuDrawing.StepMenuChoice(Math.Max(0, Array.IndexOf(TimeLimits, settings.TimeLimit)), TimeLimits.Length, delta, wrap: true)] },
            "monsters" => settings with { Monsters = !settings.Monsters },
            "itemrespawn" => settings with { ItemRespawn = !settings.ItemRespawn },
            "hidetime" => settings with { HideSeconds = StepLimit(HideTimes, settings.HideSeconds, delta) },
            "hunttime" => settings with { HuntMinutes = StepLimit(HuntTimes, settings.HuntMinutes, delta) },
            _ => settings,
        };
        if (settings == session.Settings)
            return;
        session.SetSettings(settings);
        audio.Play("menu/move1");
        DrawRules(session);
    }

    void DrawRules(NetSession session)
    {
        var language = assets.GetText("en-us");
        var settings = session.Settings;
        defs.DrawMenuComponents("net-rules");
        MenuDefs.FindMenuItem(RulesMenu, "fraglimit")?.Text = ScoreLimitKey(settings.Mode).ToLanguageText(language);
        MenuDefs.FindMenuItem(RulesMenu, "timelimit")?.Active = (short)(settings.Mode == GameMode.Horde ? 0 : 1);
        MenuDefs.FindMenuItem(RulesMenu, "hidetime")?.Active = (short)(settings.Mode == GameMode.PropHunt ? 1 : 0);
        MenuDefs.FindMenuItem(RulesMenu, "hunttime")?.Active = (short)(settings.Mode == GameMode.PropHunt ? 1 : 0);
        draw.DrawMenu(RulesItems, RulesMenu);
        var none = "$STR_MP_NONE".ToLanguageText(language);
        draw.DrawMenuChoice(RulesItems, RulesMenu, "fraglimit", settings.ScoreLimit > 0 ? settings.ScoreLimit.ToString() : none);
        draw.DrawMenuChoice(RulesItems, RulesMenu, "timelimit", settings.TimeLimit > 0 ? $"{settings.TimeLimit} min" : none);
        draw.DrawMenuCheckbox(RulesItems, RulesMenu, "monsters", settings.Monsters);
        draw.DrawMenuCheckbox(RulesItems, RulesMenu, "itemrespawn", settings.ItemRespawn);
        if (settings.Mode == GameMode.PropHunt)
        {
            draw.DrawMenuChoice(RulesItems, RulesMenu, "hidetime", $"{settings.HideSeconds} s");
            draw.DrawMenuChoice(RulesItems, RulesMenu, "hunttime", $"{settings.HuntMinutes} min");
        }
        draw.DrawMenuGun(RulesItems);
        video.Update();
    }

    /// <summary>Steps a lobby setting (left/right, or picking it)</summary>
    void StepLobbyChoice(NetSession session, int which, int delta)
    {
        var id = MenuDefs.SelectedId(LobbyMenu, which);
        var settings = session.Settings;
        LobbyItems.CurPos = (short)which;

        switch (id)
        {
            case "mode" when session.IsHost:
                // The next mode, with its own score limit, enemies and arena (the one picked, if it's one for it too)
                var modes = GameModes.NetModesFor(engine.GetGameInfo().Horde);
                var mode = modes[MenuDrawing.StepMenuChoice(Math.Max(0, Array.IndexOf(modes, settings.Mode)), modes.Length, delta, wrap: true)];
                var modeArenas = ArenaMaps(mode);
                session.SetSettings(settings with
                {
                    Mode = mode,
                    ScoreLimit = mode.DefaultScoreLimit(),
                    Monsters = mode.DefaultMonsters(),
                    Map = modeArenas.Contains(settings.Map, StringComparer.OrdinalIgnoreCase) ? settings.Map : modeArenas.FirstOrDefault() ?? "",
                });
                break;

            case "episode" when session.IsHost && PicksArena(settings):
                var arenas = ArenaMaps(settings.Mode);
                int arena = Math.Max(0, arenas.FindIndex(m => string.Equals(m, settings.Map, StringComparison.OrdinalIgnoreCase)));
                session.SetSettings(settings with { Map = arenas[MenuDrawing.StepMenuChoice(arena, arenas.Count, delta, wrap: true)] });
                break;

            case "episode" when session.IsHost:
                var episodes = PlayableEpisodes();
                if (episodes.Count == 0)
                    return;
                int at = Math.Max(0, episodes.IndexOf(settings.Episode));
                session.SetSettings(settings with { Episode = episodes[MenuDrawing.StepMenuChoice(at, episodes.Count, delta, wrap: true)] });
                break;

            case "skill" when session.IsHost:
                int skills = engine.GetGameInfo().Skills.Count;
                session.SetSettings(settings with { Skill = MenuDrawing.StepMenuChoice(settings.Skill, skills, delta, wrap: true) });
                break;

            case "class" when session.LocalPlayer is { } me:
                var classes = hooks.PlayerClasses();
                int current = Math.Max(0, classes.FindIndex(c => string.Equals(c, me.PlayerClass, StringComparison.OrdinalIgnoreCase)));
                var next = classes[MenuDrawing.StepMenuChoice(current, classes.Count, delta, wrap: true)];
                hooks.ClassChosen(next);        // the next game played alone starts as it too
                session.SetLocal(next, me.Color, me.Ready);
                break;

            case "color" when settings.Mode.IsTeamGame() && session.LocalPlayer is { } me:
                session.SetLocal(me.PlayerClass, me.Color, me.Ready, 1 - (me.Team & 1));      // the other team
                break;

            case "color" when session.LocalPlayer is { } me && PlayerColors() is { Count: > 1 } colors:
                int color = Math.Max(0, colors.FindIndex(c => string.Equals(c, me.Color, StringComparison.OrdinalIgnoreCase)));
                var picked = colors[MenuDrawing.StepMenuChoice(color, colors.Count, delta, wrap: true)];
                MpConfig.Color = picked;        // remembered for the next game (multiplayer.cfg)
                MpConfig.Write(MpConfigPath);
                session.SetLocal(me.PlayerClass, picked, me.Ready);
                break;

            default:
                return;
        }
        audio.Play("menu/move1");
    }

    /// <summary>
    /// The episodes a game can start on: their places in game-info's, leaving out locked ones (the
    /// shareware's, and starts-locked ones the host hasn't unlocked)
    /// </summary>
    List<int> PlayableEpisodes() =>
        engine.GetGameInfo().Episodes.Values
            .Select((episode, i) => (episode, i))
            .Where(e => !e.episode.Locked && hooks.EpisodeOpen?.Invoke(e.episode) != false
                && engine.GetGameInfo().Maps.ContainsKey(e.episode.StartMap))
            .Select(e => e.i)
            .ToList();

    /// <summary>The arenas for a mode (game-info maps with `deathmatch: true` or the mode in their `modes`), in game-info's order</summary>
    List<string> ArenaMaps(GameMode mode) =>
        engine.GetGameInfo().Maps
            .Where(m => m.Value.IsArenaFor(mode.Id()))
            .Select(m => m.Key)
            .ToList();

    /// <summary>
    /// The game is played on an arena the host picks: its game has arenas for the mode. Without
    /// any, it starts on an episode, as a co-op game does.
    /// </summary>
    bool PicksArena(LobbySettings settings) =>
        settings.Mode != GameMode.Coop && ArenaMaps(settings.Mode).Count > 0;

    // The next (or last) of a list of limits from where one is
    static int StepLimit(int[] limits, int current, int delta)
    {
        int at = Array.IndexOf(limits, current);
        if (at < 0)
            at = Math.Max(0, Array.FindLastIndex(limits, l => l < current));
        return limits[MenuDrawing.StepMenuChoice(at, limits.Length, delta, wrap: true)];
    }
    /// <summary>An arena's name for the lobby and the LAN list: its game-info name, else its map name</summary>
    string MapLabel(string map)
    {
        var language = assets.GetText("en-us");
        return engine.GetGameInfo().Maps.TryGetValue(map, out var info) && !string.IsNullOrWhiteSpace(info.Name)
            ? info.Name.ToLanguageText(language)
            : string.IsNullOrEmpty(map) ? "?" : map;
    }

    string EpisodeLabel(int episode)
    {
        var language = assets.GetText("en-us");
        var info = engine.GetGameInfo().Episodes.Values.ElementAtOrDefault(episode);
        return info == null ? "?" : info.Name.ToLanguageText(language).Replace('\n', ' ');
    }

    string PlayerClassLabel(string playerClass)
    {
        var language = assets.GetText("en-us");
        var info = engine.GetGameInfo().PlayerClasses
            .FirstOrDefault(c => string.Equals(c.Key, playerClass, StringComparison.OrdinalIgnoreCase)).Value;
        return (info?.Name ?? inventory.GetStringProperty(playerClass, "tag") ?? playerClass).ToLanguageText(language);
    }
}
