using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>What the player commands need from the game</summary>
/// <param name="ViewSize">The view's size setting (21: the full screen, no status bar to redraw)</param>
/// <param name="DrawPlayScreen">Redraws the status bar and view border, for the player now yours</param>
/// <param name="PlayerColor">A color by name as this machine has it (the translation), or null</param>
internal sealed record PlayerCommandsHooks(
    Func<int> ViewSize,
    Action DrawPlayScreen,
    Func<string, string?>? PlayerColor = null);

/// <summary>Several players on one machine, for testing without the network: addplayer, gamemode and controlplayer</summary>
internal sealed class PlayerCommands(
    ConsoleManager console,
    MapManager map,
    Doors doors,
    PlayerClassRules classes,
    Players playersManager,
    PlayerThink think,
    Deathmatch deathmatch,
    GameSession session,
    PlayerCommandsHooks hooks)
{
    /// <summary>The most players a game can have</summary>
    internal const int MAXPLAYERS = 4;

    internal void Register()
    {
        console.Register("addplayer", $"Adds a co-op player beside you, who stands still until you take them over with controlplayer (up to {MAXPLAYERS}; testing without the network).",
            "addplayer [class]", Cmd_AddPlayer, Cheat | InLevel, complete: (_, i) => i == 0 ? classes.PlayerClasses() : []);
        console.Register("controlplayer", "Makes another player yours: your view, status bar and controls.",
            "controlplayer <n>", Cmd_ControlPlayer, Cheat | InLevel);
        console.Register("gamemode", "Shows or sets how the players play together: co-op, deathmatch, or a team mode (team-deathmatch, ctf, heist, domination, king-of-the-hill) or horde.",
            "gamemode [mode [scorelimit]]", Cmd_GameMode, Cheat | InLevel, complete: (_, i) => i == 0 ? GameModes.NetModes.Select(m => m.Id()).ToList() : []);
        console.Register("match", "Shows the match being played: the mode, the score, the time left and the objective (whose flag's where, the wave).",
            "match", Cmd_Match, InLevel);
        console.Register("setteam", "Puts you (or player n) on a team in a team game.",
            "setteam <red|blue> [n]", Cmd_SetTeam, Cheat | InLevel, complete: (_, i) => i == 0 ? [.. GameModes.TeamNames] : []);
    }

    /// <summary>`addplayer [class]`: a player who stands still (nothing drives them yet), beside you</summary>
    internal void Cmd_AddPlayer(string[] args)
    {
        if (session.players.Count >= MAXPLAYERS)
            throw new ArgumentException($"there can only be {MAXPLAYERS} players");

        var playerClass = args.Length > 0
            ? classes.FindPlayerClass(args[0]) ?? throw new ArgumentException($"no player class {args[0]}")
            : session.localplayer.playerclass;

        var p = new Entities.PlayerState { Number = session.players.Count };
        session.players.Add(p);
        if (session.gamemode == GameMode.Single)
            session.SetGameMode(GameMode.Coop);

        var at = session.localplayer.Pawn;
        using (session.ActAs(p))
        {
            playersManager.StartPlayer(playerClass);
            map.CreatePlayer(p);
            var taken = map.Players.Where(pawn => pawn != p.Pawn).Select(pawn => ((int)pawn.TileX, (int)pawn.TileY)).ToList();
            var (x, y) = at != null ? playersManager.FreeTileNear(at.TileX, at.TileY, taken) ?? (at.TileX, at.TileY) : (1, 1);
            think.SpawnPlayer(x, y, at?.Angle ?? 0);
        }
        doors.ConnectAreas();

        console.Print($"Player {p.Number + 1} ({playerClass}) is at {p.Pawn!.TileX},{p.Pawn.TileY}; "
            + $"{session.players.Count} players, {session.gamemode.Id()}");
    }

    /// <summary>
    /// `gamemode [mode [scorelimit]]`: how the players here play together (with more than one).
    /// A team mode puts them on alternate teams (setteam changes it), in the teams' colors, and
    /// starts the mode's objective on this level.
    /// </summary>
    internal void Cmd_GameMode(string[] args)
    {
        if (args.Length > 0)
        {
            if (session.players.Count < 2)
                throw new ArgumentException("there's only one player: addplayer first");
            var mode = GameModes.Parse(args[0]) ?? throw new ArgumentException($"usage: gamemode [{string.Join("|", GameModes.NetModes.Select(m => m.Id()))} [scorelimit]]");
            session.SetGameMode(mode);
            deathmatch.netrules = deathmatch.netrules with
            {
                ScoreLimit = args.Length > 1 && int.TryParse(args[1], out var limit) ? Math.Max(limit, 0) : mode.DefaultScoreLimit(),
            };
            foreach (var p in session.players)
                SetTeam(p, mode.IsTeamGame() ? p.Number % 2 : -1);
            deathmatch.NewMatch();
            if (map != null)
                deathmatch.ObjectiveLevelSetup();
        }
        console.Print($"Game mode: {session.gamemode.Id()}, {session.players.Count} player(s)");
    }

    /// <summary>`match`: the mode, score, time left and objective, and each player's team and score</summary>
    internal void Cmd_Match(string[] args)
    {
        var (score, news) = deathmatch.MatchStatus();
        console.Print($"Match: {session.gamemode.Id()}  {score}" + (news != null ? $"  |  {news}" : ""));
        if (deathmatch.Objective?.Details() is { } details)
            console.Print($"  {details}");
        foreach (var p in session.players.Where(p => !p.Gone))
            console.Print($"  {p.Number + 1}. {p.Name ?? $"Player {p.Number + 1}"}" + (p.Team >= 0 ? $" ({GameModes.TeamLabel(p.Team)})" : "")
                + $": frags {p.Frags}, objective {p.Objective}, kills {p.Kills}, health {p.health}" + (p.Pawn is { } pawn ? $" at {pawn.TileX},{pawn.TileY}" : ""));
    }

    /// <summary>`setteam &lt;red|blue&gt; [n]`: puts you (or player n) on a team, in its color</summary>
    internal void Cmd_SetTeam(string[] args)
    {
        int team = args.Length > 0 ? Array.FindIndex(GameModes.TeamNames, t => t.Equals(args[0], StringComparison.OrdinalIgnoreCase)) : -1;
        if (team < 0)
            throw new ArgumentException("usage: setteam <red|blue> [n]");
        if (!session.IsTeamGame)
            throw new ArgumentException("this isn't a team game: gamemode first");
        var p = args.Length > 1 && int.TryParse(args[1], out var n) && n >= 1 && n <= session.players.Count
            ? session.players[n - 1]
            : session.localplayer;
        SetTeam(p, team);
        console.Print($"Player {p.Number + 1} is on {GameModes.TeamLabel(team)}");
    }

    // A player's team, and their color for it (none: their own back)
    void SetTeam(Entities.PlayerState p, int team)
    {
        p.Team = team;
        if (team >= 0)
            p.Color = hooks.PlayerColor?.Invoke(GameModes.TeamNames[team]);
    }
    /// <summary>`controlplayer &lt;n&gt;`: your view, status bar and controls become player n's</summary>
    internal void Cmd_ControlPlayer(string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out var n) || n < 1 || n > session.players.Count)
            throw new ArgumentException($"usage: controlplayer <1-{session.players.Count}>");

        session.localplayer.PendingCmd = default;   // the one left behind stands still
        session.consoleplayer = n - 1;
        session.SetActing(session.localplayer);
        session.camera.FollowPlayer();
        if (hooks.ViewSize() != 21)
            hooks.DrawPlayScreen();
        console.Print($"You're player {n} ({session.localplayer.playerclass})");
    }

}
