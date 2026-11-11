namespace PFWolf.Shading;

/// <summary>
/// How far away a billboard sprite is, as the renderer measures it for its size and its fade:
/// for previews that draw a frame shaded as the game would at some distance. The constants are
/// the game's GameConstants ones (a test checks they agree).
/// </summary>
public static class SpriteShading
{
    /// <summary>A sprite's distance is taken to its near side, this far in front of its middle (GameConstants.ActorSize)</summary>
    public const int ActorSize = 0x4000;

    /// <summary>Nearer than this (after ActorSize) a sprite isn't drawn (GameConstants.MinDist)</summary>
    public const long MinDist = 0x5800;

    /// <summary>GameConstants.FocalLength and ViewGlobal, for the view's height numerator</summary>
    public const long FocalLength = 0x5700;
    public const int ViewGlobal = 0x10000;

    /// <summary>
    /// The classic window's projection width: a 320×200 screen at the default view size (19,
    /// 19 × 16 pixels wide), so a preview steps its fade where a classic window does
    /// </summary>
    public const int ClassicProjectionWidth = 19 * 16;

    /// <summary>The height numerator of a view this many pixels wide (ViewSetup.CalcProjection at the usual focal length)</summary>
    public static int HeightNumerator(int projectionWidth)
    {
        double facedist = FocalLength + MinDist;
        int scale = (int)(projectionWidth / 2 * facedist / (ViewGlobal / 2));
        return (int)((MapConstants.TileGlobal * scale) >> 6);
    }

    /// <summary>
    /// A sprite's height on screen (as the renderer's ViewHeight) with its middle this far along
    /// the view (global units, TileGlobal a tile), as SpriteDrawing.TransformActor works it out;
    /// 0 when it's too near to be drawn
    /// </summary>
    public static int ViewHeight(long distance, int heightNumerator)
    {
        long nx = distance - ActorSize;
        if (nx < MinDist)
            return 0;
        return (short)(ushort)(heightNumerator / (int)(nx >> 8));
    }
}
