namespace PFWolf.Shading;

/// <summary>
/// Named lighten levels (game-info lighten-levels): how many fade steps less something darkens
/// with distance, by name, for actordefs `lighten` and the lighten(level) frame modifier.
/// "bright" is always unshaded and can't be set. Changes made with <see cref="Change"/> (the
/// game's `lighten` command) stay over the game-info levels when they're set again.
/// </summary>
public sealed class LightenLevelTable
{
    /// <summary>The name that draws something unshaded, as the bright flag does</summary>
    public const string BrightLevel = "bright";

    private readonly Dictionary<string, int> _levels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _changes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unknown = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where a name with no level is reported, once each (the console by default)</summary>
    public Action<string> Warn { get; set; } = Console.WriteLine;

    public static bool IsBrightLevel(string? level) => string.Equals(level, BrightLevel, StringComparison.OrdinalIgnoreCase);

    /// <summary>The named levels as they are now (game-info's, with any changed)</summary>
    public IReadOnlyDictionary<string, int> Levels => _levels;

    /// <summary>Takes the game's named levels (game-info lighten-levels), keeping the changes</summary>
    public void Set(IReadOnlyDictionary<string, int> fromGameInfo)
    {
        _levels.Clear();
        foreach (var (name, steps) in fromGameInfo)
            _levels[name] = steps;
        foreach (var (name, steps) in _changes)
            _levels[name] = steps;
    }

    /// <summary>Changes a named level for as long as this table lasts</summary>
    public void Change(string name, int steps)
    {
        _changes[name] = _levels[name] = steps;
    }

    /// <summary>A lighten level's fade steps: a number, a name's (warned of once if there's no such name), else 0</summary>
    public int Steps(string? level)
    {
        if (string.IsNullOrEmpty(level) || IsBrightLevel(level))
            return 0;
        if (int.TryParse(level, out var steps))
            return steps;
        if (_levels.TryGetValue(level, out steps))
            return steps;
        if (_unknown.Add(level))
            Warn($"Unknown lighten level '{level}': give it in game-info lighten-levels, or use a number of fade steps");
        return 0;
    }
}

/// <summary>Whether a frame is drawn unshaded, and how much less it darkens with distance, as the game draws actors</summary>
public static class ActorShadingRules
{
    /// <summary>
    /// Unshaded: the frame has the bright modifier (or lighten(bright)), the actor the bright flag,
    /// or its class `lighten: bright`
    /// </summary>
    public static bool IsBright(bool frameBright, bool hasBrightFlag, bool hasClassLighten, string? classLighten) =>
        frameBright || hasBrightFlag || hasClassLighten && LightenLevelTable.IsBrightLevel(classLighten);

    /// <summary>
    /// How many fade steps (of <see cref="ShadeMath.FadeSteps"/>) less an actor darkens with
    /// distance: its class's `lighten` (Blake Stone's lamps, lit a little rather than drawn
    /// unshaded) or its frame's lighten(level) modifier (an explosion), whichever is more when it
    /// has both. Below 0 is darker (a flickering light's dim frames). A class with a `lighten`
    /// key counts as having one even when its value is empty (0).
    /// </summary>
    public static int Lighten(bool hasClassLighten, string? classLighten, string? frameLighten, LightenLevelTable levels)
    {
        if (frameLighten == null)
            return hasClassLighten ? levels.Steps(classLighten) : 0;
        return hasClassLighten ? Math.Max(levels.Steps(classLighten), levels.Steps(frameLighten)) : levels.Steps(frameLighten);
    }
}
