using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Shading;

namespace PFWolf.Editor.Rendering;

/// <summary>How a frame is drawn "seen from the player": which light row, where in it, and why</summary>
/// <param name="Row">The light row (null: unshaded, the picture's own colors)</param>
/// <param name="Offset">Where its remap starts in the row (a fade step × 256)</param>
/// <param name="Drawn">False when the game wouldn't draw it at all (too near)</param>
/// <param name="Text">What the preview says of it</param>
public sealed record FrameShade(byte[]? Row, int Offset, bool Drawn, string Text)
{
    public int Step => Offset / 256;
}

/// <summary>
/// A frame shaded as the game shades a sprite at some distance (Rendering/SpriteDrawing.cs and
/// SpriteScaler.cs in the game, the numbers from PFWolf.Common's Shading): drawn unshaded when
/// bright; the weapon in hand lit but not faded; anything else lit, then faded by its distance
/// less its lighten. Lit by the level's own light: zones and actors' lights aren't here.
/// </summary>
public sealed class ShadePreview
{
    private LightRowBuilder? _rows;
    private PaletteColor[]? _rowsPalette;

    /// <summary>The height numerator the distance goes through: the classic window's</summary>
    public static readonly int HeightNumerator = SpriteShading.HeightNumerator(SpriteShading.ClassicProjectionWidth);

    /// <summary>Whether the game shades a level with these settings at all (Lighting.RebuildShading, zones aside)</summary>
    public static bool Shades(ShadingSettings? settings) => settings is { } s && (s.MaxFade > 0 || s.Light < 255);

    /// <summary>
    /// The shade for a frame <paramref name="tiles"/> tiles away (its middle, along the view)
    /// with these settings (null: the level isn't shaded)
    /// </summary>
    public FrameShade Shade(PaletteColor[] palette, ShadingSettings? settings, double tiles, bool inHand, bool bright, int lighten)
    {
        if (!Shades(settings))
            return new FrameShade(null, 0, true, settings == null ? "no shading here: nothing fades" : "this shading neither dims nor fades");
        var s = settings!.Value;
        var fade = FadeParams.From(s);
        int level = ShadeMath.LightLevelOf(s.Light);
        if (bright)
            return new FrameShade(null, 0, true, "drawn bright: unshaded at any distance");

        var row = Rows(palette).Row(level, ShadeMath.LightLevels, ShadeMath.FadeSteps, fade.R, fade.G, fade.B);
        string light = $"light level {level} of {ShadeMath.MaxLightLevel}";
        if (inHand)
            return new FrameShade(row, 0, true, $"in hand: {light}, weapons in hand don't fade");

        long distance = (long)Math.Round(tiles * MapConstants.TileGlobal);
        int height = SpriteShading.ViewHeight(distance, HeightNumerator);
        if (height == 0)
            return new FrameShade(row, 0, false, $"{Tiles(tiles)}: too near to be drawn");

        int far = ShadeMath.ShadeOffsetForHeight(height, HeightNumerator, fade);
        int offset = ShadeMath.Lightened(far, lighten, fade);
        string lightened = lighten == 0 ? "" : $" (lighten {lighten:+#;-#} from step {far / 256})";
        return new FrameShade(row, offset, true,
            $"{Tiles(tiles)}: fade step {offset / 256} of {fade.MaxFadeStep}{lightened}, {light}");
    }

    private static string Tiles(double tiles) => $"{tiles:0.##} tile{(tiles == 1 ? "" : "s")}";

    // One builder for the palette shown (the palette window can change it)
    private LightRowBuilder Rows(PaletteColor[] palette)
    {
        if (_rows == null || !ReferenceEquals(_rowsPalette, palette))
            (_rows, _rowsPalette) = (new LightRowBuilder(palette), palette);
        return _rows;
    }

    /// <summary>A picture through one remap of a light row: see-through pixels stay as they are</summary>
    public static IndexedPicture Remap(IndexedPicture picture, byte[] row, int offset)
    {
        var indices = new byte[picture.Indices.Length];
        for (int i = 0; i < indices.Length; i++)
            indices[i] = row[offset + picture.Indices[i]];
        return picture with { Indices = indices };
    }
}
