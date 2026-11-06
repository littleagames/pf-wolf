using PFWolf.Assets;
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

/// <summary>What puzzle switches need from the game</summary>
/// <param name="OpenPuzzle">Opens a puzzle for the acting player: its name, args, the switch's tile and tag, and what to do with its result; false if it didn't open</param>
/// <param name="Message">Shows the acting player a message (a $NAME language key or the text)</param>
internal sealed record PuzzleSwitchHooks(
    Func<string, string[], int, int, ushort, Action<string>, bool> OpenPuzzle,
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
    /// and the puzzle's opened (its success calling <paramref name="throwSwitch"/>, its fail and
    /// cancel running <paramref name="runActions"/> with those lists), unless it's locked out, or
    /// broken for good, or the player's skip item throws it at once.
    /// </summary>
    internal bool Use(MapSwitchTranslation sw, int x, int y, bool patch, ushort tag, Action throwSwitch, Action<IReadOnlyList<string>> runActions)
    {
        if (sw.Puzzle.Length == 0)
            return false;

        DropStale();
        var state = Find(x, y, patch);
        if (state != null && state.ReadyAt > session.GameState.TimeCount)
        {
            if (sw.RetryMessage.Length > 0)
                hooks.Message(sw.RetryMessage);
            return true;
        }

        if (sw.SkipItem.Length > 0 && inventory.Has(sw.SkipItem))
        {
            inventory.Take(sw.SkipItem, 1);
            Forget(x, y, patch);
            throwSwitch();
            return true;
        }

        var args = sw.PuzzleArgs.Select(arg => $"{arg.Key}={arg.Value}").ToArray();
        hooks.OpenPuzzle(sw.Puzzle, args, x, y, tag, result => Ended(sw, x, y, patch, result, throwSwitch, runActions));
        return true;
    }

    void Ended(MapSwitchTranslation sw, int x, int y, bool patch, string result, Action throwSwitch, Action<IReadOnlyList<string>> runActions)
    {
        switch (result)
        {
            case "success":
                Forget(x, y, patch);
                throwSwitch();
                break;
            case "fail":
                runActions(sw.Fail);
                Failed(sw, x, y, patch);
                break;
            case "cancel":
                runActions(sw.Cancel);
                break;
            // a result of the script's own: game.onpuzzle sees to it
        }
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

    /// <summary>A loaded game's puzzle switches (SaveGames/PlayerSaveState.cs), on the level just rebuilt</summary>
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
