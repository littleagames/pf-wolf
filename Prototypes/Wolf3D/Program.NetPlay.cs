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
        netlevel++;
        netstep = 0;
        netLeavePrompt = false;
        netWaiting = null;
        if (NetSession.Current is not { } session)
            return;

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

        // Anyone gone leaves at this step on every machine
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (!bundle.IsPresent(i) && !p.Gone)
                RemovePlayer(p);
            p.Input.Begin(bundle.IsPresent(i) && i < bundle.Cmds.Length ? bundle.Cmds[i] : default);
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

    /// <summary>Esc while playing with others: asks whether to leave (Y), or plays on (N or Esc)</summary>
    static void NetCheckKeys(ScanCodes scan)
    {
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
