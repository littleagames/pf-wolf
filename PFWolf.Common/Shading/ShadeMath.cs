using System.Runtime.CompilerServices;

namespace PFWolf.Shading;

/// <summary>
/// A level's distance fade as the renderer steps it: the fade color, where along the view the
/// fade starts and ends (global units, TileGlobal a tile), how many fade steps it reaches, and
/// whether it's Blake Stone's inverse curve. Made from a level's ShadingSettings.
/// </summary>
public readonly record struct FadeParams(byte R, byte G, byte B, long Start, long End, int MaxFadeStep, bool Inverse)
{
    /// <summary>The fade for these settings (a bad fade color is warned about once, and is black)</summary>
    public static FadeParams From(ShadingSettings settings)
    {
        var (r, g, b) = ShadingResolver.ParseRgb(settings.FadeColor, (0, 0, 0), "shading fade-color");
        long start = (long)(Math.Max(settings.FadeStart, 0) * MapConstants.TileGlobal);
        long end = Math.Max((long)(settings.FadeEnd * MapConstants.TileGlobal), start + 1);
        int maxFadeStep = (int)Math.Round(Math.Clamp(settings.MaxFade, 0, 100) / 100.0 * (ShadeMath.FadeSteps - 1));
        return new FadeParams(r, g, b, start, end, maxFadeStep, settings.InverseFade);
    }
}

/// <summary>
/// SHADING's numbers, shared by the game's renderer and the editor's previews. The view is drawn
/// in palette indices, so shading remaps them, Doom colormap style: a light row
/// (<see cref="LightRowBuilder"/>) holds FadeSteps remaps of 256 entries for one light (a level
/// and a tint), step 0 only lit and each step after it further toward the fade color. A pixel's
/// step comes from its distance along the view, as the renderer's CalcHeight measures it.
/// </summary>
public static class ShadeMath
{
    public const int LightLevels = 32;    // light 0 (black) to LightLevels - 1 (full)
    public const int FadeSteps = 32;      // step 0 (none) to FadeSteps - 1 (all the fade color)
    public const int MaxLightLevel = LightLevels - 1;

    /// <summary>The light level (0 to LightLevels - 1) for a light (0 to 255)</summary>
    public static int LightLevelOf(int light) => (int)Math.Round(Math.Clamp(light, 0, 255) / 255.0 * (LightLevels - 1));

    /// <summary>Where the remap for something nx away along the view (global units) starts in a light row.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ShadeOffset(long nx, in FadeParams fade)
    {
        if (nx <= fade.Start || fade.MaxFadeStep == 0)
            return 0;
        if (fade.Inverse)
            return (int)(fade.MaxFadeStep - fade.MaxFadeStep * fade.Start / nx) * 256;
        if (nx >= fade.End)
            return fade.MaxFadeStep * 256;
        return (int)((nx - fade.Start) * fade.MaxFadeStep / (fade.End - fade.Start)) * 256;
    }

    /// <summary>
    /// ShadeOffset for something drawn height high (as the renderer's WallHeight: a story is
    /// height >> 2 pixels), turned back into a distance through the view's height numerator.
    /// </summary>
    public static int ShadeOffsetForHeight(int height, int heightNumerator, in FadeParams fade) =>
        ShadeOffset(height > 0 ? (long)heightNumerator * 256 / height : long.MaxValue, fade);

    /// <summary>A sprite's shade offset: its distance's (a ShadeOffset), less its lighten's steps, within the fade</summary>
    public static int Lightened(int shadeOffset, int lighten, in FadeParams fade) =>
        Math.Clamp(shadeOffset - lighten * 256, 0, fade.MaxFadeStep * 256);
}
