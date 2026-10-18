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

    /// <summary>The level's scrollers by tag (game-info scrollers; a level's own only)</summary>
    public Dictionary<int, ScrollerProperties> Scrollers => Properties.Scrollers.ToDictionary(scroller => scroller.Tag);

    public bool IsAmbush(int x, int y) => Floors.Ambush is { } ambush and >= 0 && this[0, x, y] == ambush;

    /// <summary>
    /// The texture a floor tile shows: its flat-plane floor index's (mapdefs flats), else the
    /// level's default floor; null for the floor color
    /// </summary>
    public string? FloorFlat(int x, int y)
    {
        int index = this[MapConstants.FlatPlane, x, y] & 0xff;
        if (Defs.Flats.Floor.TryGetValue(index, out var flat))
            return flat;
        return Properties.DefaultFloor ?? content.DefaultMapInfo.DefaultFloor;
    }

    /// <summary>The texture a tile's ceiling shows, like <see cref="FloorFlat"/>: null for the ceiling color</summary>
    public string? CeilingFlat(int x, int y)
    {
        int index = this[MapConstants.FlatPlane, x, y] >> 8;
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
    public int Stories(int x, int y) => this[MapConstants.HeightPlane, x, y] is var stories and >= 1 and <= MaxStories ? stories : LevelStories;

    /// <summary>An open floor tile with a height over 1: a block from story 2 up to it, over the walkway</summary>
    public bool IsArch(int x, int y) => KindAt(x, y) == TileKind.Floor && this[MapConstants.HeightPlane, x, y] is > 1 and <= MaxStories;

    /// <summary>The level's sky picture name (game-info sky), or null for its ceiling color</summary>
    public string? Sky => (Properties.Sky ?? content.DefaultMapInfo.Sky) is { Length: > 0 } sky ? sky : null;

    /// <summary>
    /// The level's distance fade and light, each value from its game-info entry, else the default
    /// map's, else the game's defaults (Lighting.ResolveShading); null when neither sets any
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
        int zone = this[MapConstants.ZonePlane, x, y];
        if (zone != 0 && zones.TryGetValue(zone, out var properties))
            return (Math.Clamp(properties.Light ?? 255, 0, 255), properties.Color);
        return (levelLight, null);
    }

    /// <summary>The wall closing off a run of arch tiles from (x, y) going (dx, dy): its texture on <paramref name="face"/> (WallPosts.ArchEndTexture)</summary>
    public string? ArchEndTexture(int x, int y, int dx, int dy, string face)
    {
        do
        {
            x += dx;
            y += dy;
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return null;
        }
        while (IsArch(x, y));

        return Wall(x, y)?.WallFace(face);
    }

    /// <summary>
    /// An arch tile's underside: from a wall ending its run north or south first (the face
    /// toward the arch), else west or east (WallPosts.ArchUndersideTexture)
    /// </summary>
    public string? ArchUndersideTexture(int x, int y)
        => ArchEndTexture(x, y, 0, -1, "south") ?? ArchEndTexture(x, y, 0, 1, "north")
           ?? ArchEndTexture(x, y, -1, 0, "east") ?? ArchEndTexture(x, y, 1, 0, "west");

    /// <summary>
    /// The ceiling flat an arch tile shows underneath in place of <see cref="ArchUndersideTexture"/>:
    /// its <see cref="CeilingFlat"/> when the flat-plane ceiling index isn't 0, else null (WallPosts.UseArchCeilingFlat)
    /// </summary>
    public string? ArchCeilingFlat(int x, int y)
        => this[MapConstants.FlatPlane, x, y] >> 8 == 0 ? null : CeilingFlat(x, y);

    /// <summary>
    /// An arch face's texture (its face "north", "south", "east" or "west"): that face of the wall
    /// ending the run of arch tiles in line with the face, else across it (WallPosts.OpenArch); null
    /// leaves the arch open
    /// </summary>
    public string? ArchFaceTexture(int x, int y, string face)
    {
        bool vertical = face is "east" or "west";
        int dx = vertical ? 0 : 1, dy = vertical ? 1 : 0;
        return ArchEndTexture(x, y, -dx, -dy, face) ?? ArchEndTexture(x, y, dx, dy, face)
            ?? ArchEndTexture(x, y, -dy, -dx, face) ?? ArchEndTexture(x, y, dy, dx, face);
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
                int tag = this[MapConstants.TagPlane, x, y];
                if (tag == 0)
                    continue;
                if (!tags.TryGetValue(tag, out var tiles))
                    tags[tag] = tiles = [];
                tiles.Add((x, y));
            }
        }
        return tags;
    }

    /// <summary>A wall the player uses to set things off (mapdefs walls switch), or one with a patch that's a switch</summary>
    public bool IsSwitch(int x, int y) => Wall(x, y)?.Switch != null || PatchDef(x, y)?.Switch != null;

    public int Width => map.Width;
    public int Height => map.Height;

    /// <summary>
    /// A tile's value on a plane. On plane 0, the tile's own code (wall, door or floor), without
    /// the patch the high byte holds (<see cref="Patch"/>), as the game reads it.
    /// </summary>
    public ushort this[int plane, int x, int y]
        => plane >= map.MapData.Length ? (ushort)0
            : plane == 0 ? (ushort)(map.MapData[0][y * map.Width + x] & MapConstants.Plane0CodeMask)
            : map.MapData[plane][y * map.Width + x];

    /// <summary>The patch id on the tile (plane 0's high byte), 0 for none; the game only draws one on a wall</summary>
    public int Patch(int x, int y) => map.MapData[0][y * map.Width + x] >> MapConstants.PatchShift;

    /// <summary>The patch on a wall tile, when the mapdefs define it</summary>
    public MapPatchTranslation? PatchDef(int x, int y)
        => KindAt(x, y) == TileKind.Wall && Patch(x, y) is var id and not 0 ? Defs.Patches.GetValueOrDefault(id) : null;

    public TileKind KindAt(int x, int y)
    {
        int tile = this[0, x, y];
        if (tile is > 0 and <= MapConstants.MaxWallId && Defs.Walls.ContainsKey(tile))
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
    /// <summary>The thing placed on the tile: on open floor, or (an `in-wall` one) on a wall</summary>
    public MapActorTranslation? Thing(int x, int y)
        => Defs.Things.GetValueOrDefault(this[1, x, y]) is { } thing && KindAt(x, y) switch
        {
            TileKind.Door => false,
            TileKind.Wall => thing.InWall,
            _ => true,
        } ? thing : null;

    public MapPlayerStartTranslation? PlayerStart(int x, int y)
        => Defs.PlayerStarts.GetValueOrDefault(this[1, x, y]);

    public MapTriggerTranslation? Trigger(int x, int y)
        => Defs.Triggers.GetValueOrDefault(this[1, x, y]);

    /// <summary>
    /// Whether the mapdefs know the tile's object-plane value (0 is nothing, so known): a thing,
    /// diagonal, player start, trigger, door lock or map-info code, or the value after a map-info code
    /// </summary>
    public bool ObjectKnown(int x, int y)
    {
        int obj = this[1, x, y];
        if (obj == 0 || Defs.Things.ContainsKey(obj) || Defs.Diagonals.ContainsKey(obj) || Defs.PlayerStarts.ContainsKey(obj)
            || Defs.Triggers.ContainsKey(obj) || Defs.DoorLocks.ContainsKey(obj) || Defs.MapInfo.ContainsKey(obj >> 8))
            return true;

        // The tile after a map-info code holds that code's value
        return x > 0 && Defs.MapInfo.TryGetValue(this[1, x - 1, y] >> 8, out var info) && MapInfoCodes.HasValue(info);
    }

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

        int patch = Patch(x, y);
        if (patch != 0)
            yield return $"Patch {patch}: {PatchText(x, y, patch)}";

        int obj = this[1, x, y];
        if (obj != 0)
            yield return $"Object {obj}: {ObjectText(x, y, obj)}";

        int flat = this[MapConstants.FlatPlane, x, y];
        if (flat != 0)
            yield return $"Flats: floor {FlatText(Defs.Flats.Floor, flat & 0xff)}, ceiling {FlatText(Defs.Flats.Ceiling, flat >> 8)}";

        int height = this[MapConstants.HeightPlane, x, y];
        if (height != 0)
            yield return $"Height: {height} {(height == 1 ? "story" : "stories")}";

        int tag = this[MapConstants.TagPlane, x, y];
        if (tag != 0)
            yield return $"Tag: {tag}";

        int zone = this[MapConstants.ZonePlane, x, y];
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
            if (content.BrokenThingReason(thing.Class) is { } reason)
                details.Add($"broken: {reason}");
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
            return "a thing, but on a wall or door, so it isn't placed (only `in-wall` ones go on walls)";
        return "not in the mapdefs";
    }

    private string PatchText(int x, int y, int patch)
    {
        if (!Defs.Patches.TryGetValue(patch, out var def))
            return "not in the mapdefs";
        if (KindAt(x, y) != TileKind.Wall)
            return $"{def.Texture}, but not on a wall, so it isn't drawn";
        return PatchDescription(def);
    }

    /// <summary>A patch's texture, faces, story and switch, as the status bar and palette show it</summary>
    public static string PatchDescription(MapPatchTranslation patch)
    {
        var details = new List<string> { patch.Texture };
        if (patch.Sides.Count > 0)
            details.Add(string.Join("/", patch.Sides));
        if (patch.Story != 1)
            details.Add(patch.Story == 0 ? "every story" : $"story {patch.Story}");
        if (patch.Switch is { } patchSwitch)
            details.Add(patchSwitch.Remove ? "switch, once" : "switch");
        return string.Join(", ", details);
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
