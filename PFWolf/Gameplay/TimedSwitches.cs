using PFWolf.Assets;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>A thrown switch waiting to turn back (TimedSwitches)</summary>
/// <param name="X">Its tile</param>
/// <param name="Y">Its tile</param>
/// <param name="Patch">Whether it's a patch's switch (else a switch WallId)</param>
/// <param name="From">The WallId or patch id it was</param>
/// <param name="To">The WallId or patch id it was thrown to</param>
/// <param name="Tag">Its tile's tag, for the linked switches and the reset actions</param>
/// <param name="Dir">The way the player faced as they threw it</param>
/// <param name="Actions">Its reset-actions</param>
/// <param name="Level">The level's tile map it was thrown on (a reload makes a new one)</param>
internal sealed record TimedSwitch(int X, int Y, bool Patch, int From, int To, ushort Tag, ControlDir Dir,
    IReadOnlyList<string> Actions, ushort[,] Level)
{
    internal int TicsLeft;
}

/// <summary>
/// Switches that turn back by themselves (Midnight Assault's timed switches, 652-654, which
/// came back after 2, 4 or 7 seconds: UpdateSwitches). A switch with `reset-tics` is thrown as
/// ever; that many tics later it, and the linked switches thrown with it, go back to what they
/// were, and its `reset-actions` run on its tag (closing the doors it opened, say). While it's
/// thrown its `to` is what's there, so a `to` that isn't a switch can't be used meanwhile.
/// </summary>
internal sealed class TimedSwitches(MapManager map, GameSession session)
{
    const int BitWall = GameConstants.BitWall;
    const int BitDoor = GameConstants.BitDoor;

    readonly List<TimedSwitch> pending = [];

    /// <summary>The switches still waiting to turn back</summary>
    internal IReadOnlyList<TimedSwitch> Pending => pending;

    /// <summary>A switch just thrown from <paramref name="from"/> to <paramref name="to"/>: if it has reset-tics, it turns back after them</summary>
    internal void Thrown(MapSwitchTranslation sw, int x, int y, bool patch, int from, int to, ushort tag, ControlDir dir)
    {
        if (sw.ResetTics <= 0)
            return;
        pending.RemoveAll(p => p.X == x && p.Y == y && p.Patch == patch);
        pending.Add(new TimedSwitch(x, y, patch, from, to, tag, dir, sw.ResetActions, map.tilemap) { TicsLeft = sw.ResetTics });
    }

    /// <summary>Each tic: those whose time is up turn back. Ones from an earlier load of the level are dropped.</summary>
    internal void Tick(uint tics)
    {
        pending.RemoveAll(p => !ReferenceEquals(p.Level, map.tilemap));
        foreach (var sw in pending.ToList())
        {
            sw.TicsLeft -= (int)tics;
            if (sw.TicsLeft > 0)
                continue;
            pending.Remove(sw);
            TurnBack(sw);
        }
    }

    void TurnBack(TimedSwitch sw)
    {
        // It, and the linked ones still thrown the same way
        foreach (var (x, y) in map.TaggedTiles(sw.Tag).Prepend((sw.X, sw.Y)).Distinct())
        {
            if (sw.Patch)
            {
                if (map.GetPatch(x, y) != sw.To)
                    continue;
                map.SetPatch(x, y, (byte)sw.From);
            }
            else
            {
                var tile = map.tilemap[x, y];
                if ((tile & BitDoor) != 0 || (tile & ~BitWall) != sw.To)
                    continue;
                map.tilemap[x, y] = (ushort)(sw.From | (tile & BitWall));
            }
            map.RestartAnimations(x, y, session.GameState.TimeCount);
        }

        if (sw.Actions.Count == 0 || session.PlayerState.Pawn is not { } player)
            return;
        var activation = new Entities.TriggerActivation(sw.X, sw.Y, sw.Dir, player, sw.Tag);
        foreach (var action in sw.Actions)
            Entities.MapTriggerRegistry.Invoke(action, activation);
    }
}
