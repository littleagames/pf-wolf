using PFWolf.Assets;
using PFWolf.Constants;

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

/// <summary>
/// Reads a level's planes the way the game does when it loads one (MapManager.LoadMap): which
/// plane 0 values are walls, doors and floor codes, and what each object-plane value places
/// </summary>
public sealed class MapTiles(GameContent content, MapAsset map)
{
    private MapObjectTranslationAsset Defs => content.MapDefs;
    private MapFloorsTranslation Floors => content.MapDefs.Floors;

    public MapAsset Map => map;

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
                details.Add($"skill {thing.MinSkill}+");
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
