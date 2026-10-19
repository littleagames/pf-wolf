namespace PFWolf.Assets;

/// <summary>The kind of file a level was read from, which is how the editor saves it again</summary>
public enum MapFormat
{
    /// <summary>Planes: a GAMEMAPS level, or a binary maps/NAME.wad (WDC3.1 PLANES)</summary>
    Planes,

    /// <summary>ECWolf's text format (UWMF): a TEXTMAP in maps/NAME.wad, or maps/NAME.uwmf</summary>
    Uwmf,
}

/// <summary>
/// A thing placed where the object plane can't say it (a UWMF level's): a second thing on a
/// tile, one off a tile's middle, one on a set of skills min-skill can't give, or one of a class
/// the mapdefs have no code for. Spawned by class after the plane's things.
/// </summary>
/// <param name="X">Where it stands, in tiles (29.5 is the middle of tile 29)</param>
/// <param name="Angle">The way it faces: 0 east, 90 north, 180 west, 270 south</param>
/// <param name="Skills">Which skills it's on, a bit each from the easiest (bit 0); the last bit stands for every harder skill</param>
public sealed record MapThing(string Class, double X, double Y, int Angle, bool Ambush = false, bool Patrol = false, int Skills = MapThing.AllSkills)
{
    public const int AllSkills = 0b1111;

    public int TileX => (int)Math.Floor(X);
    public int TileY => (int)Math.Floor(Y);

    /// <summary>Whether it's placed on the skill at this place in game-info's skills</summary>
    public bool OnSkill(int skill) => (Skills & (1 << Math.Clamp(skill, 0, 3))) != 0;
}

/// <summary>What a level's own file says about it, which its game-info entry overrides</summary>
public sealed record MapLevelDefaults
{
    /// <summary>The sky (UWMF's sky), or null</summary>
    public string? Sky { get; init; }

    /// <summary>How many stories tall its walls are (UWMF's plane depth / 64), or null</summary>
    public int? WallStories { get; init; }
}

public record MapAsset : Asset
{
    public ushort Height { get; set; }
    public ushort Width { get; set; }
    public string Name { get; set; } = "";

    /// <summary>The planes, each Width x Height tiles, row by row</summary>
    public ushort[][] MapData { get; set; } = [];

    /// <summary>The kind of file it came from</summary>
    public MapFormat Format { get; set; } = MapFormat.Planes;

    /// <summary>
    /// A UWMF level as read, until it's turned into planes (that needs the running game's
    /// mapdefs, so the AssetManager does it once everything's loaded)
    /// </summary>
    public Loaders.Uwmf.UwmfMap? Uwmf { get; set; }

    /// <summary>A UWMF level not yet turned into planes</summary>
    public bool NeedsTranslation => Uwmf != null && MapData.Length == 0;

    /// <summary>
    /// The level's own walls, doors, flats, things and triggers, laid over the game's mapdefs
    /// while it's played (a UWMF level's tiles and sectors the mapdefs have nothing like), and its
    /// own area count. Null: the mapdefs alone.
    /// </summary>
    public MapObjectTranslationAsset? LocalDefs { get; set; }

    /// <summary>Things the object plane can't hold (see <see cref="MapThing"/>)</summary>
    public List<MapThing> ExtraThings { get; set; } = [];

    /// <summary>What the level's file says about it, under its game-info entry</summary>
    public MapLevelDefaults Defaults { get; set; } = new();

    /// <summary>A copy whose planes can be changed without touching this one</summary>
    public MapAsset DeepCopy() => this with
    {
        MapData = MapData.Select(plane => (ushort[])plane.Clone()).ToArray(),
        ExtraThings = [.. ExtraThings],
    };
}
