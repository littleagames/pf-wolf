using PFWolf.Assets;

namespace PFWolf.Editor.Editing;

/// <summary>A zone of a level (game-info zones: the zone plane's value → how it's lit, and its ambient sound)</summary>
public sealed record ZoneProperties(int Id, int? Light = null, string? Color = null, string? Effect = null,
    int? Low = null, int? Tics = null, int? BrightTics = null, string? Ambient = null, string? AmbientDisguised = null,
    string? AmbientUndetected = null)
{
    public static ZoneProperties From(int id, ZoneInfo zone)
        => new(id, zone.Light, zone.Color, zone.Effect, zone.Low, zone.Tics, zone.BrightTics, zone.Ambient, zone.AmbientDisguised,
            zone.AmbientUndetected);
}

/// <summary>
/// A scroller of a level (game-info scrollers: the tag plane's value → how its tiles' textures
/// slide), speeds in texels a tic: on walls x right and y down, on floors and ceilings x east
/// and y south
/// </summary>
public sealed record ScrollerProperties(int Tag, double X = 0, double Y = 0, bool Walls = true, bool Floor = true,
    bool Ceiling = true, bool Active = true)
{
    public static ScrollerProperties From(int tag, ScrollerInfo scroller)
        => new(tag, scroller.X, scroller.Y, scroller.Walls, scroller.Floor, scroller.Ceiling, scroller.Active);
}

/// <summary>
/// A level's game-info entry, as far as the editor edits it: what's shown and played, the
/// level after it, how it looks and how it's lit. Null leaves a value to the default map.
/// </summary>
public sealed record MapProperties
{
    public string? Name { get; init; }
    public string? Music { get; init; }
    public string? Next { get; init; }
    public string? SecretNext { get; init; }
    public int? ParTime { get; init; }
    public string? FloorColor { get; init; }
    public string? CeilingColor { get; init; }
    public int? WallHeight { get; init; }
    public string? Sky { get; init; }
    public string? DefaultFloor { get; init; }
    public string? DefaultCeiling { get; init; }
    public string? Weather { get; init; }

    /// <summary>The level's floor in its mission (game-info floor-number: Blake Stone's 0 to 10), which elevators and map-info links go by</summary>
    public int? FloorNumber { get; init; }

    /// <summary>The cluster (game-info clusters key) the level is in: a Blake Stone mission</summary>
    public int? Cluster { get; init; }

    // Shading
    public string? FadeColor { get; init; }
    public double? FadeStart { get; init; }
    public double? FadeEnd { get; init; }
    public int? MaxFade { get; init; }
    public int? Light { get; init; }

    public IReadOnlyList<ZoneProperties> Zones { get; init; } = [];

    public IReadOnlyList<ScrollerProperties> Scrollers { get; init; } = [];

    public bool HasShading => FadeColor != null || FadeStart != null || FadeEnd != null || MaxFade != null || Light != null;

    public static MapProperties From(MapInfo? info) => info == null ? new() : new()
    {
        Name = info.Name,
        Music = info.Music,
        Next = info.Next,
        SecretNext = info.SecretNext,
        ParTime = info.ParTime == 0 ? null : info.ParTime,
        FloorColor = info.FloorColor,
        CeilingColor = info.CeilingColor,
        WallHeight = info.WallHeight,
        Sky = info.Sky,
        DefaultFloor = info.DefaultFloor,
        DefaultCeiling = info.DefaultCeiling,
        Weather = info.Weather,
        // game-info can't tell floor 0 from none: both are left to the default, 0
        FloorNumber = info.FloorNumber == 0 ? null : info.FloorNumber,
        Cluster = info.Cluster == 0 ? null : info.Cluster,
        FadeColor = info.Shading?.FadeColor,
        FadeStart = info.Shading?.FadeStart,
        FadeEnd = info.Shading?.FadeEnd,
        MaxFade = info.Shading?.MaxFade,
        Light = info.Shading?.Light,
        Zones = (info.Zones ?? []).OrderBy(zone => zone.Key).Select(zone => ZoneProperties.From(zone.Key, zone.Value)).ToList(),
        Scrollers = (info.Scrollers ?? []).OrderBy(scroller => scroller.Key)
            .Select(scroller => ScrollerProperties.From(scroller.Key, scroller.Value)).ToList(),
    };

    /// <summary>The game-info keys whose values differ between the two</summary>
    public IReadOnlyList<string> ChangedKeys(MapProperties other)
    {
        var keys = new List<string>();
        void Check(string key, bool same)
        {
            if (!same)
                keys.Add(key);
        }

        Check("name", Name == other.Name);
        Check("music", Music == other.Music);
        Check("next", Next == other.Next);
        Check("secret-next", SecretNext == other.SecretNext);
        Check("par-time", ParTime == other.ParTime);
        Check("floor-color", FloorColor == other.FloorColor);
        Check("ceiling-color", CeilingColor == other.CeilingColor);
        Check("wall-height", WallHeight == other.WallHeight);
        Check("sky", Sky == other.Sky);
        Check("default-floor", DefaultFloor == other.DefaultFloor);
        Check("default-ceiling", DefaultCeiling == other.DefaultCeiling);
        Check("weather", Weather == other.Weather);
        Check("floor-number", FloorNumber == other.FloorNumber);
        Check("cluster", Cluster == other.Cluster);
        Check("shading", FadeColor == other.FadeColor && FadeStart == other.FadeStart && FadeEnd == other.FadeEnd
                         && MaxFade == other.MaxFade && Light == other.Light);
        Check("zones", Zones.SequenceEqual(other.Zones));
        Check("scrollers", Scrollers.SequenceEqual(other.Scrollers));
        return keys;
    }

    /// <summary>The zones that apply: the default map's, with the level's own over them by id</summary>
    public Dictionary<int, ZoneProperties> EffectiveZones(DefaultMapInfo defaults)
    {
        var zones = defaults.Zones.ToDictionary(zone => zone.Key, zone => ZoneProperties.From(zone.Key, zone.Value));
        foreach (var zone in Zones)
        {
            zones[zone.Id] = zones.TryGetValue(zone.Id, out var under)
                ? new ZoneProperties(zone.Id, zone.Light ?? under.Light, zone.Color ?? under.Color, zone.Effect ?? under.Effect,
                    zone.Low ?? under.Low, zone.Tics ?? under.Tics, zone.BrightTics ?? under.BrightTics,
                    zone.Ambient ?? under.Ambient, zone.AmbientDisguised ?? under.AmbientDisguised,
                    zone.AmbientUndetected ?? under.AmbientUndetected)
                : zone;
        }
        return zones;
    }
}
