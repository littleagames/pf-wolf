using PFWolf.Assets;

namespace PFWolf;

internal static partial class GameHost
{
    // Drawing sprites scaled onto the view (Rendering/SpriteScaler.cs)
    static SpriteScaler spriteScaler = null!;       // made in InitServices

    static SpriteScaler MakeSpriteScaler() => new(_assetManager, _videoManager, lighting, render, FloorRow);

    internal static void ScaleColumn(short x, short toppix, int fracstep, SpriteAsset sprite, int column, byte[] shade, int shadeofs,
        bool cloaked = false, SpriteTranslation? translation = null) =>
        spriteScaler.ScaleColumn(x, toppix, fracstep, sprite, column, shade, shadeofs, cloaked, translation);
    internal static void ScaleShape(visobj_t sprite) => spriteScaler.ScaleShape(sprite);
    internal static void SimpleScaleShape(int dispx, string shapenum, int dispheight, byte[]? shade = null, int dropy = 0) =>
        spriteScaler.SimpleScaleShape(dispx, shapenum, dispheight, shade, dropy);
    internal static int TexelsToPixels(int texels, int half) => SpriteScaler.TexelsToPixels(texels, half);
}
