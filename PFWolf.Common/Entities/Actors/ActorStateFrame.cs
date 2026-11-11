namespace PFWolf.Entities.Actors;

/// <summary>
/// One frame of an actor's state, as <see cref="ActorStateResolver"/> makes them from a class's
/// actordefs states: a sprite frame shown for a number of tics, what it runs, and the frame after
/// it. Shared by the game, which plays them, and the editor, which shows them.
/// </summary>
public class ActorStateFrame
{
    public required string StateName { get; init; }
    public required string Sprite { get; init; }
    public required string FrameLetter { get; init; }
    // ZDoom-style: -1 holds the frame forever, 0 ends it the moment it's entered (its Action
    // runs and Next follows in the same tic), anything else counts down that many 70Hz tics.
    public short TicTime { get; init; }
    public bool HoldsForever => TicTime < 0;
    public List<string> Modifiers { get; init; } = [];

    // The "bright" modifier (or lighten(bright)): the frame is drawn at full light, unshaded
    private bool? _bright;
    public bool Bright => _bright ??= Modifiers.Contains(BrightModifier, StringComparer.OrdinalIgnoreCase)
        || string.Equals(LightenLevel, BrightModifier, StringComparison.OrdinalIgnoreCase);     // lighten(bright): the game's Lighting.BrightLevel

    // The "lighten(level)" modifier: the frame darkens less with distance, as the actor's
    // `lighten` property (Blake Stone's explosions, nearly unshaded): a number of fade steps or a
    // game-info lighten-levels name (Lighting.LevelSteps); null when it has none
    private bool _lightenRead;
    private string? _lighten;
    public string? LightenLevel
    {
        get
        {
            if (!_lightenRead)
                (_lighten, _lightenRead) = (Modifiers.Select(LightenArgument).FirstOrDefault(level => level != null), true);
            return _lighten;
        }
    }

    /// <summary>The frame modifiers the engine reads (the actordefs reference's modifiers); any other is ignored, with a warning</summary>
    public const string BrightModifier = "bright";
    public static readonly IReadOnlySet<string> KnownModifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { BrightModifier };

    public static bool IsKnownModifier(string modifier) => KnownModifiers.Contains(modifier) || LightenArgument(modifier) != null;

    // lighten(level)'s level, else null
    static string? LightenArgument(string modifier) =>
        modifier.StartsWith("lighten(", StringComparison.OrdinalIgnoreCase) && modifier.EndsWith(')')
            && modifier[8..^1].Trim() is { Length: > 0 } level ? level : null;

    // The state's `light:`, overriding the actor's light on this frame; null keeps the actor's
    public StateLight? Light { get; init; }

    // The state's `particles:`, the emitters running on this frame in place of the actor's; null keeps the actor's
    public List<string>? Particles { get; init; }

    public string? Think { get; init; }
    public string? Action { get; init; }
    public ActorStateFrame? Next { get; internal set; }

    /// <summary>The frame's picture seen from a view: 0 its single view, 1 (front) to 8 turning ("GARDA1")</summary>
    public string ShapeName(int view) => ShapeNames(Sprite, FrameLetter)[view];

    private static readonly Dictionary<(string, string), string[]> _shapeNames = [];

    /// <summary>
    /// A frame's picture names, view 0 (its single view) to 8: "GARDA0" to "GARDA8". Made once,
    /// as the renderer asks for them every frame for everything it draws.
    /// </summary>
    public static string[] ShapeNames(string sprite, string frameLetter)
    {
        if (!_shapeNames.TryGetValue((sprite, frameLetter), out var names))
            _shapeNames[(sprite, frameLetter)] = names = MakeShapeNames(sprite, frameLetter);
        return names;
    }

    // Apart from ShapeNames, whose every call would otherwise make the lambda's closure
    private static string[] MakeShapeNames(string sprite, string frameLetter) =>
        Enumerable.Range(0, 9).Select(view => $"{sprite}{frameLetter}{view}").ToArray();
}
