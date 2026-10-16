using PFWolf.Gameplay;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>
/// unlock and lock: what the player has unlocked from game to game (progress.cfg: Gameplay/Unlocks.cs),
/// game-info's starts-locked episodes and any name a script or A_Unlock gave
/// </summary>
internal sealed class UnlockCommands(ConsoleManager console, Unlocks unlocks)
{
    internal void Register()
    {
        console.Register("unlock", "Unlocks a name for good: a starts-locked episode's key (EP02), or all of them; on its own, lists what's locked and unlocked.",
            "unlock [name|all]", Cmd_Unlock, Cheat, complete: (_, i) => i == 0 ? ["all", .. unlocks.Lockable()] : []);
        console.Register("lock", "Locks an unlocked name again, or all of them (everything back as a first game has it).",
            "lock <name|all>", Cmd_Lock, complete: (_, i) => i == 0 ? ["all", .. unlocks.Unlocked] : []);
    }

    internal void Cmd_Unlock(string[] args)
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

    internal void Cmd_Lock(string[] args)
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
