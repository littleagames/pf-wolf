using PFWolf.Entities.Actors;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>How a deathmatch is played: the host's choices in the lobby</summary>
internal sealed record NetRules(bool Monsters, int FragLimit, int TimeLimitMinutes, bool ItemRespawn);

// An item taken, to come back: its class, where, and when (gamestate.TimeCount)
internal sealed record ItemRespawn(string ClassName, int TileX, int TileY, int At);

/// <summary>What a deathmatch needs from the game</summary>
/// <param name="IsFreeFloor">Open floor nobody stands on</param>
/// <param name="GiveAllKeys">The acting player carries every key</param>
/// <param name="SetPlayState">Ends the level the way it says</param>
internal sealed record DeathmatchHooks(Func<int, int, bool> IsFreeFloor, Action GiveAllKeys, Action<playstatetypes> SetPlayState);

/// <summary>
/// Players against each other. Everyone comes in (and back) at a random spot: one of the
/// map's deathmatch starts (mapdefs player-starts with `deathmatch: true`), else any open
/// floor, as far from the others as can be had. Each carries every key, so no door is shut to
/// anyone. A weapon on the floor stays there for everyone without it yet; anything else taken
/// comes back after ITEMRESPAWNTICS (with item-respawn on). Killing another player is a frag
/// (killing yourself takes one away); the first to the frag limit, or whoever leads when the
/// time runs out, ends the match. The map's enemies are only there with monsters on.
///
/// All of it runs the same on every machine (US_RndT, the level's clock): it's the game itself.
/// </summary>
internal sealed class Deathmatch(
    MapManager map,
    InventoryManager inventory,
    StatusBarDrawing statusBar,
    GameSession session,
    DeathmatchHooks hooks)
{
    internal static readonly NetRules DefaultRules = new(Monsters: false, FragLimit: 20, TimeLimitMinutes: 0, ItemRespawn: true);

    /// <summary>The rules of the deathmatch being played</summary>
    internal NetRules netrules = DefaultRules;

    /// <summary>How long a taken item takes to come back, in a deathmatch (tics)</summary>
    internal const int ITEMRESPAWNTICS = 30 * 70;

    /// <summary>The match has been won (the frag limit) or run out of time: the level ends, for the scoreboard</summary>
    internal bool matchover;

    // Items taken, to come back
    internal readonly List<ItemRespawn> itemrespawns = [];

    /// <summary>A new level of a deathmatch: nothing waiting to come back, and its enemies only with monsters on</summary>
    internal void DeathmatchLevelSetup()
    {
        itemrespawns.Clear();
        map.NoMonsters = session.gamemode == GameMode.Deathmatch && !netrules.Monsters;
    }

    /// <summary>
    /// A spot for a player to come into a deathmatch at: a free deathmatch start, else any
    /// free open floor; of those, one at random among the ones at least 8 tiles from every
    /// other living player when there are any
    /// </summary>
    internal (int x, int y, int angle) DeathmatchSpot(PlayerPawn self)
    {
        var others = map.Players
            .Where(pawn => pawn != self && pawn.Active == activetypes.ac_yes && pawn.State.health > 0)
            .ToList();
        int NearestOther(int x, int y) =>
            others.Count == 0 ? int.MaxValue : others.Min(pawn => Math.Max(Math.Abs(pawn.TileX - x), Math.Abs(pawn.TileY - y)));

        List<(int x, int y, int angle)> spots = map.DeathmatchStarts.Count > 0
            ? map.DeathmatchStarts.Where(s => hooks.IsFreeFloor(s.TileX, s.TileY)).Select(s => (s.TileX, s.TileY, s.Angle)).ToList()
            : [];
        if (spots.Count == 0)
        {
            for (int y = 1; y < map.mapheight - 1; y++)
                for (int x = 1; x < map.mapwidth - 1; x++)
                    if (hooks.IsFreeFloor(x, y) && !map.ShootableActorsAt(x, y).Any() && map.PatrolPointAt(x, y) == null)
                        spots.Add((x, y, -1));
        }
        if (spots.Count == 0)
            return (self.TileX, self.TileY, self.Angle);

        var far = spots.Where(s => NearestOther(s.x, s.y) >= 8).ToList();
        if (far.Count > 0)
            spots = far;

        var random = session.random;
        var spot = spots[((random.RndT() << 8) | random.RndT()) % spots.Count];
        int angle = spot.angle >= 0 ? spot.angle : (random.RndT() & 3) * 90;     // a random way, where the spot says none
        return (spot.x, spot.y, angle);
    }

    /// <summary>The acting player carries every key (in a deathmatch, no door is shut to anyone)</summary>
    internal void GiveDeathmatchKeys()
    {
        hooks.GiveAllKeys();      // (Program.ConsoleCommands.cs)
        statusBar.DrawKeys();
    }

    /// <summary>Whether the acting player already has the weapon a pickup gives (it then stays, for someone without it)</summary>
    internal bool HasWeaponFrom(Weapon pickup)
    {
        var weaponName = pickup.Properties.TryGetValue("weapongiver.weapon", out var given)
            ? given.ToString() ?? pickup.Name
            : pickup.Name;
        return inventory.Has(weaponName);
    }

    /// <summary>An item has just been taken: it comes back where it was after a while</summary>
    internal void QueueItemRespawn(Inventory item) =>
        itemrespawns.Add(new ItemRespawn(item.Name, (int)item.Position.X, (int)item.Position.Y, session.gamestate.TimeCount + ITEMRESPAWNTICS));

    /// <summary>Each frame of a deathmatch: items coming back, and the time limit</summary>
    internal void TickDeathmatch()
    {
        var gamestate = session.gamestate;
        for (int i = 0; i < itemrespawns.Count; i++)
        {
            var item = itemrespawns[i];
            if (gamestate.TimeCount < item.At)
                continue;
            itemrespawns.RemoveAt(i--);
            map.SpawnThing(item.TileX, item.TileY, item.ClassName);
        }

        if (netrules.TimeLimitMinutes > 0 && gamestate.TimeCount >= netrules.TimeLimitMinutes * 60 * 70)
            EndMatch();
    }

    /// <summary>
    /// A player has died in a deathmatch, killed by <paramref name="killer"/>: a frag for
    /// another player who did it, one fewer for killing yourself; the frag limit ends the match
    /// </summary>
    internal void CountFrag(Entities.PlayerState victim, Entities.Actors.Actor? killer)
    {
        var by = killer?.Shooter ?? killer;
        if (by is PlayerPawn rival && rival.State != victim)
        {
            rival.State.Frags++;
            using (session.ActAs(rival.State))
                statusBar.DrawScore();
            if (netrules.FragLimit > 0 && rival.State.Frags >= netrules.FragLimit)
                EndMatch();
        }
        else if (by is PlayerPawn self && self.State == victim)
        {
            victim.Frags--;
            using (session.ActAs(victim))
                statusBar.DrawScore();
        }
    }

    /// <summary>The deathmatch is over: the level ends, and the scoreboard shows who won</summary>
    internal void EndMatch()
    {
        if (matchover)
            return;
        matchover = true;
        hooks.SetPlayState(playstatetypes.ex_abort);
    }
}
