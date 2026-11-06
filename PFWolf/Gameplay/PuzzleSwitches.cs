using PFWolf.Assets;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>A puzzle switch's fails so far, and when it can be tried again (PuzzleSwitches)</summary>
/// <param name="X">Its tile</param>
/// <param name="Y">Its tile</param>
/// <param name="Patch">Whether it's a patch's switch (else a switch wall)</param>
/// <param name="Level">The level's tile map it's on (a reload makes a new one)</param>
internal sealed record PuzzleSwitchState(int X, int Y, bool Patch, ushort[,] Level)
{
    /// <summary>Fails of its puzzle so far</summary>
    internal int Fails;

    /// <summary>The level clock (GameState.TimeCount) it can be tried again at; int.MaxValue for never</summary>
    internal int ReadyAt;
}

/// <summary>
/// The switch a puzzle was opened from, all its result needs to act (as data, so an open puzzle
/// can be saved and sent to a player joining): a wall's or a patch's switch, its tile, the way
/// the player faced, its tag, the wall or patch id it was, and the keys it'll take if it's thrown
/// </summary>
internal sealed record SwitchPuzzle(bool Patch, int X, int Y, ControlDir Dir, ushort Tag, int Id, List<string> Keys)
{
    internal void Write(BinaryWriter bw)
    {
        bw.Write(Patch); bw.Write(X); bw.Write(Y); bw.Write((byte)Dir); bw.Write(Tag); bw.Write(Id);
        bw.Write(Keys.Count);
        foreach (var key in Keys)
            bw.Write(key);
    }

    internal static SwitchPuzzle Read(BinaryReader br) => new(br.ReadBoolean(), br.ReadInt32(), br.ReadInt32(), (ControlDir)br.ReadByte(),
        br.ReadUInt16(), br.ReadInt32(), [.. Enumerable.Range(0, br.ReadInt32()).Select(_ => br.ReadString())]);
}

/// <summary>What puzzle switches need from the game</summary>
/// <param name="OpenPuzzle">Opens a puzzle for the acting player: its name, args and the switch it's for (its result comes to Ended); false if it didn't open</param>
/// <param name="Throw">Throws a switch as using it does (PlayerUse): flips it, its sound and actions, its keys taken</param>
/// <param name="Message">Shows the acting player a message (a $NAME language key or the text)</param>
internal sealed record PuzzleSwitchHooks(
    Func<string, string[], SwitchPuzzle, bool> OpenPuzzle,
    Action<SwitchPuzzle, MapSwitchTranslation> Throw,
    Action<string> Message);

/// <summary>
/// Switches (a wall's or a patch's) with a `puzzle:`: using one opens the puzzle (Gameplay/Puzzles.cs),
/// and only a success throws it. A fail runs its `fail` actions and may keep it shut for `retry`
/// tics, and after `tries` fails it breaks for good (becoming its `broken` wall or patch). A
/// `skip-item` the player carries throws it with no puzzle, used up. Each switch's fails and
/// lockout are kept per tile, in a save.
/// </summary>
internal sealed class PuzzleSwitches(MapManager map, InventoryManager inventory, GameSession session, PuzzleSwitchHooks hooks)
{
    const int BitWall = GameConstants.BitWall;

    readonly List<PuzzleSwitchState> states = [];

    /// <summary>The switches on this level with fails or a lockout, for a save</summary>
    internal IReadOnlyList<PuzzleSwitchState> States
    {
        get
        {
            DropStale();
            return states;
        }
    }

    /// <summary>
    /// The acting player uses a switch: with no puzzle, false (it's thrown as ever). Else true,
    /// and the puzzle's opened (its result coming to <see cref="Ended"/>), unless it's locked
    /// out, or broken for good, or the player's skip item throws it at once.
    /// </summary>
    internal bool Use(MapSwitchTranslation sw, SwitchPuzzle origin)
    {
        if (sw.Puzzle.Length == 0)
            return false;

        DropStale();
        var state = Find(origin.X, origin.Y, origin.Patch);
        if (state != null && state.ReadyAt > session.GameState.TimeCount)
        {
            if (sw.RetryMessage.Length > 0)
                hooks.Message(sw.RetryMessage);
            return true;
        }

        if (sw.SkipItem.Length > 0 && inventory.Has(sw.SkipItem))
        {
            inventory.Take(sw.SkipItem, 1);
            Forget(origin.X, origin.Y, origin.Patch);
            hooks.Throw(origin, sw);
            return true;
        }

        var args = sw.PuzzleArgs.Select(arg => $"{arg.Key}={arg.Value}").ToArray();
        hooks.OpenPuzzle(sw.Puzzle, args, origin);
        return true;
    }

    /// <summary>
    /// A switch's puzzle has ended, as the player who worked it: a success throws it, a fail runs
    /// its fail actions (and counts toward its lockout and tries), backing out its cancel ones;
    /// a result of the script's own does nothing here. The switch is the one its wall or patch
    /// id names, so it acts as that switch whatever its tile shows by now.
    /// </summary>
    internal void Ended(SwitchPuzzle origin, string result)
    {
        if (SwitchOf(origin) is not { } sw)
            return;
        switch (result)
        {
            case "success":
                Forget(origin.X, origin.Y, origin.Patch);
                hooks.Throw(origin, sw);
                break;
            case "fail":
                RunActions(sw.Fail, origin);
                Failed(sw, origin.X, origin.Y, origin.Patch);
                break;
            case "cancel":
                RunActions(sw.Cancel, origin);
                break;
        }
    }

    // The switch a wall or patch id has (walls.yaml / patches.yaml), if it still has one
    MapSwitchTranslation? SwitchOf(SwitchPuzzle origin)
    {
        var data = map.GetMapData();
        return origin.Patch
            ? data.Patches.TryGetValue(origin.Id, out var patch) ? patch.Switch : null
            : data.Walls.TryGetValue(origin.Id, out var wall) ? wall.Switch : null;
    }

    // A switch's action calls, run on its tag from its tile, as the acting player
    void RunActions(IReadOnlyList<string> actions, SwitchPuzzle origin)
    {
        if (actions.Count == 0)
            return;
        var activation = new Entities.TriggerActivation(origin.X, origin.Y, origin.Dir, session.Player, origin.Tag);
        foreach (var action in actions)
            Entities.MapTriggerRegistry.Invoke(action, activation);
    }

    void Failed(MapSwitchTranslation sw, int x, int y, bool patch)
    {
        var state = Find(x, y, patch);
        if (state == null)
            states.Add(state = new PuzzleSwitchState(x, y, patch, map.TileMap));
        state.Fails++;
        state.ReadyAt = session.GameState.TimeCount + Math.Max(sw.Retry, 0);
        if (sw.Tries <= 0 || state.Fails < sw.Tries)
            return;

        if (sw.Broken <= 0)
        {
            state.ReadyAt = int.MaxValue;   // it never works again
            return;
        }
        Forget(x, y, patch);
        if (patch)
            map.SetPatch(x, y, (byte)Math.Clamp(sw.Broken, 0, 255));
        else
        {
            var tile = map.TileMap[x, y];
            map.TileMap[x, y] = (ushort)(sw.Broken | (tile & BitWall));
        }
        map.RestartAnimations(x, y, session.GameState.TimeCount);
    }

    /// <summary>A loaded game's (or a joined game's) puzzle switches (SaveGames/PlayerSaveState.cs), on the level just rebuilt</summary>
    internal void Restore(IEnumerable<SaveGames.SavedPuzzleSwitch> saved)
    {
        states.Clear();
        foreach (var s in saved)
            states.Add(new PuzzleSwitchState(s.X, s.Y, s.Patch, map.TileMap) { Fails = s.Fails, ReadyAt = s.ReadyAt });
    }

    PuzzleSwitchState? Find(int x, int y, bool patch) => states.Find(s => s.X == x && s.Y == y && s.Patch == patch);

    void Forget(int x, int y, bool patch) => states.RemoveAll(s => s.X == x && s.Y == y && s.Patch == patch);

    // Those from an earlier level, or an earlier load of this one, are gone
    void DropStale() => states.RemoveAll(s => !ReferenceEquals(s.Level, map.TileMap));
}
