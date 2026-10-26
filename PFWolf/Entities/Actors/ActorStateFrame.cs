using PFWolf;

namespace PFWolf.Entities.Actors;

internal class ActorStateFrame
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
        || Rendering.Lighting.IsBrightLevel(LightenLevel);

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
    internal const string BrightModifier = "bright";
    internal static readonly IReadOnlySet<string> KnownModifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { BrightModifier };

    internal static bool IsKnownModifier(string modifier) => KnownModifiers.Contains(modifier) || LightenArgument(modifier) != null;

    // lighten(level)'s level, else null
    static string? LightenArgument(string modifier) =>
        modifier.StartsWith("lighten(", StringComparison.OrdinalIgnoreCase) && modifier.EndsWith(')')
            && modifier[8..^1].Trim() is { Length: > 0 } level ? level : null;

    // The state's `light:`, overriding the actor's light on this frame; null keeps the actor's
    public StateLight? Light { get; init; }

    public string? Think { get; init; }
    public string? Action { get; init; }
    public ActorStateFrame? Next { get; internal set; }

    public string GetShapeName(ObjDir dir)
    {
        var frame = dir == ObjDir.NoDir ? 0 : (int)dir;
        return ShapeNames(Sprite, FrameLetter)[frame];
    }

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
