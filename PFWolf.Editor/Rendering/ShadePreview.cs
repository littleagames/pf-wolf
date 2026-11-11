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

/// <summary>A light zone the frame stands in: the light it shows now (0-255) and its tint</summary>
public sealed record PreviewZone(int Id, int Light, (byte R, byte G, byte B) Tint);

/// <summary>The light a frame gives off now (ActorLightRules.Current), and whether it's a set light (light.mode: set)</summary>
public sealed record SelfLight(int Intensity, double Radius, string? Color, bool Sets);

/// <summary>
/// A frame shaded as the game shades a sprite at some distance (Rendering/SpriteDrawing.cs and
/// SpriteScaler.cs in the game, the numbers from PFWolf.Common's Shading): drawn unshaded when
/// bright; the weapon in hand lit but not faded; anything else lit, then faded by its distance
/// less its lighten. Lit by the level's light, or a zone's; actors' own lights aren't here.
/// </summary>
public sealed class ShadePreview
{
    private LightRowBuilder? _rows;
    private PaletteColor[]? _rowsPalette;

    /// <summary>The height numerator the distance goes through: the classic window's</summary>
    public static readonly int HeightNumerator = SpriteShading.HeightNumerator(SpriteShading.ClassicProjectionWidth);

    /// <summary>
    /// Whether the game shades a level with these settings at all (Lighting.RebuildShading): when
    /// something is dimmed or faded, or the level has light zones (whose light can change)
    /// </summary>
    public static bool Shades(ShadingSettings? settings, bool levelHasZones = false)
    {
        var s = settings ?? ShadingResolver.NoFade;
        return s.MaxFade > 0 || s.Light < 255 || levelHasZones;
    }

    /// <summary>
    /// The shade for a frame <paramref name="tiles"/> tiles away (its middle, along the view)
    /// with these settings (null: the level has no shading of its own), standing in
    /// <paramref name="zone"/> (null: lit by the level's light), giving off <paramref name="self"/>
    /// (null: no light of its own)
    /// </summary>
    public FrameShade Shade(PaletteColor[] palette, ShadingSettings? settings, double tiles, bool inHand, bool bright, int lighten,
        bool levelHasZones = false, PreviewZone? zone = null, SelfLight? self = null)
    {
        levelHasZones |= zone != null;
        // a set light turns shading on for a level with none (Lighting.ActorShading)
        bool shades = Shades(settings, levelHasZones) || self is { Sets: true };
        if (!shades)
            return new FrameShade(null, 0, true, settings == null ? "no shading here: nothing fades" : "this shading neither dims nor fades");
        var s = settings ?? ShadingResolver.NoFade;
        var fade = FadeParams.From(s);
        if (bright)
            return new FrameShade(null, 0, true, "drawn bright: unshaded at any distance");

        // Lit as the game's light grid lights the spot it stands on: its tile's light (the
        // zone's, else the level's), with its own light cast round it
        var (level, tint, added) = LightOnItself(ShadeMath.LightLevelOf(zone?.Light ?? s.Light), zone?.Tint ?? ShadingResolver.NoTint, self);
        var row = Rows(palette).Row(level, ShadeMath.LightLevels, ShadeMath.FadeSteps, fade.R, fade.G, fade.B, tint.R, tint.G, tint.B);
        string light = $"light level {level} of {ShadeMath.MaxLightLevel}"
                       + (zone == null ? "" : $" (zone {zone.Id})")
                       + (added == 0 ? "" : $", its light {added:+#;-#} levels")
                       + (tint == ShadingResolver.NoTint ? "" : $", tinted #{tint.R:X2}{tint.G:X2}{tint.B:X2}");
        if (inHand)
            return new FrameShade(row, 0, true, $"in hand: {light}, weapons in hand don't fade");

        long distance = (long)Math.Round(tiles * MapConstants.TileGlobal);
        int height = SpriteShading.ViewHeight(distance, HeightNumerator);
        if (height == 0)
            return new FrameShade(row, 0, false, $"{Tiles(tiles)}: too near to be drawn");

        int far = ShadeMath.ShadeOffsetForHeight(height, HeightNumerator, fade);
        int offset = ShadeMath.Lightened(far, lighten, fade);
        string lightened = lighten == 0 ? "" : $" (lighten {lighten:+#;-#} from step {far / 256})";
        string faded = fade.MaxFadeStep == 0 ? "no fade" : $"fade step {offset / 256} of {fade.MaxFadeStep}{lightened}";
        return new FrameShade(row, offset, true, $"{Tiles(tiles)}: {faded}, {light}");
    }

    private static string Tiles(double tiles) => $"{tiles:0.##} tile{(tiles == 1 ? "" : "s")}";

    // A few tiles of open floor, the thing on the middle one: enough for its own light to be
    // cast round it as the game casts it (PFWolf.Common's LightCaster)
    private const int Patch = 5;
    private readonly LightGrid _grid = new();
    private readonly Dictionary<string, byte[]> _colorTints = new(StringComparer.OrdinalIgnoreCase);

    private sealed class OpenPatch : ILightMap
    {
        public int TileIndexOrNone(int x, int y) => (uint)x < Patch && (uint)y < Patch ? y * Patch + x : -1;
        public bool LightEnters(int x, int y) => true;
        public bool LightPasses(int x, int y) => true;
    }

    private static readonly OpenPatch PatchMap = new();

    /// <summary>
    /// The light level and tint at a thing's own spot (the middle of the patch's middle tile,
    /// where a placed thing stands), its tile at <paramref name="baseLevel"/> and
    /// <paramref name="baseTint"/>, with its own light cast as the game casts it; and how many
    /// levels its light moved it
    /// </summary>
    public (int Level, (byte R, byte G, byte B) Tint, int Added) LightOnItself(int baseLevel, (byte R, byte G, byte B) baseTint, SelfLight? self)
    {
        _grid.SizeTo(Patch, Patch);
        _grid.Tints.Clear();
        _colorTints.Clear();
        _grid.TintIndex(ShadingResolver.NoTint);
        byte tileTint = _grid.TintIndex(baseTint);
        Array.Fill(_grid.TileLevel, (byte)baseLevel);
        Array.Fill(_grid.TileTint, tileTint);
        var caster = new LightCaster(_grid);
        Array.Clear(_grid.CellAdd);
        Array.Clear(_grid.CellStrong);
        Array.Clear(_grid.CellTint);
        Array.Clear(_grid.CellSet);
        Array.Clear(_grid.CellSetWeight);

        int middle = Patch / 2 * (int)MapConstants.TileGlobal + (int)MapConstants.TileGlobal / 2;
        if (self is { } light)
        {
            var tints = LightCaster.ColorTints(light.Color, _colorTints, _grid.TintIndex);
            caster.CastLight(PatchMap, middle, middle, light.Intensity, light.Radius, tints, light.Sets);
        }
        var (level, tint) = _grid.LightAt(PatchMap.TileIndexOrNone(Patch / 2, Patch / 2), middle, middle);
        return (level, _grid.Tints[tint], level - baseLevel);
    }

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
