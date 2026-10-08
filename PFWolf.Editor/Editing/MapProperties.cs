using PFWolf.Assets;

namespace PFWolf.Editor.Editing;

/// <summary>A light zone of a level (game-info zones: the zone plane's value → how it's lit)</summary>
public sealed record ZoneProperties(int Id, int? Light = null, string? Color = null, string? Effect = null,
    int? Low = null, int? Tics = null, int? BrightTics = null)
{
    public static ZoneProperties From(int id, ZoneInfo zone)
        => new(id, zone.Light, zone.Color, zone.Effect, zone.Low, zone.Tics, zone.BrightTics);
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

    // Shading
    public string? FadeColor { get; init; }
    public double? FadeStart { get; init; }
    public double? FadeEnd { get; init; }
    public int? MaxFade { get; init; }
    public int? Light { get; init; }

    public IReadOnlyList<ZoneProperties> Zones { get; init; } = [];

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
        FadeColor = info.Shading?.FadeColor,
        FadeStart = info.Shading?.FadeStart,
        FadeEnd = info.Shading?.FadeEnd,
        MaxFade = info.Shading?.MaxFade,
        Light = info.Shading?.Light,
        Zones = (info.Zones ?? []).OrderBy(zone => zone.Key).Select(zone => ZoneProperties.From(zone.Key, zone.Value)).ToList(),
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
        Check("shading", FadeColor == other.FadeColor && FadeStart == other.FadeStart && FadeEnd == other.FadeEnd
                         && MaxFade == other.MaxFade && Light == other.Light);
        Check("zones", Zones.SequenceEqual(other.Zones));
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
                    zone.Low ?? under.Low, zone.Tics ?? under.Tics, zone.BrightTics ?? under.BrightTics)
                : zone;
        }
        return zones;
    }
}
