using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Editing;

namespace PFWolf.Editor.Data;

/// <summary>What a plane 0 value is, by the game pack's mapdefs</summary>
public enum TileKind
{
    Floor,
    Wall,
    Door,
    /// <summary>A value the mapdefs don't know: neither a wall, a door nor a floor code</summary>
    Unknown,
}

/// <summary>A level's shading with every value filled in (the game's ShadingSettings): fade start and end in tiles, max fade in percent, light 0 to 255</summary>
public sealed record ShadingValues(string FadeColor, double FadeStart, double FadeEnd, int MaxFade, int Light, bool InverseFade);

/// <summary>
/// Reads a level's planes the way the game does when it loads one (MapManager.LoadMap): which
/// plane 0 values are walls, doors and floor codes, and what each object-plane value places
/// </summary>
public sealed class MapTiles(GameContent content, MapAsset map, Func<MapProperties>? properties = null)
{
    private MapObjectTranslationAsset Defs => content.MapDefs;
    private MapFloorsTranslation Floors => content.MapDefs.Floors;

    public GameContent Content => content;
    public MapAsset Map => map;

    /// <summary>The level's game-info entry as edited</summary>
    public MapProperties Properties => properties?.Invoke() ?? new MapProperties();

    /// <summary>The light zones that apply to the level: the default map's, with its own over them</summary>
    public Dictionary<int, ZoneProperties> Zones => Properties.EffectiveZones(content.DefaultMapInfo);

    public bool IsAmbush(int x, int y) => Floors.Ambush is { } ambush and >= 0 && this[0, x, y] == ambush;

    /// <summary>
    /// The texture a floor tile shows: its flat-plane floor index's (mapdefs flats), else the
    /// level's default floor; null for the floor color
    /// </summary>
    public string? FloorFlat(int x, int y)
    {
        int index = this[MapConstants.FLATPLANE, x, y] & 0xff;
        if (Defs.Flats.Floor.TryGetValue(index, out var flat))
            return flat;
        return Properties.DefaultFloor ?? content.DefaultMapInfo.DefaultFloor;
    }

    /// <summary>The texture a tile's ceiling shows, like <see cref="FloorFlat"/>: null for the ceiling color</summary>
    public string? CeilingFlat(int x, int y)
    {
        int index = this[MapConstants.FLATPLANE, x, y] >> 8;
        if (Defs.Flats.Ceiling.TryGetValue(index, out var flat))
            return flat;
        return Properties.DefaultCeiling ?? content.DefaultMapInfo.DefaultCeiling;
    }

    /// <summary>The level's floor and ceiling colors (#RRGGBB), from its game-info entry or the default map's</summary>
    public string? FloorColor => Properties.FloorColor ?? content.DefaultMapInfo.FloorColor;
    public string? CeilingColor => Properties.CeilingColor ?? content.DefaultMapInfo.CeilingColor;

    public const int MaxStories = 8;

    /// <summary>How tall the level's walls are, in stories, unless a tile says otherwise</summary>
    public int LevelStories => Math.Clamp(Properties.WallHeight ?? content.DefaultMapInfo.WallHeight, 1, MaxStories);

    /// <summary>How tall a tile's wall is: its height-plane value when that's 1 to 8, else the level's (MapManager.WallStories)</summary>
    public int Stories(int x, int y) => this[MapConstants.HEIGHTPLANE, x, y] is var stories and >= 1 and <= MaxStories ? stories : LevelStories;

    /// <summary>An open floor tile with a height over 1: a block from story 2 up to it, over the walkway</summary>
    public bool IsArch(int x, int y) => KindAt(x, y) == TileKind.Floor && this[MapConstants.HEIGHTPLANE, x, y] is > 1 and <= MaxStories;

    /// <summary>The level's sky picture name (game-info sky), or null for its ceiling color</summary>
    public string? Sky => (Properties.Sky ?? content.DefaultMapInfo.Sky) is { Length: > 0 } sky ? sky : null;

    /// <summary>
    /// The level's distance fade and light, each value from its game-info entry, else the default
    /// map's, else the game's defaults (Program.ResolveShading); null when neither sets any
    /// </summary>
    public ShadingValues? Shading
    {
        get
        {
            var map = Properties;
            var fallback = content.DefaultMapInfo.Shading;
            if (!map.HasShading && fallback == null)
                return null;
            return new ShadingValues(
                map.FadeColor ?? fallback?.FadeColor ?? "#000000",
                map.FadeStart ?? fallback?.FadeStart ?? 0,
                map.FadeEnd ?? fallback?.FadeEnd ?? 16,
                map.MaxFade ?? fallback?.MaxFade ?? 100,
                map.Light ?? fallback?.Light ?? 255,
                string.Equals(fallback?.FadeCurve, "inverse", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// How a tile is lit, 0 to 255, and the zone's tint (#RRGGBB) if it has one: its light
    /// zone's when game-info defines the zone, else the level's light
    /// </summary>
    public (int Light, string? Tint) LightAt(int x, int y, IReadOnlyDictionary<int, ZoneProperties> zones, int levelLight)
    {
        int zone = this[MapConstants.ZONEPLANE, x, y];
        if (zone != 0 && zones.TryGetValue(zone, out var properties))
            return (Math.Clamp(properties.Light ?? 255, 0, 255), properties.Color);
        return (levelLight, null);
    }

    /// <summary>The wall closing off a run of arch tiles from (x, y) going (dx, dy): its East texture for a vertical face, else North (Program.ArchEndTexture)</summary>
    public string? ArchEndTexture(int x, int y, int dx, int dy, bool vertical)
    {
        do
        {
            x += dx;
            y += dy;
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return null;
        }
        while (IsArch(x, y));

        return Wall(x, y) is { } wall ? vertical ? wall.East : wall.North : null;
    }

    /// <summary>An arch tile's underside: from a wall ending its run north or south first, else west or east (Program.ArchUndersideTexture)</summary>
    public string? ArchUndersideTexture(int x, int y)
        => ArchEndTexture(x, y, 0, -1, false) ?? ArchEndTexture(x, y, 0, 1, false)
           ?? ArchEndTexture(x, y, -1, 0, true) ?? ArchEndTexture(x, y, 1, 0, true);

    /// <summary>
    /// An arch face's texture, vertical for a face along x = constant: the wall ending the run
    /// of arch tiles in line with the face, else across it (Program.OpenArch); null leaves the arch open
    /// </summary>
    public string? ArchFaceTexture(int x, int y, bool vertical)
    {
        int dx = vertical ? 0 : 1, dy = vertical ? 1 : 0;
        return ArchEndTexture(x, y, -dx, -dy, vertical) ?? ArchEndTexture(x, y, dx, dy, vertical)
            ?? ArchEndTexture(x, y, -dy, -dx, vertical) ?? ArchEndTexture(x, y, dy, dx, vertical);
    }

    /// <summary>A wall the player can push (its object-plane trigger runs A_PushWall)</summary>
    public bool IsPushWall(int x, int y)
        => KindAt(x, y) == TileKind.Wall && Trigger(x, y)?.Action.Contains("PushWall", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>The tiles of each tag on plane 4, for the switches' links</summary>
    public Dictionary<int, List<(int X, int Y)>> TaggedTiles()
    {
        var tags = new Dictionary<int, List<(int, int)>>();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int tag = this[MapConstants.TAGPLANE, x, y];
                if (tag == 0)
                    continue;
                if (!tags.TryGetValue(tag, out var tiles))
                    tags[tag] = tiles = [];
                tiles.Add((x, y));
            }
        }
        return tags;
    }

    /// <summary>A wall the player uses to set things off (mapdefs walls switch)</summary>
    public bool IsSwitch(int x, int y) => Wall(x, y)?.Switch != null;

    public int Width => map.Width;
    public int Height => map.Height;

    public ushort this[int plane, int x, int y]
        => plane < map.MapData.Length ? map.MapData[plane][y * map.Width + x] : (ushort)0;

    public TileKind KindAt(int x, int y)
    {
        int tile = this[0, x, y];
        if (tile is > 0 and <= MapConstants.MAXWALLID && Defs.Walls.ContainsKey(tile))
            return TileKind.Wall;
        if (Defs.Doors.ContainsKey(tile))
            return TileKind.Door;
        return FloorCode(tile) != null ? TileKind.Floor : TileKind.Unknown;
    }

    /// <summary>Whether the tile blocks: a wall or a door, as the game reads it</summary>
    public bool IsSolid(int x, int y) => KindAt(x, y) is TileKind.Wall or TileKind.Door;

    /// <summary>What a plane 0 floor code is, or null when it isn't one</summary>
    public string? FloorCode(int tile)
    {
        if (Floors.AreaStart is { } start && Floors.AreaCount is { } count && tile >= start && tile < start + count)
            return $"area {tile - start}";
        if (Floors.HiddenAreaStart is { } hidden and > 0 && Floors.AreaCount is { } hiddenCount && tile >= hidden && tile < hidden + hiddenCount)
            return $"area {tile - hidden} (hidden on the automap)";
        if (tile == Floors.Ambush)
            return "ambush";
        if (tile == Floors.SecretExit)
            return "secret exit";
        if (Floors.Triggers?.TryGetValue(tile, out var action) == true)
            return $"trigger {action}";
        if (Floors.ActorCodes?.ContainsKey(tile) == true)
            return "enemy code";
        return null;
    }

    public MapTextureTranslation? Wall(int x, int y)
        => KindAt(x, y) == TileKind.Wall ? Defs.Walls[this[0, x, y]] : null;

    public MapTextureTranslation? Door(int x, int y)
        => Defs.Doors.GetValueOrDefault(this[0, x, y]);

    /// <summary>The thing placed on the tile; things only stand on open floor, as in the game</summary>
    public MapActorTranslation? Thing(int x, int y)
        => !IsSolid(x, y) ? Defs.Things.GetValueOrDefault(this[1, x, y]) : null;

    public MapPlayerStartTranslation? PlayerStart(int x, int y)
        => Defs.PlayerStarts.GetValueOrDefault(this[1, x, y]);

    public MapTriggerTranslation? Trigger(int x, int y)
        => Defs.Triggers.GetValueOrDefault(this[1, x, y]);

    /// <summary>A diagonal marker, which only shapes a wall</summary>
    public MapDiagonalTranslation? Diagonal(int x, int y)
        => KindAt(x, y) == TileKind.Wall ? Defs.Diagonals.GetValueOrDefault(this[1, x, y]) : null;

    /// <summary>A line per plane saying what's on the tile, for the status bar</summary>
    public IEnumerable<string> Describe(int x, int y)
    {
        int tile = this[0, x, y];
        yield return KindAt(x, y) switch
        {
            TileKind.Wall => $"Wall {tile}: {Faces(Defs.Walls[tile])}{(Defs.Walls[tile].Switch != null ? ", switch" : "")}",
            TileKind.Door => $"Door {tile}: {DoorText(Defs.Doors[tile])}",
            TileKind.Floor => $"Floor {tile}: {FloorCode(tile)}",
            _ => $"Plane 0: {tile} (not in the mapdefs)",
        };

        int obj = this[1, x, y];
        if (obj != 0)
            yield return $"Object {obj}: {ObjectText(x, y, obj)}";

        int flat = this[MapConstants.FLATPLANE, x, y];
        if (flat != 0)
            yield return $"Flats: floor {FlatText(Defs.Flats.Floor, flat & 0xff)}, ceiling {FlatText(Defs.Flats.Ceiling, flat >> 8)}";

        int height = this[MapConstants.HEIGHTPLANE, x, y];
        if (height != 0)
            yield return $"Height: {height} {(height == 1 ? "story" : "stories")}";

        int tag = this[MapConstants.TAGPLANE, x, y];
        if (tag != 0)
            yield return $"Tag: {tag}";

        int zone = this[MapConstants.ZONEPLANE, x, y];
        if (zone != 0)
            yield return $"Light zone: {zone}";
    }

    private string ObjectText(int x, int y, int obj)
    {
        if (Thing(x, y) is { } thing)
        {
            var details = new List<string> { thing.Class };
            if (thing.Angles != 0 || thing.Patrol != 0)
                details.Add($"facing {thing.Angles}");
            if (thing.Patrol != 0)
                details.Add("patrols");
            if (thing.MinSkill > 0)
                details.Add($"skill {thing.MinSkill + 1}+");
            return string.Join(", ", details);
        }
        if (PlayerStart(x, y) is { } start)
            return $"{(start.Deathmatch ? "deathmatch start" : "player start")}, facing {start.Angles}";
        if (Trigger(x, y) is { } trigger)
            return $"{trigger.Action} ({trigger.Activation}{(trigger.Secret ? ", secret" : "")})";
        if (Diagonal(x, y) is { } diagonal)
            return $"diagonal {diagonal.Shape}";
        if (Defs.MapInfo.TryGetValue(obj >> 8, out var info))
            return $"map info: {info}";
        if (Defs.DoorLocks.TryGetValue(obj, out var item))
            return $"door lock: {item}";
        if (Defs.Things.ContainsKey(obj))
            return "a thing, but on a wall or door, so it isn't placed";
        return "not in the mapdefs";
    }

    private static string Faces(MapTextureTranslation textures)
    {
        var faces = new[] { textures.North, textures.East, textures.South, textures.West }.Distinct().ToList();
        return faces.Count == 1 ? faces[0] : $"N {textures.North}, E {textures.East}, S {textures.South}, W {textures.West}";
    }

    private static string DoorText(MapTextureTranslation door)
    {
        var details = new List<string> { door.Vertical ? "vertical" : "horizontal", door.Vertical ? door.East : door.North };
        if (!string.IsNullOrEmpty(door.Lock))
            details.Add($"locked: {door.Lock}");
        if (!string.IsNullOrEmpty(door.OpensFrom))
            details.Add($"opens from the {door.OpensFrom}");
        return string.Join(", ", details);
    }

    private static string FlatText(Dictionary<int, string> flats, int index)
        => flats.TryGetValue(index, out var name) ? $"{index} ({name})" : $"{index} (the map's default)";
}
