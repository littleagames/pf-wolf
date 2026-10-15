using PFWolf.Managers;

namespace PFWolf.Networking;

/// <summary>What playing with others needs from the game</summary>
/// <param name="LocalCmd">This machine's controls for this frame (PollControls' localcmd)</param>
/// <param name="WriteNetState">The game as it stands, for a player who's just joined (Program.NetJoin.cs)</param>
/// <param name="ThreeDRefresh">Draws the view, with the waiting-for message over it</param>
/// <param name="ResetLastTimeCount">After a wait: no rush to catch up on it</param>
/// <param name="AddNetPlayer">A player joins at this frame (Gameplay/Players.cs)</param>
/// <param name="ConnectAreas">Works out again which areas join the players' (one has left)</param>
/// <param name="NetGame">Whether this is a game with others</param>
internal sealed record NetPlayHooks(
    Func<Entities.TicCmd> LocalCmd,
    Func<byte[]> WriteNetState,
    Action ThreeDRefresh,
    Action ResetLastTimeCount,
    Action<NetJoin> AddNetPlayer,
    Action ConnectAreas,
    Func<bool> NetGame);

/// <summary>
/// Every machine plays the whole game, from everyone's controls. A frame (a step) goes:
/// this machine's controls (PollControls' localcmd) are sent for the step NETDELAY ahead; the
/// host gathers everyone's for each step and sends them out together; and once this step's
/// are here, every player gets theirs (TicBundle) and the actors think. As long as each
/// machine starts the same and plays the same controls through the same code, they all
/// come out the same, so only the controls go over the network. Frames are NETTICS tics long,
/// always, so the game doesn't depend on how fast each machine is.
///
/// Nothing local may change the game: no pausing, no cheats, no menus over it (Esc asks to
/// leave instead), and anything the game decides from what a screen shows is worked out from
/// the players' own positions instead (InViewOf). To catch it if it ever happens anyway, each
/// machine sums up the game every so often and the host compares (GameChecksum).
/// </summary>
/// <param name="game">The game being played (`session` is the network one here)</param>
internal sealed class NetPlay(
    MapManager map,
    VideoManager video,
    InputManager input,
    HudMessageManager hudMessages,
    Doors doors,
    PushWalls pushWalls,
    GameSession game,
    NetPlayHooks hooks)
{
    /// <summary>Tics a frame plays, with others: always the same, on every machine</summary>
    internal const uint NETTICS = 2;

    /// <summary>
    /// How many frames ahead a machine sends its controls: the time they have to reach the host
    /// and come back before they're needed, at the cost of that much delay before they act
    /// </summary>
    internal const int NETDELAY = 2;

    /// <summary>How often (frames) the game is summed up to check every machine still agrees</summary>
    internal const int NETCHECKEVERY = 35;

    // The level being played, counted from 1 on every machine alike (each PlayLoop), and its frame
    internal int netlevel, netstep;

    // Esc pressed: asking whether to leave. And whether this machine has left the game
    internal bool netLeavePrompt, netLeft;

    // The waiting-for message, while a step's controls are late
    internal string? netWaiting;

    // Host: a player who came in at the frame just played, to send the game to before the next
    internal (int Index, int Step)? pendingjoinstate;

    // A player joining: the game they've just taken up carries on, rather than a level starting
    internal bool netJoinResume;

    /// <summary>Starts counting frames for a new level, sending empty controls for the frames before ours can arrive</summary>
    internal void NetLevelStart()
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
    internal bool NetFrame()
    {
        if (NetSession.Current is not { } session || session.Error != null || netLeft)
            return false;

        // A player who came in at the frame just played gets the game as it now stands
        session.Poll();
        if (pendingjoinstate is { } joined)
        {
            session.SendJoinState(joined.Index, netlevel, joined.Step, hooks.WriteNetState());
            pendingjoinstate = null;
        }

        // While asking about leaving, this player stands still
        session.SubmitLocal(netlevel, netstep + NETDELAY, netLeavePrompt ? default : hooks.LocalCmd());

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
                if (waiting != netWaiting && !session.HasBundle(netlevel, netstep))
                {
                    netWaiting = waiting;
                    hooks.ThreeDRefresh();
                    if (video.screenfaded)
                        video.FadeIn();     // a level's first frame: others are still on the intermission
                }
                input.ProcessEvents();
                if (input.IsKeyDown(ScanCodes.sc_Escape))
                {
                    input.ClearKeysDown();
                    netLeft = true;
                    return false;
                }
            }
            GameEngineManager.DelayMs(1);
        }

        if (netWaiting != null)
        {
            netWaiting = null;
            hooks.ResetLastTimeCount();     // no rush to catch up on the wait
        }

        // Anyone leaving leaves, and anyone joining joins, at this step on every machine
        var players = game.players;
        for (int i = 0; i < players.Count; i++)
        {
            if (bundle.HasLeft(i) && !players[i].Gone)
                RemovePlayer(players[i]);
        }
        if (bundle.Join is { } join)
        {
            hooks.AddNetPlayer(join);
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
    internal void NetFrameDone()
    {
        if (NetSession.Current is not { } session)
            return;

        if (netstep % NETCHECKEVERY == 0)
            session.ReportChecksum(netlevel, netstep, GameChecksum());

        while (session.Notices.TryDequeue(out var line))
        {
            Console.WriteLine($"Network: {line}");
            hudMessages.Show(HudMessageKind.Other, line);
        }
    }

    /// <summary>A player has left: out of the level, and out of every level after</summary>
    internal void RemovePlayer(Entities.PlayerState p)
    {
        p.Gone = true;
        if (p.Pawn is { } pawn)
        {
            map.MarkForRemoval(pawn);      // dropped from the actors as they next think
            p.Pawn = null;
            if (game.camera.Target == pawn)
                game.camera.FollowPlayer();
            hooks.ConnectAreas();
        }
    }

    /// <summary>
    /// The game as it stands, summed up: the random numbers' place, every actor's class, place,
    /// health and state, and every player's stats. Machines playing the same game get the same.
    /// </summary>
    internal uint GameChecksum()
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

        Add(game.random.rndindex);
        foreach (var actor in map.GetActors())
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
        foreach (var p in game.players)
        {
            Add(p.health);
            Add(p.score);
            Add(p.armor);
            AddText(p.weapon);
        }
        for (int door = 0; door < doors.doornum; door++)
            Add(doors.doorobjlist[door].position);
        foreach (var wall in pushWalls.pushwalls)
        {
            Add(wall.State);
            Add(wall.X);
            Add(wall.Y);
        }
        return hash;
    }

    /// <summary>Whether the game with others is over for this machine: it left, or lost the host</summary>
    internal bool NetGameOver => hooks.NetGame() && (netLeft || NetSession.Current is not { Error: null });
}
