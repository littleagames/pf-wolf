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
}
