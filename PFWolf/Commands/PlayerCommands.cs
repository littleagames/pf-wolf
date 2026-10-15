using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>What the player commands need from the game</summary>
/// <param name="ViewSize">The view's size setting (21: the full screen, no status bar to redraw)</param>
/// <param name="DrawPlayScreen">Redraws the status bar and view border, for the player now yours</param>
internal sealed record PlayerCommandsHooks(
    Func<int> ViewSize,
    Action DrawPlayScreen);

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
        console.Register("gamemode", "Shows or sets how the players play together: co-op (keys shared, no friendly fire) or deathmatch.",
            "gamemode [coop|deathmatch [fraglimit]]", Cmd_GameMode, Cheat | InLevel, complete: (_, i) => i == 0 ? ["coop", "deathmatch"] : []);
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
            + $"{session.players.Count} players, {session.gamemode.ToString().ToLowerInvariant()}");
    }

    /// <summary>`gamemode [coop|deathmatch]`: how the players here play together (with more than one)</summary>
    internal void Cmd_GameMode(string[] args)
    {
        if (args.Length > 0)
        {
            if (session.players.Count < 2)
                throw new ArgumentException("there's only one player: addplayer first");
            session.SetGameMode(args[0].ToLowerInvariant() switch
            {
                "coop" => GameMode.Coop,
                "deathmatch" or "dm" => GameMode.Deathmatch,
                _ => throw new ArgumentException("usage: gamemode [coop|deathmatch [fraglimit]]"),
            });
            if (session.gamemode == GameMode.Deathmatch && args.Length > 1 && int.TryParse(args[1], out var fragLimit))
                deathmatch.netrules = deathmatch.netrules with { FragLimit = Math.Max(fragLimit, 0) };
            deathmatch.matchover = false;
        }
        console.Print($"Game mode: {session.gamemode.ToString().ToLowerInvariant()}, {session.players.Count} player(s)");
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
