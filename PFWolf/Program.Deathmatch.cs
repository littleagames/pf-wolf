using PFWolf.Entities.Actors;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                    DEATHMATCH

    Players against each other. Everyone comes in (and back) at a random spot: one of the
    map's deathmatch starts (mapdefs player-starts with `deathmatch: true`), else any open
    floor, as far from the others as can be had. Each carries every key, so no door is shut to
    anyone. A weapon on the floor stays there for everyone without it yet; anything else taken
    comes back after ITEMRESPAWNTICS (with item-respawn on). Killing another player is a frag
    (killing yourself takes one away); the first to the frag limit, or whoever leads when the
    time runs out, ends the match. The map's enemies are only there with monsters on.

    All of it runs the same on every machine (US_RndT, the level's clock): it's the game itself.

    =============================================================================
    */

    /// <summary>How a deathmatch is played: the host's choices in the lobby</summary>
    internal sealed record NetRules(bool Monsters, int FragLimit, int TimeLimitMinutes, bool ItemRespawn);

    internal static readonly NetRules DefaultRules = new(Monsters: false, FragLimit: 20, TimeLimitMinutes: 0, ItemRespawn: true);

    /// <summary>The rules of the deathmatch being played</summary>
    internal static NetRules netrules = DefaultRules;

    /// <summary>How long a taken item takes to come back, in a deathmatch (tics)</summary>
    const int ITEMRESPAWNTICS = 30 * 70;

    /// <summary>The match has been won (the frag limit) or run out of time: the level ends, for the scoreboard</summary>
    internal static bool matchover;

    // Items taken, to come back: their class, where, and when (gamestate.TimeCount)
    sealed record ItemRespawn(string ClassName, int TileX, int TileY, int At);
    static readonly List<ItemRespawn> itemrespawns = [];

    /// <summary>A new level of a deathmatch: nothing waiting to come back, and its enemies only with monsters on</summary>
    internal static void DeathmatchLevelSetup()
    {
        itemrespawns.Clear();
        _mapManager.NoMonsters = gamemode == GameMode.Deathmatch && !netrules.Monsters;
    }

    /// <summary>
    /// A spot for a player to come into a deathmatch at: a free deathmatch start, else any
    /// free open floor; of those, one at random among the ones at least 8 tiles from every
    /// other living player when there are any
    /// </summary>
    static (int x, int y, int angle) DeathmatchSpot(PlayerPawn self)
    {
        var others = _mapManager.Players
            .Where(pawn => pawn != self && pawn.Active == activetypes.ac_yes && pawn.State.health > 0)
            .ToList();
        int NearestOther(int x, int y) =>
            others.Count == 0 ? int.MaxValue : others.Min(pawn => Math.Max(Math.Abs(pawn.TileX - x), Math.Abs(pawn.TileY - y)));

        List<(int x, int y, int angle)> spots = _mapManager.DeathmatchStarts.Count > 0
            ? _mapManager.DeathmatchStarts.Where(s => IsFreeFloor(s.TileX, s.TileY)).Select(s => (s.TileX, s.TileY, s.Angle)).ToList()
            : [];
        if (spots.Count == 0)
        {
            for (int y = 1; y < _mapManager.mapheight - 1; y++)
                for (int x = 1; x < _mapManager.mapwidth - 1; x++)
                    if (IsFreeFloor(x, y) && !_mapManager.ShootableActorsAt(x, y).Any() && _mapManager.PatrolPointAt(x, y) == null)
                        spots.Add((x, y, -1));
        }
        if (spots.Count == 0)
            return (self.TileX, self.TileY, self.Angle);

        var far = spots.Where(s => NearestOther(s.x, s.y) >= 8).ToList();
        if (far.Count > 0)
            spots = far;

        var spot = spots[((US_RndT() << 8) | US_RndT()) % spots.Count];
        int angle = spot.angle >= 0 ? spot.angle : (US_RndT() & 3) * 90;     // a random way, where the spot says none
        return (spot.x, spot.y, angle);
    }

    /// <summary>The acting player carries every key (in a deathmatch, no door is shut to anyone)</summary>
    static void GiveDeathmatchKeys()
    {
        GiveAllKeys();      // (Program.ConsoleCommands.cs)
        DrawKeys();
    }

    /// <summary>Whether the acting player already has the weapon a pickup gives (it then stays, for someone without it)</summary>
    static bool HasWeaponFrom(Weapon pickup)
    {
        var weaponName = pickup.Properties.TryGetValue("weapongiver.weapon", out var given)
            ? given.ToString() ?? pickup.Name
            : pickup.Name;
        return _inventoryManager.Has(weaponName);
    }

    /// <summary>An item has just been taken: it comes back where it was after a while</summary>
    static void QueueItemRespawn(Inventory item) =>
        itemrespawns.Add(new ItemRespawn(item.Name, (int)item.Position.X, (int)item.Position.Y, gamestate.TimeCount + ITEMRESPAWNTICS));

    /// <summary>Each frame of a deathmatch: items coming back, and the time limit</summary>
    internal static void TickDeathmatch()
    {
        for (int i = 0; i < itemrespawns.Count; i++)
        {
            var item = itemrespawns[i];
            if (gamestate.TimeCount < item.At)
                continue;
            itemrespawns.RemoveAt(i--);
            _mapManager.SpawnThing(item.TileX, item.TileY, item.ClassName);
        }

        if (netrules.TimeLimitMinutes > 0 && gamestate.TimeCount >= netrules.TimeLimitMinutes * 60 * 70)
            EndMatch();
    }

    /// <summary>
    /// A player has died in a deathmatch, killed by <paramref name="killer"/>: a frag for
    /// another player who did it, one fewer for killing yourself; the frag limit ends the match
    /// </summary>
    static void CountFrag(Entities.PlayerState victim, Entities.Actors.Actor? killer)
    {
        var by = killer?.Shooter ?? killer;
        if (by is PlayerPawn rival && rival.State != victim)
        {
            rival.State.Frags++;
            using (ActAs(rival.State))
                DrawScore();
            if (netrules.FragLimit > 0 && rival.State.Frags >= netrules.FragLimit)
                EndMatch();
        }
        else if (by is PlayerPawn self && self.State == victim)
        {
            victim.Frags--;
            using (ActAs(victim))
                DrawScore();
        }
    }

    /// <summary>The deathmatch is over: the level ends, and the scoreboard shows who won</summary>
    static void EndMatch()
    {
        if (matchover)
            return;
        matchover = true;
        playstate = playstatetypes.ex_abort;
    }

    /*
    ===============
    = The scoreboard
    ===============
    */

    /// <summary>Whether the scoreboard is up over the view (the `scoreboard` command toggles it)</summary>
    static bool showscoreboard;

    /// <summary>The players, best first: by frags in a deathmatch, else by score</summary>
    static List<Entities.PlayerState> RankedPlayers() =>
        players.Where(p => !p.Gone)
            .OrderByDescending(p => gamemode == GameMode.Deathmatch ? p.Frags : p.score)
            .ThenBy(p => p.Number)
            .ToList();

    /// <summary>
    /// The scoreboard in a box at (x, y): each player's name, and their frags (deathmatch) or
    /// score and kills (co-op), this machine's player highlighted
    /// </summary>
    static void DrawScoreboard(int y, string? title = null)
    {
        var ranked = RankedPlayers();
        const int X = 40, W = 240, Row = 10;
        int h = 14 + Row * ranked.Count + (title != null ? Row : 0);
        _videoManager.Bar(X, y, W, h, "BKGDCOLOR");
        DrawOutline(X, y, W, h, "DEACTIVE", "HIGHLIGHT");

        int ty = y + 4;
        if (title != null)
        {
            CenteredText(X, W, ty, new Fonts.TextStyle(SMALL_FONT, "READHCOLOR")).CPrint(title);
            ty += Row;
        }
        bool dm = gamemode == GameMode.Deathmatch;
        TextAt(X + 6, ty, new Fonts.TextStyle(SMALL_FONT, "READCOLOR")).Print(dm ? "Player" : "Player");
        TextAt(X + 150, ty, new Fonts.TextStyle(SMALL_FONT, "READCOLOR")).Print(dm ? "Frags" : "Score");
        if (!dm)
            TextAt(X + 200, ty, new Fonts.TextStyle(SMALL_FONT, "READCOLOR")).Print("Kills");
        ty += Row;

        foreach (var p in ranked)
        {
            var style = new Fonts.TextStyle(SMALL_FONT, ReferenceEquals(p, localplayer) ? "HIGHLIGHT" : "TEXTCOLOR");
            var name = p.Name ?? $"Player {p.Number + 1}";
            if (p.health <= 0)
                name += " (dead)";
            TextAt(X + 6, ty, style).Print(FitText(name, 140, SMALL_FONT));
            TextAt(X + 150, ty, style).Print((dm ? p.Frags : p.score).ToString());
            if (!dm)
                TextAt(X + 200, ty, style).Print(p.Kills.ToString());
            ty += Row;
        }
    }

    /// <summary>The end of a deathmatch: the final scoreboard, until a key</summary>
    static void ShowFinalScoreboard()
    {
        var winner = RankedPlayers().FirstOrDefault();
        ClearMemory();
        _videoManager.FadeOut();
        _videoManager.ClearScreen(0);
        DrawScoreboard(40, winner == null ? "Match over" : $"{winner.Name ?? $"Player {winner.Number + 1}"} wins!");
        _videoManager.Update();
        _videoManager.FadeIn();
        _inputManager.ClearKeysDown();
        _inputManager.UserInput(Timing.TickBase * 15);
        _videoManager.FadeOut();
    }

    /// <summary>`scoreboard`: shows or hides the scoreboard over the view (bind it to a key)</summary>
    static void Cmd_Scoreboard(string[] args)
    {
        showscoreboard = args.Length > 0 ? args[0] == "1" : !showscoreboard;
    }
}
