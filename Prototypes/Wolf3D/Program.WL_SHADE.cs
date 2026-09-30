using Wolf3D.Assets;
using Wolf3D.Constants;
using Wolf3D.Entities;

namespace Wolf3D;

/// <summary>A level's shading, with every value filled in (see ShadingInfo)</summary>
internal record struct ShadingSettings(string FadeColor, double FadeStart, double FadeEnd, int MaxFade, int Light);

internal partial class Program
{
    /*
    =============================================================================

                                    SHADING

    The view is drawn in palette indices, so shading remaps them, Doom colormap style. A light
    row (VideoManager.GetLightRow) holds FADESTEPS remaps of 256 entries for one light level:
    step 0 is only dimmed to the light, and each step after it goes further toward the fade
    color. A pixel's step comes from its distance along the view (as CalcHeight measures it),
    so walls take one per post, flats one per row, and sprites one per sprite.

    =============================================================================
    */

    internal const int LIGHTLEVELS = 32;    // light 0 (black) to LIGHTLEVELS - 1 (full)
    internal const int FADESTEPS = 32;      // step 0 (none) to FADESTEPS - 1 (all the fade color)

    static readonly byte[] noshade = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

    internal static ShadingSettings? levelshading;  // null: the level isn't shaded
    static bool shading;                // anything to do: levelshading dims or fades something
    static byte[] lightrow = noshade;   // the level's light's row
    static byte fader, fadeg, fadeb;
    static long fadestart, fadeend;     // along the view, in global units (TILEGLOBAL a tile)
    static int maxfadestep;

    /// <summary>
    /// The map's shading, each value it leaves out taken from the default map's, else the
    /// ShadingInfo defaults; null when neither has any.
    /// </summary>
    internal static ShadingSettings? ResolveShading(MapInfo? mapInfo, DefaultMapInfo defaultMap)
    {
        ShadingInfo? map = mapInfo?.Shading, fallback = defaultMap.Shading;
        if (map == null && fallback == null)
            return null;

        return new ShadingSettings(
            map?.FadeColor ?? fallback?.FadeColor ?? "#000000",
            map?.FadeStart ?? fallback?.FadeStart ?? 0,
            map?.FadeEnd ?? fallback?.FadeEnd ?? 16,
            map?.MaxFade ?? fallback?.MaxFade ?? 100,
            map?.Light ?? fallback?.Light ?? 255);
    }

    /// <summary>Shades the level with settings (null for none) until it's left or reloaded.</summary>
    internal static void SetShading(ShadingSettings? settings)
    {
        levelshading = settings;
        shading = settings is { } set && (set.MaxFade > 0 || set.Light < 255);
        lightrow = noshade;
        if (!shading)
            return;

        var s = settings!.Value;
        try
        {
            var fade = Color.FromHexRGBA(s.FadeColor);
            (fader, fadeg, fadeb) = (fade.Red, fade.Green, fade.Blue);
        }
        catch (ArgumentException)
        {
            Console.WriteLine($"Shading fade-color '{s.FadeColor}' isn't #RRGGBB, fading to black instead");
            (fader, fadeg, fadeb) = (0, 0, 0);
        }

        fadestart = (long)(Math.Max(s.FadeStart, 0) * MapConstants.TILEGLOBAL);
        fadeend = Math.Max((long)(s.FadeEnd * MapConstants.TILEGLOBAL), fadestart + 1);
        maxfadestep = (int)Math.Round(Math.Clamp(s.MaxFade, 0, 100) / 100.0 * (FADESTEPS - 1));

        lightrow = LightRow((int)Math.Round(Math.Clamp(s.Light, 0, 255) / 255.0 * (LIGHTLEVELS - 1)));
    }

    // The light row for a light level (0 to LIGHTLEVELS - 1)
    static byte[] LightRow(int level) =>
        shading ? _videoManager.GetLightRow(level, LIGHTLEVELS, FADESTEPS, fader, fadeg, fadeb) : noshade;

    /// <summary>Where the remap for something nx away along the view (global units) starts in a light row.</summary>
    static int ShadeOffset(long nx)
    {
        if (!shading || nx <= fadestart || maxfadestep == 0)
            return 0;
        if (nx >= fadeend)
            return maxfadestep * 256;
        return (int)((nx - fadestart) * maxfadestep / (fadeend - fadestart)) * 256;
    }

    /// <summary>ShadeOffset for something drawn height high (as wallheight: a story is height >> 2 pixels).</summary>
    static int ShadeOffsetForHeight(int height) =>
        !shading ? 0 : ShadeOffset(height > 0 ? (long)heightnumerator * 256 / height : long.MaxValue);

    /// <summary>Whether an actor's frame is drawn unshaded: its state has the bright modifier, or the actor the BRIGHT flag.</summary>
    internal static bool IsBright(Entities.Actors.Actor actor) =>
        actor.CurrentState?.Bright == true || actor.Flags.Contains("bright", StringComparer.OrdinalIgnoreCase);

    internal static void InitLevelShading(MapInfo? mapInfo) =>
        SetShading(ResolveShading(mapInfo, _gameEngineManager.GetGameInfo().DefaultMap));
}
