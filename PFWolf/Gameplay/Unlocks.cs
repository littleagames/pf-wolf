using PFWolf.Assets;

namespace PFWolf.Gameplay;

/*
=============================================================================

UNLOCKS

What the player has unlocked, kept from game to game in progress.cfg in the game's config folder:
one name a line. An episode with `starts-locked: true` in game-info is shown greyed out (as its
`locked-name`, if it has one) and can't be picked until its key (EP02) is unlocked.

Names are unlocked by A_Unlock("name"[, "message"]) (an actor's, such as an item's Pickup state,
or a switch's), Lua game.unlock(name), or the unlock console command; any name can be unlocked,
so a script can keep its own. A demo playing back unlocks nothing.

=============================================================================
*/

internal sealed class Unlocks(Func<string> path, Func<GameInfoAsset> gameInfo)
{
    public const string FileName = "progress.cfg";

    readonly HashSet<string> unlocked = new(StringComparer.OrdinalIgnoreCase);
    bool loaded;

    /// <summary>The names unlocked, in the order they were</summary>
    public IReadOnlyList<string> Unlocked
    {
        get
        {
            Load();
            return order;
        }
    }
    readonly List<string> order = [];

    public bool IsUnlocked(string name)
    {
        Load();
        return unlocked.Contains(name.Trim());
    }

    /// <summary>Whether an episode can be played: not starts-locked, or unlocked since</summary>
    public bool IsOpen(EpisodeInfo episode) =>
        !episode.StartsLocked || EpisodeKey(episode) is not { } key || IsUnlocked(key);

    /// <summary>The names game-info locks: its starts-locked episodes' keys</summary>
    public IEnumerable<string> Lockable() =>
        gameInfo().Episodes.Where(e => e.Value.StartsLocked).Select(e => e.Key);

    /// <returns>Whether it wasn't already (and was written)</returns>
    public bool Unlock(string name)
    {
        Load();
        name = name.Trim();
        if (name.Length == 0 || !unlocked.Add(name))
            return false;
        order.Add(name);
        Save();
        return true;
    }

    /// <returns>Whether it was unlocked</returns>
    public bool Lock(string name)
    {
        Load();
        name = name.Trim();
        if (!unlocked.Remove(name))
            return false;
        order.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        Save();
        return true;
    }

    string? EpisodeKey(EpisodeInfo episode) =>
        gameInfo().Episodes.FirstOrDefault(e => ReferenceEquals(e.Value, episode)).Key;

    void Load()
    {
        if (loaded)
            return;
        loaded = true;

        var file = path();
        if (!File.Exists(file))
            return;
        try
        {
            foreach (var line in File.ReadAllLines(file).Select(l => l.Trim()))
                if (line.Length > 0 && !line.StartsWith('#') && unlocked.Add(line))
                    order.Add(line);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't read {file} ({e.Message}); nothing is unlocked.");
        }
    }

    void Save()
    {
        var file = path();
        try
        {
            File.WriteAllLines(file,
            [
                "# What's been unlocked in this game (game-info's starts-locked episodes, and any name a",
                "# script or A_Unlock gives), one a line. The unlock and lock console commands change it.",
                .. order,
            ]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't write {file}: {e.Message}");
        }
    }
}
