using PFWolf.Managers;

namespace PFWolf.Networking;

/// <summary>What playing with others needs from the game</summary>
/// <param name="LocalCmd">This machine's controls for this frame (PollControls' LocalCmd)</param>
/// <param name="WriteNetState">The game as it stands, for a player who's just joined (Networking/MidGameJoin.cs)</param>
/// <param name="ThreeDRefresh">Draws the view, with the waiting-for message over it</param>
/// <param name="ResetLastTimeCount">After a wait: no rush to catch up on it</param>
/// <param name="AddNetPlayer">A player joins at this frame (Gameplay/Players.cs)</param>
/// <param name="ConnectAreas">Works out again which areas join the players' (one has left)</param>
internal sealed record NetPlayHooks(
    Func<Entities.TicCmd> LocalCmd,
    Func<byte[]> WriteNetState,
    Action ThreeDRefresh,
    Action ResetLastTimeCount,
    Action<NetJoin> AddNetPlayer,
    Action ConnectAreas);

/// <summary>
/// Every machine plays the whole game, from everyone's controls. A frame (a step) goes:
/// this machine's controls (PollControls' LocalCmd) are sent for the step NetDelay ahead; the
/// host gathers everyone's for each step and sends them out together; and once this step's
/// are here, every player gets theirs (TicBundle) and the actors think. As long as each
/// machine starts the same and plays the same controls through the same code, they all
/// come out the same, so only the controls go over the network. Frames are NetTics tics long,
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
    internal const uint NetTics = 2;

    /// <summary>
    /// How many frames ahead a machine sends its controls: the time they have to reach the host
    /// and come back before they're needed, at the cost of that much delay before they act
    /// </summary>
    internal const int NetDelay = 2;

    /// <summary>How often (frames) the game is summed up to check every machine still agrees</summary>
    internal const int NetCheckEvery = 35;

    // The level being played, counted from 1 on every machine alike (each PlayLoop), and its frame
    internal int NetLevel, NetStep;

    // Esc pressed: asking whether to leave. And whether this machine has left the game
    internal bool NetLeavePrompt, NetLeft;

    // The waiting-for message, while a step's controls are late
    internal string? NetWaiting;

    // Host: a player who came in at the frame just played, to send the game to before the next
    internal (int Index, int Step)? PendingJoinState;

    // A player joining: the game they've just taken up carries on, rather than a level starting
    internal bool NetJoinResume;

    /// <summary>Starts counting frames for a new level, sending empty controls for the frames before ours can arrive</summary>
    internal void NetLevelStart()
    {
        NetLeavePrompt = false;
        NetWaiting = null;
        PendingJoinState = null;        // (someone who came in on a level's last frame is left waiting)
        if (NetSession.Current is not { } session)
            return;

        // Just joined: the game taken up carries on from the frame after the host's (BeginJoinedGame)
        if (NetJoinResume)
        {
            NetJoinResume = false;
            for (int step = NetStep; step < NetStep + NetDelay; step++)
                session.SubmitLocal(NetLevel, step, default);
            return;
        }

        NetLevel++;
        NetStep = 0;
        session.ForgetBefore(NetLevel);
        for (int step = 0; step < NetDelay; step++)
            session.SubmitLocal(NetLevel, step, default);
    }

    /// <summary>
    /// This frame's controls for everyone: sends ours, waits for everyone's, and gives each
    /// player theirs. False when the game with others is over for this machine (it left, or
    /// lost the host): the level then ends.
    /// </summary>
    internal bool NetFrame()
    {
        if (NetSession.Current is not { } session || session.Error != null || NetLeft)
            return false;

        // A player who came in at the frame just played gets the game as it now stands
        session.Poll();
        if (PendingJoinState is { } joined)
        {
            session.SendJoinState(joined.Index, NetLevel, joined.Step, hooks.WriteNetState());
            PendingJoinState = null;
        }

        // While asking about leaving, this player stands still
        session.SubmitLocal(NetLevel, NetStep + NetDelay, NetLeavePrompt ? default : hooks.LocalCmd());

        TicBundle? bundle;
        long waitStart = Environment.TickCount64;
        while ((bundle = session.TryTake(NetLevel, NetStep)) == null)
        {
            session.Poll();
            if (session.Error != null)
                return false;

            // Late: say who for, and let Esc give up on them
            if (Environment.TickCount64 - waitStart > 500)
            {
                var waiting = $"Waiting for {string.Join(", ", session.WaitingFor(NetLevel, NetStep))}...";
                if (waiting != NetWaiting && !session.HasBundle(NetLevel, NetStep))
                {
                    NetWaiting = waiting;
                    hooks.ThreeDRefresh();
                    if (video.ScreenFaded)
                        video.FadeIn();     // a level's first frame: others are still on the intermission
                }
                input.ProcessEvents();
                if (input.IsKeyDown(ScanCodes.Escape))
                {
                    input.ClearKeysDown();
                    NetLeft = true;
                    return false;
                }
            }
            GameEngineManager.DelayMs(1);
        }

        if (NetWaiting != null)
        {
            NetWaiting = null;
            hooks.ResetLastTimeCount();     // no rush to catch up on the wait
        }

        // Anyone leaving leaves, and anyone joining joins, at this step on every machine
        var players = game.Players;
        for (int i = 0; i < players.Count; i++)
        {
            if (bundle.HasLeft(i) && !players[i].Gone)
                RemovePlayer(players[i]);
        }
        if (bundle.Join is { } join)
        {
            hooks.AddNetPlayer(join);
            if (session.IsHost)
                PendingJoinState = (join.Index, NetStep);     // the game goes to them once this frame's played
        }
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            p.Input.Begin(!p.Gone && i < bundle.Cmds.Length ? bundle.Cmds[i] : default);
        }

        NetStep++;
        return true;
    }

    /// <summary>After a frame has played: checks now and then that every machine still agrees, and shows what the host has to say</summary>
    internal void NetFrameDone()
    {
        if (NetSession.Current is not { } session)
            return;

        if (NetStep % NetCheckEvery == 0)
            session.ReportChecksum(NetLevel, NetStep, GameChecksum());

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
            if (game.Camera.Target == pawn)
                game.Camera.FollowPlayer();
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

        Add(game.Random.RndIndex);
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
        foreach (var p in game.Players)
        {
            Add(p.Health);
            Add(p.Score);
            Add(p.Armor);
            AddText(p.Weapon);
        }
        for (int door = 0; door < doors.DoorNum; door++)
            Add(doors.DoorObjList[door].Position);
        foreach (var wall in pushWalls.MovingWalls)
        {
            Add(wall.State);
            Add(wall.X);
            Add(wall.Y);
        }
        return hash;
    }

    /// <summary>Whether the game with others is over for this machine: it left, or lost the host</summary>
    internal bool NetGameOver => game.NetGame && (NetLeft || NetSession.Current is not { Error: null });
}
