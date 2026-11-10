namespace PFWolf.Fonts;

/// <summary>
/// The fonts the engine prints in by name where nothing in the game's definitions names one,
/// shared by the game and the editor's font window (its "Used by")
/// </summary>
public static class FontNames
{
    /// <summary>Wolf3D's small font: the console, the automap and most text</summary>
    public const string Small = "SmallFont";

    /// <summary>Wolf3D's large font: menus</summary>
    public const string Large = "LargeFont";

    /// <summary>The intermission and victory screens' letters and numbers</summary>
    public const string Intermission = "IntermissionFont";

    /// <summary>What the engine itself prints in each of these, for the editor to show</summary>
    public static IReadOnlyDictionary<string, string> EngineUses { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [Small] = "The engine: the console, the automap, and text where nothing names a font",
        [Large] = "The engine: menus where the menu names no font",
        [Intermission] = "The engine: the intermission and victory screens",
    };
}
