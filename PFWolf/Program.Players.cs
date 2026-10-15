using PFWolf.Entities.Actors;

namespace PFWolf;

internal partial class Program
{
    /*
    ====================
    =
    = Players
    =
    = Everyone in the game, each with their own PlayerState, and who's acting now: the
    = GameSession (Gameplay/GameSession.cs), which these forward to.
    =
    ====================
    */

    internal static List<Entities.PlayerState> players => session.players;
    internal static int consoleplayer { get => session.consoleplayer; set => session.consoleplayer = value; }
    internal static Entities.PlayerState localplayer => session.localplayer;
    internal static Entities.PlayerState playerstate => session.playerstate;
    internal static bool ActingIsLocal => session.ActingIsLocal;
    internal static GameSession.ActingScope ActAs(Entities.PlayerState p) => session.ActAs(p);
    static void SetActing(Entities.PlayerState p) => session.SetActing(p);
    internal static GameMode gamemode => session.gamemode;
    internal static void SetGameMode(GameMode mode) => session.SetGameMode(mode);

    /*
    ====================
    =
    = What players see, with several of them
    =
    = Alone, what the player sees is what the renderer drew: an enemy on the screen is
    = FL_VISABLE (enemies aim worse at a player who can see them) and wakes for good. With
    = others, the screen is just one of them (and on another machine, another one), so it's
    = worked out from each player's own position instead: InViewOf, the same on every machine.
    =
    ====================
    */

    /// <summary>
    /// Whether a player could see an actor: in front of them, within the view's width either
    /// side (the classic field of view, however wide the screen), with nothing in the way
    /// </summary>
    internal static bool InViewOf(Entities.Actors.PlayerPawn pawn, Entities.Actors.Actor actor)
    {
        int sin = sintable[pawn.Angle], cos = sintable[pawn.Angle + ANGLES / 4];
        int fromx = pawn.X - MathUtils.FixedMul(focallength, cos), fromy = pawn.Y + MathUtils.FixedMul(focallength, sin);
        int gx = actor.X - fromx, gy = actor.Y - fromy;
        int nx = MathUtils.FixedMul(gx, cos) - MathUtils.FixedMul(gy, sin) - ACTORSIZE;
        int ny = MathUtils.FixedMul(gy, cos) + MathUtils.FixedMul(gx, sin);
        if (nx < MINDIST)
            return false;

        // |ny / nx| * scale within half the view: the view's width taken out of both
        return Math.Abs((long)ny) * (FOCALLENGTH + MINDIST) < (long)nx * (VIEWGLOBAL / 2)
            && CheckLine(actor, pawn);
    }

    /// <summary>Whether any living player could see an actor (InViewOf)</summary>
    internal static bool SeenByAPlayer(Entities.Actors.Actor actor) =>
        _mapManager.Players.Any(pawn => pawn.State.health > 0 && InViewOf(pawn, actor));

    /// <summary>
    /// With several players, each frame: enemies a living player could see wake for good, as
    /// being drawn wakes them alone (MapManager.DoActor lets a sleeping one rest out of reach)
    /// </summary>
    internal static void WakeSeenEnemies()
    {
        foreach (var actor in _mapManager.GetActors())
        {
            if (actor is Entities.Actors.Monster && !actor.IsRemoved && actor.Active == activetypes.ac_no && SeenByAPlayer(actor))
                actor.Active = activetypes.ac_yes;
        }
    }

    // Everyone in the game coming and going (Gameplay/Players.cs)
    static Players playersManager = null!;      // made in InitServices

    static Players MakePlayers() =>
        new(_mapManager, _audioManager, _videoManager, _inventoryManager, statusBarDrawing, playerClassRules, playerStats,
            playerWeapons, playerMovement, playerThink, session,
            new PlayersHooks(PlaySoundLocActor, DeathmatchSpot, GiveDeathmatchKeys, CountFrag, ShowObituary, ConnectAreas,
                () => deathTurnSpeed, () => deathDropHeight, () => deathDropSpeed, () => netgame = false));

    internal static void TakeDamage(Entities.Actors.PlayerPawn victim, int points, Entities.Actors.Actor attacker) => playersManager.TakeDamage(victim, points, attacker);
    internal static void PlayPlayerSound(string name) => playersManager.PlayPlayerSound(name);
    internal static void ResetPlayers() => playersManager.ResetPlayers();
    internal static void SpawnPlayers(int tilex, int tiley, int angle) => playersManager.SpawnPlayers(tilex, tiley, angle);
    internal static (int x, int y) StandInStart() => playersManager.StandInStart();
    static (int x, int y)? FreeTileNear(int tilex, int tiley, List<(int x, int y)> taken) => playersManager.FreeTileNear(tilex, tiley, taken);
    static bool IsFreeFloor(int x, int y) => playersManager.IsFreeFloor(x, y);
    internal static void PlayerDies() => playersManager.PlayerDies();
    internal static void T_PlayerDead(Entities.Actors.Actor ob) => playersManager.T_PlayerDead(ob);
    static string? PlayerName(Entities.Actors.PlayerPawn pawn) => playersManager.PlayerName(pawn);
    internal static void StartPlayer(string playerClass) => playersManager.StartPlayer(playerClass);

    /// <summary>The most players a game can have</summary>
    internal const int MAXPLAYERS = 4;

    /// <summary>`addplayer [class]`: a player who stands still (nothing drives them yet), beside you</summary>
    static void Cmd_AddPlayer(string[] args)
    {
        if (players.Count >= MAXPLAYERS)
            throw new ArgumentException($"there can only be {MAXPLAYERS} players");

        var playerClass = args.Length > 0
            ? FindPlayerClass(args[0]) ?? throw new ArgumentException($"no player class {args[0]}")
            : localplayer.playerclass;

        var p = new Entities.PlayerState { Number = players.Count };
        players.Add(p);
        if (gamemode == GameMode.Single)
            SetGameMode(GameMode.Coop);

        var at = localplayer.Pawn;
        using (ActAs(p))
        {
            StartPlayer(playerClass);
            _mapManager.CreatePlayer(p);
            var taken = _mapManager.Players.Where(pawn => pawn != p.Pawn).Select(pawn => ((int)pawn.TileX, (int)pawn.TileY)).ToList();
            var (x, y) = at != null ? FreeTileNear(at.TileX, at.TileY, taken) ?? (at.TileX, at.TileY) : (1, 1);
            SpawnPlayer(x, y, at?.Angle ?? 0);
        }
        ConnectAreas();

        _consoleManager.Print($"Player {p.Number + 1} ({playerClass}) is at {p.Pawn!.TileX},{p.Pawn.TileY}; "
            + $"{players.Count} players, {gamemode.ToString().ToLowerInvariant()}");
    }

    /// <summary>`gamemode [coop|deathmatch]`: how the players here play together (with more than one)</summary>
    static void Cmd_GameMode(string[] args)
    {
        if (args.Length > 0)
        {
            if (players.Count < 2)
                throw new ArgumentException("there's only one player: addplayer first");
            SetGameMode(args[0].ToLowerInvariant() switch
            {
                "coop" => GameMode.Coop,
                "deathmatch" or "dm" => GameMode.Deathmatch,
                _ => throw new ArgumentException("usage: gamemode [coop|deathmatch [fraglimit]]"),
            });
            if (gamemode == GameMode.Deathmatch && args.Length > 1 && int.TryParse(args[1], out var fragLimit))
                netrules = netrules with { FragLimit = Math.Max(fragLimit, 0) };
            matchover = false;
        }
        _consoleManager.Print($"Game mode: {gamemode.ToString().ToLowerInvariant()}, {players.Count} player(s)");
    }

    /// <summary>`controlplayer &lt;n&gt;`: your view, status bar and controls become player n's</summary>
    static void Cmd_ControlPlayer(string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out var n) || n < 1 || n > players.Count)
            throw new ArgumentException($"usage: controlplayer <1-{players.Count}>");

        localplayer.PendingCmd = default;   // the one left behind stands still
        consoleplayer = n - 1;
        SetActing(localplayer);
        camera.FollowPlayer();
        if (viewsize != 21)
            DrawPlayScreen();
        _consoleManager.Print($"You're player {n} ({localplayer.playerclass})");
    }

    //
    // The acting player's state, under the names the game has always used for it
    //
    internal static int thrustspeed { get => playerstate.ThrustSpeed; set => playerstate.ThrustSpeed = value; }
    static ushort plux { get => playerstate.PlUX; set => playerstate.PlUX = value; }    // player coordinates scaled to unsigned
    static ushort pluy { get => playerstate.PlUY; set => playerstate.PlUY = value; }
    static short anglefrac { get => playerstate.AngleFrac; set => playerstate.AngleFrac = value; }
    static Entities.Actors.Actor? LastAttacker { get => playerstate.LastAttacker; set => playerstate.LastAttacker = value; }
    static string? grinsound { get => playerstate.GrinSound; set => playerstate.GrinSound = value; }
    static int facecount { get => playerstate.FaceCount; set => playerstate.FaceCount = value; }
    static int facetimes { get => playerstate.FaceTimes; set => playerstate.FaceTimes = value; }
    static Entities.Actors.Actor? weaponSprite { get => playerstate.WeaponSprite; set => playerstate.WeaponSprite = value; }
    internal static int weaponcharge { get => playerstate.WeaponCharge; set => playerstate.WeaponCharge = value; }

    // The player's own look up/down (degrees, up positive) and eye height (texels above the
    // floor): reset each level, not saved
    internal static double playerpitch { get => playerstate.Pitch; set => playerstate.Pitch = value; }
    internal static int playereyez { get => playerstate.EyeZ; set => playerstate.EyeZ = value; }
}
