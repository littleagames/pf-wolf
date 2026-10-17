using PFWolf.Gameplay;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>
/// unlock and lock: what the player has unlocked from game to game (progress.cfg: Gameplay/Unlocks.cs),
/// game-info's starts-locked episodes and any name a script or A_Unlock gave; records: the best
/// done on each map (records.cfg: Gameplay/LevelRecords.cs)
/// </summary>
internal sealed class UnlockCommands(ConsoleManager console, Unlocks unlocks, LevelRecords? records = null)
{
    internal void Register()
    {
        if (records != null)
            console.Register("records", "Shows the best done on each map finished (or one map's); records clear [map] forgets them.",
                "records [map|clear [map]]", RecordsCommand, complete: (_, i) => i == 0 ? ["clear", .. records.Maps] : []);
        console.Register("unlock", "Unlocks a name for good: a starts-locked episode's key (EP02), or all of them; on its own, lists what's locked and unlocked.",
            "unlock [name|all]", UnlockCommand, Cheat, complete: (_, i) => i == 0 ? ["all", .. unlocks.Lockable()] : []);
        console.Register("lock", "Locks an unlocked name again, or all of them (everything back as a first game has it).",
            "lock <name|all>", LockCommand, complete: (_, i) => i == 0 ? ["all", .. unlocks.Unlocked] : []);
    }

    internal void UnlockCommand(string[] args)
    {
        if (args.Length == 0)
        {
            var lockable = unlocks.Lockable().ToList();
            foreach (var name in lockable)
                console.Print($"{name}: {(unlocks.IsUnlocked(name) ? "unlocked" : "locked")}");
            var others = unlocks.Unlocked.Where(n => !lockable.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
            if (others.Count > 0)
                console.Print($"Also unlocked: {string.Join(", ", others)}");
            if (lockable.Count == 0 && others.Count == 0)
                console.Print("Nothing in this game is locked");
            return;
        }

        if (string.Equals(args[0], "all", StringComparison.OrdinalIgnoreCase))
        {
            int count = unlocks.Lockable().Count(unlocks.Unlock);
            console.Print($"Unlocked {count} more");
            return;
        }

        bool known = unlocks.Lockable().Contains(args[0], StringComparer.OrdinalIgnoreCase);
        console.Print(unlocks.Unlock(args[0])
            ? $"Unlocked {args[0]}{(known ? "" : " (game-info locks nothing by that name)")}"
            : $"{args[0]} was already unlocked");
    }

    internal void RecordsCommand(string[] args)
    {
        if (records == null)
            return;
        if (args.Length > 0 && string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
        {
            var map = args.Length > 1 ? args[1] : null;
            console.Print(records.Clear(map) ? $"Forgot {(map == null ? "every map's record" : $"{map}'s record")}" : "Nothing to forget");
            return;
        }

        var maps = args.Length > 0 ? [args[0]] : records.Maps.ToList();
        if (maps.Count == 0)
            console.Print("No map has been finished yet");
        foreach (var map in maps)
            console.Print(records.Get(map) is { } record ? $"{map}: {record.ToLine()}" : $"{map}: never finished");
    }

    internal void LockCommand(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: lock <name|all>");

        if (string.Equals(args[0], "all", StringComparison.OrdinalIgnoreCase))
        {
            int count = unlocks.Unlocked.ToList().Count(unlocks.Lock);
            console.Print($"Locked {count}");
            return;
        }

        console.Print(unlocks.Lock(args[0]) ? $"Locked {args[0]}" : $"{args[0]} wasn't unlocked");
    }
}
