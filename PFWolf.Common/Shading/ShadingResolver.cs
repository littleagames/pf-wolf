using PFWolf.Assets;

namespace PFWolf.Shading;

/// <summary>
/// A level's shading and light zones as game-info gives them, each value a map leaves out taken
/// from the default map's, else the defaults; and the colors and effect names they're written
/// with. What the game shades a level with, and what the editor previews.
/// </summary>
public static class ShadingResolver
{
    /// <summary>
    /// The map's shading, each value it leaves out taken from the default map's, else the
    /// ShadingInfo defaults; null when neither has any.
    /// </summary>
    public static ShadingSettings? ResolveShading(MapInfo? mapInfo, DefaultMapInfo defaultMap)
    {
        ShadingInfo? map = mapInfo?.Shading, fallback = defaultMap.Shading;
        if (map == null && fallback == null)
            return null;

        return new ShadingSettings(
            map?.FadeColor ?? fallback?.FadeColor ?? "#000000",
            map?.FadeStart ?? fallback?.FadeStart ?? 0,
            map?.FadeEnd ?? fallback?.FadeEnd ?? 16,
            map?.MaxFade ?? fallback?.MaxFade ?? 100,
            map?.Light ?? fallback?.Light ?? 255,
            IsInverseCurve(map?.FadeCurve ?? fallback?.FadeCurve));
    }

    public static bool IsInverseCurve(string? curve)
    {
        if (string.IsNullOrEmpty(curve) || curve.Equals("linear", StringComparison.OrdinalIgnoreCase))
            return false;
        if (curve.Equals("inverse", StringComparison.OrdinalIgnoreCase))
            return true;
        if (BadColors.Add("fade-curve:" + curve))
            Console.WriteLine($"Unknown shading fade-curve '{curve}' (linear or inverse); using linear");
        return false;
    }

    /// <summary>
    /// The map's light zones and the default map's, a map's zone taking each value it leaves
    /// out from the default map's zone of the same id, else the ZoneInfo defaults. A zone that
    /// only has ambient sound, in both, isn't one: its tiles keep the level's light.
    /// </summary>
    public static Dictionary<int, ZoneState> ResolveZones(MapInfo? mapInfo, DefaultMapInfo defaultMap)
    {
        var zones = new Dictionary<int, ZoneState>();
        var mapZones = mapInfo?.Zones ?? [];
        foreach (var id in defaultMap.Zones.Keys.Union(mapZones.Keys))
        {
            ZoneInfo? map = mapZones.GetValueOrDefault(id), fallback = defaultMap.Zones.GetValueOrDefault(id);
            if (map is { HasLight: false } && fallback is not { HasLight: true } || map == null && fallback is { HasLight: false })
                continue;
            zones[id] = ZoneState.FromInfo(map, fallback);
        }
        return zones;
    }

    /// <summary>What a level fades with when it has zones but no shading of its own: nothing</summary>
    public static readonly ShadingSettings NoFade = new("#000000", 0, 16, 0, 255);

    /// <summary>White: no tint</summary>
    public static readonly (byte, byte, byte) NoTint = (255, 255, 255);

    /// <summary>The colors (and fade curves) already warned about, so each is warned of once</summary>
    public static readonly HashSet<string> BadColors = [];

    /// <summary>A #RRGGBB color, or fallback when there's none or it isn't one (warned about once)</summary>
    public static (byte R, byte G, byte B) ParseRgb(string? color, (byte, byte, byte) fallback, string what)
    {
        if (string.IsNullOrEmpty(color))
            return fallback;
        try
        {
            var c = Color.FromHexRGBA(color);
            return (c.Red, c.Green, c.Blue);
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            if (BadColors.Add(color))
                Console.WriteLine($"The {what} '{color}' isn't #RRGGBB; using #{fallback.Item1:X2}{fallback.Item2:X2}{fallback.Item3:X2}");
            return fallback;
        }
    }

    public static bool IsRgb(string color)
    {
        try { Color.FromHexRGBA(color); return color.TrimStart('#').Length == 6; }
        catch (Exception e) when (e is ArgumentException or FormatException) { return false; }
    }

    public static bool TryParseZoneEffect(string name, out ZoneEffect effect) =>
        Enum.TryParse(name, ignoreCase: true, out effect) && Enum.IsDefined(effect);
}
