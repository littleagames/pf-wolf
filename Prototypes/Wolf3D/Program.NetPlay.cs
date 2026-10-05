using Wolf3D.Extensions;
using Wolf3D.Networking;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                PLAYING WITH OTHERS

    Every machine plays the whole game, from everyone's controls. A frame (a step) goes:
    this machine's controls (PollControls' localcmd) are sent for the step NETDELAY ahead; the
    host gathers everyone's for each step and sends them out together; and once this step's
    are here, every player gets theirs (TicBundle) and the actors think. As long as each
    machine starts the same and plays the same controls through the same code, they all
    come out the same, so only the controls go over the network. Frames are NETTICS tics long,
    always, so the game doesn't depend on how fast each machine is.

    Nothing local may change the game: no pausing, no cheats, no menus over it (Esc asks to
    leave instead), and anything the game decides from what a screen shows is worked out from
    the players' own positions instead (InViewOf). To catch it if it ever happens anyway, each
    machine sums up the game every so often and the host compares (GameChecksum).

    =============================================================================
    */

    /// <summary>Tics a frame plays, with others: always the same, on every machine</summary>
    internal const uint NETTICS = 2;

    /// <summary>
    /// How many frames ahead a machine sends its controls: the time they have to reach the host
    /// and come back before they're needed, at the cost of that much delay before they act
    /// </summary>
    const int NETDELAY = 2;

    /// <summary>How often (frames) the game is summed up to check every machine still agrees</summary>
    const int NETCHECKEVERY = 35;

    // The level being played, counted from 1 on every machine alike (each PlayLoop), and its frame
    static int netlevel, netstep;

    // Esc pressed: asking whether to leave. And whether this machine has left the game
    static bool netLeavePrompt, netLeft;

    // The waiting-for message, while a step's controls are late
    static string? netWaiting;

    /// <summary>Starts counting frames for a new level, sending empty controls for the frames before ours can arrive</summary>
    internal static void NetLevelStart()
    {
        netLeavePrompt = false;
        netWaiting = null;
        pendingjoinstate = null;        // (someone who came in on a level's last frame is left waiting)
        if (NetSession.Current is not { } session)
            return;

        // Just joined: the game taken up carries on from the frame after the host's (BeginJoinedGame)
        if (netJoinResume)
        {
            netJoinResume = false;
            for (int step = netstep; step < netstep + NETDELAY; step++)
                session.SubmitLocal(netlevel, step, default);
            return;
        }

        netlevel++;
        netstep = 0;
        session.ForgetBefore(netlevel);
        for (int step = 0; step < NETDELAY; step++)
            session.SubmitLocal(netlevel, step, default);
    }

    /// <summary>
    /// This frame's controls for everyone: sends ours, waits for everyone's, and gives each
    /// player theirs. False when the game with others is over for this machine (it left, or
    /// lost the host): the level then ends.
    /// </summary>
    internal static bool NetFrame()
    {
        if (NetSession.Current is not { } session || session.Error != null || netLeft)
            return false;

        // A player who came in at the frame just played gets the game as it now stands
        session.Poll();
        if (pendingjoinstate is { } joined)
        {
            session.SendJoinState(joined.Index, netlevel, joined.Step, WriteNetState());
            pendingjoinstate = null;
        }

        // While asking about leaving, this player stands still
        session.SubmitLocal(netlevel, netstep + NETDELAY, netLeavePrompt ? default : localcmd);

        TicBundle? bundle;
        long waitStart = Environment.TickCount64;
        while ((bundle = session.TryTake(netlevel, netstep)) == null)
        {
            session.Poll();
            if (session.Error != null)
                return false;

            // Late: say who for, and let Esc give up on them
            if (Environment.TickCount64 - waitStart > 500)
            {
                var waiting = $"Waiting for {string.Join(", ", session.WaitingFor(netlevel, netstep))}...";
                if (waiting != netWaiting)
                {
                    netWaiting = waiting;
                    ThreeDRefresh();
                }
                _inputManager.ProcessEvents();
                if (_inputManager.IsKeyDown(ScanCodes.sc_Escape))
                {
                    _inputManager.ClearKeysDown();
                    netLeft = true;
                    return false;
                }
            }
            GameEngineManager.DelayMs(1);
        }

        if (netWaiting != null)
        {
            netWaiting = null;
            lasttimecount = (int)GameEngineManager.GetTimeCount();     // no rush to catch up on the wait
        }

        // Anyone leaving leaves, and anyone joining joins, at this step on every machine
        for (int i = 0; i < players.Count; i++)
        {
            if (bundle.HasLeft(i) && !players[i].Gone)
                RemovePlayer(players[i]);
        }
        if (bundle.Join is { } join)
        {
            AddNetPlayer(join);
            if (session.IsHost)
                pendingjoinstate = (join.Index, netstep);     // the game goes to them once this frame's played
        }
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            p.Input.Begin(!p.Gone && i < bundle.Cmds.Length ? bundle.Cmds[i] : default);
        }

        netstep++;
        return true;
    }

    /// <summary>After a frame has played: checks now and then that every machine still agrees, and shows what the host has to say</summary>
    internal static void NetFrameDone()
    {
        if (NetSession.Current is not { } session)
            return;

        if (netstep % NETCHECKEVERY == 0)
            session.ReportChecksum(netlevel, netstep, GameChecksum());

        while (session.Notices.TryDequeue(out var line))
        {
            Console.WriteLine($"Network: {line}");
            _hudMessageManager.Show(Managers.HudMessageKind.Other, line);
        }
    }

    /// <summary>A player has left: out of the level, and out of every level after</summary>
    static void RemovePlayer(Entities.PlayerState p)
    {
        p.Gone = true;
        if (p.Pawn is { } pawn)
        {
            _mapManager.MarkForRemoval(pawn);      // dropped from the actors as they next think
            p.Pawn = null;
            if (camera.Target == pawn)
                camera.FollowPlayer();
            ConnectAreas();
        }
    }

    /// <summary>
    /// The game as it stands, summed up: the random numbers' place, every actor's class, place,
    /// health and state, and every player's stats. Machines playing the same game get the same.
    /// </summary>
    static uint GameChecksum()
    {
        uint hash = 2166136261;     // FNV-1a
        void Add(int value)
        {
            for (int i = 0; i < 4; i++, value >>= 8)
                hash = (hash ^ (byte)value) * 16777619;
        }
        void AddText(string? text)
        {
            foreach (var c in text ?? "")
                hash = (hash ^ c) * 16777619;
        }

        Add(rndindex);
        foreach (var actor in _mapManager.GetActors())
        {
            if (actor.IsRemoved)
                continue;
            AddText(actor.Name);
            Add(actor.X);
            Add(actor.Y);
            Add(actor.Angle);
            Add(actor.Hitpoints);
            Add(actor.TicCount);
            AddText(actor.CurrentState?.StateName);
        }
        foreach (var p in players)
        {
            Add(p.health);
            Add(p.score);
            Add(p.armor);
            AddText(p.weapon);
        }
        for (int door = 0; door < doornum; door++)
            Add(doorobjlist[door].position);
        Add(pwallstate);
        return hash;
    }

    /// <summary>`net_status`: the game with others as this machine has it</summary>
    static void Cmd_NetStatus(string[] args)
    {
        if (!netgame || NetSession.Current is not { } session)
        {
            _consoleManager.Print("Not playing with others");
            return;
        }

        var lines = new List<string> { $"{(session.IsHost ? "Hosting" : "Joined")}, level {netlevel}, frame {netstep}, game sum {GameChecksum():x8}" };
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            lines.Add($"  {i + 1}. {p.Name}{(i == consoleplayer ? " (you)" : "")}: "
                + (p.Pawn is { } pawn ? $"{pawn.X},{pawn.Y} facing {pawn.Angle}, health {p.health}" : p.Gone ? "left" : "-"));
        }
        if (session.IsHost)
            lines.Add($"  {session.SumsMatched} of {session.SumsCompared} checks matched");

        foreach (var line in lines)
        {
            _consoleManager.Print(line);
            Console.WriteLine($"net_status: {line}");
        }
    }

    // Typing a line to say to the others (T), and what's typed so far
    static bool netChatting;
    static string netChatText = "";

    /// <summary>While typing a chat line: letters go on it, Backspace takes one off, Enter says it, Esc drops it</summary>
    static void NetChatKeys()
    {
        foreach (var c in _inputManager.GetTextInput())
        {
            if (c == 0)
                break;
            if (c >= ' ' && c < 127 && netChatText.Length < NetProtocol.MaxChatLength)
                netChatText += c;
        }
        _inputManager.ClearTextInput();

        while (_inputManager.TryTakePressedKey(out var key))
        {
            switch (key)
            {
                case ScanCodes.sc_Enter:
                    NetSession.Current?.Say(netChatText);
                    goto case ScanCodes.sc_Escape;
                case ScanCodes.sc_Escape:
                    netChatting = false;
                    netChatText = "";
                    _inputManager.ClearKeysDown();
                    return;
                case ScanCodes.sc_BackSpace when netChatText.Length > 0:
                    netChatText = netChatText[..^1];
                    break;
            }
        }
    }

    /// <summary>Dead, with others: watches the next (or previous) living player, until coming back</summary>
    static void WatchNextPlayer(int step)
    {
        var living = _mapManager.Players.Where(pawn => pawn.State != localplayer && pawn.State.health > 0).ToList();
        if (living.Count == 0)
        {
            camera.FollowPlayer();
            return;
        }
        int at = camera.Target is Entities.Actors.PlayerPawn watched ? living.IndexOf(watched) : -1;
        camera.Follow(living[((at < 0 ? (step > 0 ? -1 : 0) : at) + step + living.Count) % living.Count]);
    }

    /// <summary>
    /// The keys, with others: Esc asks whether to leave (then Y leaves, N or Esc plays on), T
    /// starts a line to say, and while dead, turning left or right watches another player
    /// </summary>
    static void NetCheckKeys(ScanCodes scan)
    {
        if (!netLeavePrompt && scan == ScanCodes.sc_T)
        {
            netChatting = true;
            netChatText = "";
            _inputManager.ClearTextInput();
            _inputManager.ClearKeysDown();
            return;
        }

        if (localplayer.health <= 0 && localplayer.Pawn != null)
        {
            if (_inputManager.IsButtonPressed(buttontypes.bt_turnright) && !_inputManager.IsButtonHeld(buttontypes.bt_turnright))
                WatchNextPlayer(1);
            else if (_inputManager.IsButtonPressed(buttontypes.bt_turnleft) && !_inputManager.IsButtonHeld(buttontypes.bt_turnleft))
                WatchNextPlayer(-1);
        }

        if (netLeavePrompt)
        {
            if (scan == ScanCodes.sc_Y)
            {
                netLeft = true;
                playstate = playstatetypes.ex_abort;
            }
            if (scan is ScanCodes.sc_Y or ScanCodes.sc_N or ScanCodes.sc_Escape)
            {
                netLeavePrompt = false;
                _inputManager.ClearKeysDown();
            }
            return;
        }

        if (scan == ScanCodes.sc_Escape || _inputManager.IsButtonPressed(buttontypes.bt_esc))
        {
            netLeavePrompt = true;
            _inputManager.ClearKeysDown();
        }
    }

    /// <summary>Over the view, with others: who the game is waiting for, or whether to leave</summary>
    static void DrawNetOverlay()
    {
        if (!netgame)
            return;

        // Dead: everyone's score, who's being watched, and how to come back
        bool dead = localplayer.health <= 0 && localplayer.Pawn != null;
        if (showscoreboard || dead)
            DrawScoreboard(12);
        if (dead)
        {
            var watching = camera.Target is Entities.Actors.PlayerPawn pawn && pawn != localplayer.Pawn
                ? $"Watching {pawn.State.Name ?? $"Player {pawn.State.Number + 1}"} - " : "";
            var hint = $"{watching}use or fire to come back, left/right to watch others";
            CenteredText(0, 320, MAXY - 12, new Fonts.TextStyle(SMALL_FONT, "HIGHLIGHT")).CPrint(FitText(hint, 300, SMALL_FONT));
        }

        // The line being typed to the others
        if (netChatting)
        {
            _videoManager.Bar(16, MAXY - 24, 288, 11, "BKGDCOLOR");
            TextAt(20, MAXY - 22, new Fonts.TextStyle(SMALL_FONT, "HIGHLIGHT"))
                .Print(FitText($"Say: {netChatText}_", 280, SMALL_FONT));
        }

        var text = netLeavePrompt ? "Leave this game?\n(Y or N)" : netWaiting;
        if (text != null)
            Message(text, MAXY);
    }

    /// <summary>Whether the game with others is over for this machine: it left, or lost the host</summary>
    internal static bool NetGameOver => netgame && (netLeft || NetSession.Current is not { Error: null });

    /// <summary>A random number for something only this machine shows (a quit message), not the game's own</summary>
    internal static int UiRandom() => netgame ? Random.Shared.Next(256) : US_RndT();

    /// <summary>
    /// A random number for which of a sound's variants plays: the game's own alone, as it always
    /// was, but not with others (one machine may have its sound off, and play none)
    /// </summary>
    internal static int SoundRandom() => gamemode == GameMode.Single ? US_RndT() : Random.Shared.Next(256);
}
