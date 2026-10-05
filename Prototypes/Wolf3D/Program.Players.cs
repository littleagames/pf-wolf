using Wolf3D.Entities.Actors;

namespace Wolf3D;

internal partial class Program
{
    /*
    ====================
    =
    = Players
    =
    = Everyone in the game, each with their own PlayerState. The game's player code is written
    = for one player: it reads player, playerstate, playerinput and the player globals below,
    = which are always the acting player's. MapManager.DoActors makes each pawn the acting player
    = while it thinks, ActAs does it for anything else done to a particular player (hurting
    = them, say), and the rest of the time it's the local player: the one whose view, status
    = bar, flashes and sounds this machine shows.
    =
    ====================
    */

    /// <summary>Everyone in the game, in the order they think; never empty once a game has started</summary>
    internal static readonly List<Entities.PlayerState> players = [new()];

    /// <summary>Which of players is this machine's: what the screen shows and the controls drive</summary>
    internal static int consoleplayer;

    /// <summary>This machine's player</summary>
    internal static Entities.PlayerState localplayer => players[consoleplayer];

    /// <summary>The player acting now: see ActAs</summary>
    internal static Entities.PlayerState playerstate { get; private set; } = players[0];

    /// <summary>Whether the acting player is this machine's, so their screen, status bar and sounds are shown</summary>
    internal static bool ActingIsLocal => ReferenceEquals(playerstate, localplayer);

    /// <summary>
    /// Makes <paramref name="p"/> the acting player until the returned scope is disposed: the
    /// game's player code (player, playerstate, the inventory...) then reads and changes theirs.
    /// </summary>
    internal static ActingScope ActAs(Entities.PlayerState p)
    {
        var scope = new ActingScope(playerstate);
        SetActing(p);
        return scope;
    }

    static void SetActing(Entities.PlayerState p)
    {
        playerstate = p;
        _inventoryManager?.SetHolder(p.Items);
    }

    /// <summary>
    /// Whether the status bar's parts aren't drawn now: with the view full screen, or for a
    /// player other than this machine's (the bar is the local player's)
    /// </summary>
    static bool StatusBarHidden => viewsize == 21 && ingame || !ActingIsLocal;

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

    /// <summary>Hurts a particular player (an enemy's shot, a projectile), as the acting player while it does</summary>
    internal static void TakeDamage(Entities.Actors.PlayerPawn victim, int points, Entities.Actors.Actor attacker)
    {
        using var _ = ActAs(victim.State);
        TakeDamage(points, attacker);
    }

    /// <summary>
    /// A sound the acting player makes (a pickup, a shot): the local player hears their own
    /// as ever, anyone else's from where that player stands
    /// </summary>
    internal static void PlayPlayerSound(string name)
    {
        if (ActingIsLocal || playerstate.Pawn is not { } pawn)
            _audioManager.Play(name);
        else
            PlaySoundLocActor(name, pawn);
    }

    internal readonly struct ActingScope(Entities.PlayerState previous) : IDisposable
    {
        public void Dispose() => SetActing(previous);
    }

    /// <summary>
    /// A new game's players: just the local one, fresh. Anyone else is added after (addplayer).
    /// </summary>
    internal static void ResetPlayers()
    {
        players.Clear();
        players.Add(new Entities.PlayerState());
        consoleplayer = 0;
        SetActing(players[0]);
        SetGameMode(GameMode.Single);
        netgame = false;        // BeginNetGame says otherwise, after
    }

    /// <summary>The most players a game can have</summary>
    internal const int MAXPLAYERS = 4;

    internal enum GameMode { Single, Coop, Deathmatch }

    /// <summary>
    /// How the players play together: Single (just one), Coop (together against the level,
    /// keys shared) or Deathmatch (against each other)
    /// </summary>
    internal static GameMode gamemode { get; private set; } = GameMode.Single;

    internal static void SetGameMode(GameMode mode)
    {
        gamemode = mode;
        _inventoryManager.ShareKeys = mode == GameMode.Coop;
    }

    /// <summary>
    /// Puts every player on the level: the first on the map's start, the rest on the nearest
    /// free floor tiles around it, all facing the start's way.
    /// </summary>
    internal static void SpawnPlayers(int tilex, int tiley, int angle)
    {
        var taken = new List<(int x, int y)>();
        foreach (var p in players.Where(p => p.Pawn != null))
        {
            using var _ = ActAs(p);
            if (gamemode == GameMode.Deathmatch)
            {
                // somewhere random, carrying every key (Program.Deathmatch.cs)
                var (dx, dy, dangle) = DeathmatchSpot(p.Pawn!);
                SpawnPlayer(dx, dy, dangle);
                GiveDeathmatchKeys();
                continue;
            }

            var (x, y) = taken.Count == 0 ? (tilex, tiley) : FreeTileNear(tilex, tiley, taken) ?? (tilex, tiley);
            taken.Add((x, y));
            SpawnPlayer(x, y, angle);
        }
    }

    /// <summary>
    /// The nearest open floor tile to a spot (by steps across open floor, so never through a
    /// wall), that isn't one of <paramref name="taken"/> and has nothing standing on it; null
    /// if there's none within reach.
    /// </summary>
    static (int x, int y)? FreeTileNear(int tilex, int tiley, List<(int x, int y)> taken)
    {
        const int Reach = 6;     // steps from the start
        var seen = new HashSet<(int, int)> { (tilex, tiley) };
        var frontier = new Queue<(int x, int y, int steps)>();
        frontier.Enqueue((tilex, tiley, 0));

        while (frontier.Count > 0)
        {
            var (x, y, steps) = frontier.Dequeue();
            if (!taken.Contains((x, y)) && IsFreeFloor(x, y))
                return (x, y);
            if (steps == Reach)
                continue;

            foreach (var (dx, dy) in new[] { (1, 0), (0, -1), (-1, 0), (0, 1) })
            {
                int nx = x + dx, ny = y + dy;
                if (nx <= 0 || ny <= 0 || nx >= _mapManager.mapwidth - 1 || ny >= _mapManager.mapheight - 1
                    || !seen.Add((nx, ny)) || !IsOpenFloor(nx, ny))
                    continue;
                frontier.Enqueue((nx, ny, steps + 1));
            }
        }
        return null;
    }

    // Floor a player can walk on: no wall, door or solid thing, and in an area
    static bool IsOpenFloor(int x, int y) =>
        _mapManager.tilemap[x, y] == 0 && _mapManager.actorat[x, y] == null
        && _mapManager.VALIDAREA(_mapManager.MAPSPOT(x, y, 0));

    // Open floor nobody stands on: no enemy, and no player already put there
    static bool IsFreeFloor(int x, int y) =>
        IsOpenFloor(x, y) && !_mapManager.EnemiesAt(x, y).Any()
        && !_mapManager.Players.Any(pawn => pawn.TileX == x && pawn.TileY == y && pawn.Active == activetypes.ac_yes);

    /*
    ====================
    =
    = Dying with others playing
    =
    = Alone, dying ends the level (Died). With others, the game plays on: the dead player lies
    = where they fell, turning to face their killer as their view sinks (game-info
    = death-turn-speed, death-drop-height and death-drop-speed, as Died), and a press of use or
    = fire once RESPAWNTICS have passed brings them back at the level's start with the class's
    = starting loadout: whatever else they carried is lost. Their score stays; lives don't count.
    =
    ====================
    */

    /// <summary>How long a dead player lies before they can come back (tics)</summary>
    const int RESPAWNTICS = 70;

    /// <summary>The acting player has just been killed, with others playing on</summary>
    internal static void PlayerDies()
    {
        playerstate.weapon = null;              // take away weapon, as Died
        playerstate.DeadTics = 0;
        playerstate.DeathAngle = FacingAngle(player, LastAttacker);
        player.SetState(Entities.Actors.PlayerPawn.DeadState);
        if (gamemode == GameMode.Deathmatch)
            CountFrag(playerstate, LastAttacker);

        if (_inventoryManager.GetStringProperty(PlayerClass, "deathsound") is { Length: > 0 } deathSound)
            PlayPlayerSound(deathSound);
        ShowObituary();
    }

    // The angle from a player to an actor (the way they turn to face their killer), else behind them
    static short FacingAngle(Entities.Actors.PlayerPawn from, Entities.Actors.Actor? to)
    {
        if (to == null)
            return (short)((from.Angle + ANGLES / 2) % ANGLES);

        var fangle = (float)Math.Atan2((float)(from.Y - to.Y), (float)(to.X - from.X));
        if (fangle < 0)
            fangle = (float)(M_PI * 2 + fangle);
        return (short)((int)(fangle / (M_PI * 2) * ANGLES) % ANGLES);
    }

    /// <summary>A dead player's think: turning to face their killer, sinking, and waiting to come back</summary>
    internal static void T_PlayerDead(Entities.Actors.Actor ob)
    {
        playerstate.DeadTics += (int)tics;

        // Turn the shorter way round toward the killer
        int diff = ((playerstate.DeathAngle - player.Angle) % ANGLES + ANGLES) % ANGLES;
        if (diff != 0)
        {
            int step = Math.Min((int)tics * deathTurnSpeed, diff <= ANGLES / 2 ? diff : ANGLES - diff);
            player.Angle = (short)(((player.Angle + (diff <= ANGLES / 2 ? step : -step)) % ANGLES + ANGLES) % ANGLES);
        }

        // and the view sinks (only with a death-drop-height)
        if (deathDropHeight is { } dropto && playereyez > dropto)
            playereyez = Math.Max(playereyez - (int)tics * deathDropSpeed, dropto);

        if (playerstate.DeadTics >= RESPAWNTICS
            && (playerinput.IsFreshPress(buttontypes.bt_use) || playerinput.IsFreshPress(buttontypes.bt_attack)))
            Respawn();
    }

    /// <summary>Brings the acting player back at the level's start, with the class's starting loadout</summary>
    static void Respawn()
    {
        GiveStartingInventory();
        playerstate.health = StartingHealth;
        playerstate.Pitch = 0;
        playerstate.EyeZ = EYEDEFAULT;
        playerstate.WeaponSprite = null;
        playerstate.WeaponCharge = 0;
        playerstate.LastAttacker = null;
        playerstate.PainTics = playerstate.FaceTimes = 0;

        // The start, or the nearest free spot to it with someone already there
        var taken = _mapManager.Players
            .Where(pawn => pawn != player && pawn.State.health > 0)
            .Select(pawn => ((int)pawn.TileX, (int)pawn.TileY))
            .ToList();
        int x = player.TileX, y = player.TileY, angle = player.Angle;
        if (gamemode == GameMode.Deathmatch)
            (x, y, angle) = DeathmatchSpot(player);
        else if (_mapManager.PlayerStart is { } start)
        {
            (x, y) = taken.Contains((start.TileX, start.TileY))
                ? FreeTileNear(start.TileX, start.TileY, taken) ?? (start.TileX, start.TileY)
                : (start.TileX, start.TileY);
            angle = start.Angle;
        }

        player.SetPosition(x, y);
        player.AreaNumber = _mapManager.SpawnArea(x, y);
        player.SetState(Entities.Actors.PlayerPawn.SpawnState);
        player.Angle = (short)((angle % ANGLES + ANGLES) % ANGLES);
        Thrust(0, 0);
        ConnectAreas();
        if (gamemode == GameMode.Deathmatch)
            GiveDeathmatchKeys();
        if (ActingIsLocal)
            camera.FollowPlayer();      // back through their own eyes, from watching someone

        // Their screen and status bar, if they're this machine's player
        if (ActingIsLocal)
            _videoManager.ClearPaletteShifts();     // the red of the killing blow
        DrawFace();
        DrawHealth();
        DrawArmor();
        DrawWeapon();
        DrawAmmo();
        DrawKeys();
    }

    /// <summary>What messages call a player: their name, else "Player n" (the class's tag, alone)</summary>
    static string? PlayerName(Entities.Actors.PlayerPawn pawn) =>
        gamemode == GameMode.Single ? null : pawn.State.Name ?? $"Player {pawn.State.Number + 1}";

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

    /// <summary>The acting player starts the game afresh as <paramref name="playerClass"/>: their loadout, health and lives</summary>
    internal static void StartPlayer(string playerClass)
    {
        playerstate.playerclass = playerClass;
        GiveStartingInventory();

        playerstate.health = StartingHealth;
        playerstate.lives = StartingLives;
        playerstate.nextextra = ExtraLifeScore;
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
