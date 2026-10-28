using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>What the players' comings and goings need from the game</summary>
/// <param name="PlaySoundAt">A sound from where an actor is</param>
/// <param name="DeathmatchSpot">Deathmatch: somewhere random for a player to start</param>
/// <param name="GiveDeathmatchKeys">Deathmatch: the acting player carries every key</param>
/// <param name="CountFrag">With others: a player was killed, by whom (a frag, a flag dropped)</param>
/// <param name="MayRespawn">Whether a dead player may come back yet (Horde: between waves)</param>
/// <param name="ShowObituary">Says what killed the acting player</param>
/// <param name="ConnectAreas">Works out again which areas join the players' (a player came or went)</param>
/// <param name="DeathTurnSpeed">How fast a dead player turns to face their killer (game-info death-turn-speed)</param>
/// <param name="DeathDropHeight">How low a dead player's view sinks, or null for not at all (game-info death-drop-height)</param>
/// <param name="DeathDropSpeed">How fast it sinks (game-info death-drop-speed)</param>
/// <param name="PlayerColor">A player's color as this machine has it: the translation, or null if there's no such one</param>
/// <param name="Spawned">A player's been put on the level mid-game, and why: "respawn" or "join" (game.onspawn)</param>
internal sealed record PlayersHooks(
    Action<string, Entities.Actors.Actor> PlaySoundAt,
    Func<Entities.Actors.PlayerPawn, (int x, int y, int angle)> DeathmatchSpot,
    Action GiveDeathmatchKeys,
    Action<Entities.PlayerState, Entities.Actors.Actor?> CountFrag,
    Func<Entities.PlayerState, bool> MayRespawn,
    Action ShowObituary,
    Action ConnectAreas,
    Func<int> DeathTurnSpeed,
    Func<int?> DeathDropHeight,
    Func<int> DeathDropSpeed,
    Func<string, string?> PlayerColor,
    Action<Entities.Actors.PlayerPawn, string>? Spawned = null);

/// <summary>
/// Everyone in the game coming and going: a new game's players, putting them on a level, a
/// player hurt or making a sound, and -- with others playing -- dying, lying there and coming
/// back. Who's in the game and who's acting is the GameSession's (Gameplay/GameSession.cs).
/// </summary>
internal sealed class Players(
    MapManager map,
    AudioManager audio,
    VideoManager video,
    InventoryManager inventory,
    HudMessageManager hudMessages,
    StatusBarDrawing statusBar,
    PlayerClassRules classes,
    PlayerStats stats,
    PlayerWeapons weapons,
    PlayerMovement movement,
    PlayerThink think,
    GameSession session,
    PlayersHooks hooks)
{
    const int Angles = GameConstants.Angles;

    /// <summary>Hurts a particular player (an enemy's shot, a projectile), as the acting player while it does</summary>
    internal void TakeDamage(Entities.Actors.PlayerPawn victim, int points, Entities.Actors.Actor attacker)
    {
        using var _ = session.ActAs(victim.State);
        stats.TakeDamage(points, attacker);
    }

    /// <summary>
    /// A sound the acting player makes (a pickup, a shot): the local player hears their own
    /// as ever, anyone else's from where that player stands
    /// </summary>
    internal void PlayPlayerSound(string name)
    {
        if (session.ActingIsLocal || session.PlayerState.Pawn is not { } pawn)
            audio.Play(name);
        else
            hooks.PlaySoundAt(name, pawn);
    }

    /// <summary>
    /// A new game's players: just the local one, fresh. Anyone else is added after (addplayer).
    /// </summary>
    internal void ResetPlayers()
    {
        var players = session.Players;
        players.Clear();
        players.Add(new Entities.PlayerState());
        session.ConsolePlayer = 0;
        session.SetActing(players[0]);
        session.SetGameMode(GameMode.Single);
        session.NetGame = false;       // BeginNetGame says otherwise, after
    }

    /// <summary>
    /// Puts every player on the level: the first on the map's start, the rest on the nearest
    /// free floor tiles around it, all facing the start's way.
    /// </summary>
    internal void SpawnPlayers(int tilex, int tiley, int angle)
    {
        var taken = new List<(int x, int y)>();
        foreach (var p in session.Players.Where(p => p.Pawn != null))
        {
            using var _ = session.ActAs(p);
            if (session.IsVersus)
            {
                // somewhere random, carrying every key (Gameplay/Deathmatch.cs)
                var (dx, dy, dangle) = hooks.DeathmatchSpot(p.Pawn!);
                think.SpawnPlayer(dx, dy, dangle);
                hooks.GiveDeathmatchKeys();
                continue;
            }

            var (x, y) = taken.Count == 0 ? (tilex, tiley) : FreeTileNear(tilex, tiley, taken) ?? (tilex, tiley);
            taken.Add((x, y));
            think.SpawnPlayer(x, y, angle);
        }
    }

    /// <summary>
    /// Where the players go on a level with no player start: the empty tile nearest the middle
    /// of the map (one in an area first), else the middle itself
    /// </summary>
    internal (int x, int y) StandInStart()
    {
        int midx = map.MapWidth / 2, midy = map.MapHeight / 2;
        var tiles = from y in Enumerable.Range(1, Math.Max(map.MapHeight - 2, 0))
                    from x in Enumerable.Range(1, Math.Max(map.MapWidth - 2, 0))
                    where map.TileMap[x, y] == 0 && map.ActorAt[x, y] == null
                    orderby IsFreeFloor(x, y) ? 0 : 1, (x - midx) * (x - midx) + (y - midy) * (y - midy)
                    select (x, y);
        return tiles.Cast<(int, int)?>().FirstOrDefault() ?? (midx, midy);
    }

    /// <summary>
    /// The nearest open floor tile to a spot (by steps across open floor, so never through a
    /// wall), that isn't one of <paramref name="taken"/> and has nothing standing on it; null
    /// if there's none within reach.
    /// </summary>
    internal (int x, int y)? FreeTileNear(int tilex, int tiley, List<(int x, int y)> taken)
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
                if (nx <= 0 || ny <= 0 || nx >= map.MapWidth - 1 || ny >= map.MapHeight - 1
                    || !seen.Add((nx, ny)) || !IsOpenFloor(nx, ny))
                    continue;
                frontier.Enqueue((nx, ny, steps + 1));
            }
        }
        return null;
    }

    // Floor a player can walk on: no wall, door or solid thing, and in an area
    internal bool IsOpenFloor(int x, int y) =>
        map.TileMap[x, y] == 0 && map.ActorAt[x, y] == null
        && map.ValidArea(map.MapSpot(x, y, 0));

    // Open floor nobody stands on: no enemy, and no player already put there
    internal bool IsFreeFloor(int x, int y) =>
        IsOpenFloor(x, y) && !map.EnemiesAt(x, y).Any()
        && !map.Players.Any(pawn => pawn.TileX == x && pawn.TileY == y && pawn.Active == ActiveType.Yes);

    /*
    ====================
    =
    = Dying with others playing
    =
    = Alone, dying ends the level (Died). With others, the game plays on: the dead player lies
    = where they fell, turning to face their killer as their view sinks (game-info
    = death-turn-speed, death-drop-height and death-drop-speed, as Died), and a press of use or
    = fire once RespawnTics have passed brings them back at the level's start with the class's
    = starting loadout: whatever else they carried is lost. Their score stays; lives don't count.
    =
    ====================
    */

    /// <summary>How long a dead player lies before they can come back (tics)</summary>
    internal const int RespawnTics = 70;

    /// <summary>The acting player has just been killed, with others playing on</summary>
    internal void PlayerDies()
    {
        var playerstate = session.PlayerState;
        var player = session.Player;
        playerstate.Weapon = null;              // take away weapon, as Died
        playerstate.DeadTics = 0;
        playerstate.DeathAngle = FacingAngle(player, playerstate.LastAttacker);
        player.SetState(Entities.Actors.PlayerPawn.DeadState);
        if (session.GameMode != GameMode.Coop)
            hooks.CountFrag(playerstate, playerstate.LastAttacker);     // a frag, a flag dropped... (Gameplay/Deathmatch.cs)

        if (inventory.GetStringProperty(playerstate.PlayerClass, "deathsound") is { Length: > 0 } deathSound)
            PlayPlayerSound(deathSound);
        hooks.ShowObituary();
    }

    // The angle from a player to an actor (the way they turn to face their killer), else behind them
    internal static short FacingAngle(Entities.Actors.PlayerPawn from, Entities.Actors.Actor? to)
    {
        if (to == null)
            return (short)((from.Angle + Angles / 2) % Angles);

        var fangle = (float)Math.Atan2((float)(from.Y - to.Y), (float)(to.X - from.X));
        if (fangle < 0)
            fangle = (float)(GameConstants.MathPi * 2 + fangle);
        return (short)((int)(fangle / (GameConstants.MathPi * 2) * Angles) % Angles);
    }

    /// <summary>A dead player's think: turning to face their killer, sinking, and waiting to come back</summary>
    internal void ThinkPlayerDead(Entities.Actors.Actor ob)
    {
        var playerstate = session.PlayerState;
        var player = session.Player;
        var tics = session.Tics;
        playerstate.DeadTics += (int)tics;

        // Turn the shorter way round toward the killer
        int diff = ((playerstate.DeathAngle - player.Angle) % Angles + Angles) % Angles;
        if (diff != 0)
        {
            int step = Math.Min((int)tics * hooks.DeathTurnSpeed(), diff <= Angles / 2 ? diff : Angles - diff);
            player.Angle = (short)(((player.Angle + (diff <= Angles / 2 ? step : -step)) % Angles + Angles) % Angles);
        }

        // and the view sinks (only with a death-drop-height)
        if (hooks.DeathDropHeight() is { } dropto && playerstate.EyeZ > dropto)
            playerstate.EyeZ = Math.Max(playerstate.EyeZ - (int)tics * hooks.DeathDropSpeed(), dropto);

        if (playerstate.DeadTics >= RespawnTics && hooks.MayRespawn(playerstate)
            && (session.PlayerInput.IsFreshPress(ButtonType.Use) || session.PlayerInput.IsFreshPress(ButtonType.Attack)))
            Respawn();
    }

    /// <summary>Brings the acting player back at the level's start, with the class's starting loadout</summary>
    internal void Respawn()
    {
        var playerstate = session.PlayerState;
        var player = session.Player;
        stats.GiveStartingInventory();
        playerstate.Health = classes.StartingHealth;
        playerstate.Pitch = 0;
        playerstate.EyeZ = classes.StandingEyeZ(playerstate);
        playerstate.WeaponSprite = null;
        playerstate.WeaponCharge = 0;
        playerstate.LastAttacker = null;
        playerstate.PainTics = playerstate.FaceTimes = 0;

        // The start, or the nearest free spot to it with someone already there
        var taken = map.Players
            .Where(pawn => pawn != player && pawn.State.Health > 0)
            .Select(pawn => ((int)pawn.TileX, (int)pawn.TileY))
            .ToList();
        int x = player.TileX, y = player.TileY, angle = player.Angle;
        if (session.IsVersus)
            (x, y, angle) = hooks.DeathmatchSpot(player);
        else if (map.PlayerStart is { } start)
        {
            (x, y) = taken.Contains((start.TileX, start.TileY))
                ? FreeTileNear(start.TileX, start.TileY, taken) ?? (start.TileX, start.TileY)
                : (start.TileX, start.TileY);
            angle = start.Angle;
        }

        player.SetPosition(x, y);
        player.AreaNumber = map.SpawnArea(x, y);
        player.SetState(Entities.Actors.PlayerPawn.SpawnState);
        player.Angle = (short)((angle % Angles + Angles) % Angles);
        movement.Thrust(0, 0);
        hooks.ConnectAreas();
        if (session.IsVersus)
            hooks.GiveDeathmatchKeys();
        if (session.ActingIsLocal)
            session.Camera.FollowPlayer();      // back through their own eyes, from watching someone

        // Their screen and status bar, if they're this machine's player
        if (session.ActingIsLocal)
            video.ClearPaletteShifts();     // the red of the killing blow
        statusBar.DrawFace();
        statusBar.DrawHealth();
        statusBar.DrawArmor();
        weapons.DrawWeapon();
        weapons.DrawAmmo();
        statusBar.DrawKeys();
        hooks.Spawned?.Invoke(player, "respawn");
    }

    /// <summary>What messages call a player: their name, else "Player n" (the class's tag, alone)</summary>
    internal string? PlayerName(Entities.Actors.PlayerPawn pawn) =>
        session.GameMode == GameMode.Single ? null : pawn.State.Name ?? $"Player {pawn.State.Number + 1}";

    /// <summary>The acting player starts the game afresh as <paramref name="playerClass"/>: their loadout, health and lives</summary>
    internal void StartPlayer(string playerClass)
    {
        var playerstate = session.PlayerState;
        playerstate.PlayerClass = playerClass;
        stats.GiveStartingInventory();

        playerstate.Health = classes.StartingHealth;
        playerstate.Lives = classes.StartingLives;
        playerstate.NextExtra = stats.ExtraLifeScore;
    }

    /// <summary>
    /// A player joins a network game at this frame, on every machine alike (Networking/MidGameJoin.cs):
    /// at the start, or the nearest free spot to it, or (deathmatch) somewhere random
    /// </summary>
    internal void AddNetPlayer(Networking.NetJoin join)
    {
        var players = session.Players;
        if (join.Index != players.Count)
        {
            Console.WriteLine($"Network: {join.Name} joined as player {join.Index + 1}, but there are {players.Count} players here");
            return;
        }

        int team = session.IsTeamGame ? join.Team & 1 : -1;      // a team game: on their team, in its color
        var p = new Entities.PlayerState
        {
            Number = join.Index, Name = join.Name, Team = team,
            Color = hooks.PlayerColor(team >= 0 ? GameModes.TeamNames[team] : join.Color),
        };
        players.Add(p);
        Entities.Actors.PlayerPawn pawn;
        using (session.ActAs(p))
        {
            StartPlayer(classes.FindPlayerClass(join.PlayerClass) ?? classes.DefaultPlayerClass);
            pawn = map.CreatePlayer(p);
            if (session.IsVersus)
            {
                var (x, y, angle) = hooks.DeathmatchSpot(pawn);
                think.SpawnPlayer(x, y, angle);
                hooks.GiveDeathmatchKeys();
            }
            else if (map.PlayerStart is { } start)
            {
                var taken = map.Players.Where(other => other != pawn && other.State.Health > 0)
                    .Select(other => ((int)other.TileX, (int)other.TileY)).ToList();
                var (x, y) = taken.Contains((start.TileX, start.TileY))
                    ? FreeTileNear(start.TileX, start.TileY, taken) ?? (start.TileX, start.TileY)
                    : (start.TileX, start.TileY);
                think.SpawnPlayer(x, y, start.Angle);
            }
        }
        hooks.ConnectAreas();     // SpawnPlayer started the areas afresh; it's mid-level
        hudMessages.Show(HudMessageKind.Other, $"{join.Name} joined the game");
        using (session.ActAs(p))
            hooks.Spawned?.Invoke(pawn, "join");
    }
}