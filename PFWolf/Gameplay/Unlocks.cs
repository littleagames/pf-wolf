using PFWolf.Assets;

namespace PFWolf.Gameplay;

/*
=============================================================================

UNLOCKS

What the player has unlocked, kept from game to game in progress.cfg in the game's config folder:
one name a line. An episode with `starts-locked: true` in game-info is shown greyed out (as its
`locked-name`, if it has one) and can't be picked until its key (EP02) is unlocked. An episode's
`level-select` menu (once its `level-select-after` name is unlocked, if it has one) greys out a
map that `starts-locked` the same way until the map's name (DT03) is unlocked.

Names are unlocked by A_Unlock("name"[, "message"]) (an actor's, such as an item's Pickup state,
or a switch's), Lua game.unlock(name), or the unlock console command; any name can be unlocked,
so a script can keep its own. A demo playing back unlocks nothing. Anything unlocked during a
game is told on the next level-end (or victory) screen, where its intermission.yaml has an
`unlocked` value ("New Levels Unlocked!", as its format).

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

    /// <summary>Whether a level can be picked on its episode's level select: not starts-locked, or unlocked since</summary>
    public bool IsOpen(string mapName, MapInfo map) => !map.StartsLocked || IsUnlocked(mapName);

    /// <summary>Whether an episode has a level select menu now: it lists levels, and its level-select-after (if any) is unlocked</summary>
    public bool HasLevelSelect(EpisodeInfo episode) =>
        episode.LevelSelect.Count > 0 && (string.IsNullOrWhiteSpace(episode.LevelSelectAfter) || IsUnlocked(episode.LevelSelectAfter));

    /// <summary>The names game-info locks: its starts-locked episodes' keys and levels' names, and level-select-after names</summary>
    public IEnumerable<string> Lockable() =>
        gameInfo().Episodes.Where(e => e.Value.StartsLocked).Select(e => e.Key)
            .Concat(gameInfo().Episodes.Values.Select(e => e.LevelSelectAfter).OfType<string>().Where(name => name.Trim().Length > 0))
            .Concat(gameInfo().Maps.Where(m => m.Value.StartsLocked).Select(m => m.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <returns>Whether it wasn't already (and was written)</returns>
    public bool Unlock(string name)
    {
        Load();
        name = name.Trim();
        if (name.Length == 0 || !unlocked.Add(name))
            return false;
        order.Add(name);
        Save();
        news = true;
        return true;
    }

    bool news;

    /// <summary>
    /// Whether anything's been unlocked since last asked (or since ForgetNews), for the level-end
    /// screen's intermission.yaml `unlocked` value ("New Levels Unlocked!"); asking clears it
    /// </summary>
    public bool TakeNews()
    {
        bool any = news;
        news = false;
        return any;
    }

    /// <summary>A new game starts: what was unlocked before it isn't news on its first tally</summary>
    public void ForgetNews() => news = false;

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
