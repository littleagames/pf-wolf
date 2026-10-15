using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf;

internal partial class Program
{
    // Wall sprites' geometry (Rendering/WallSprites.cs) and their blocking on the level
    // (Rendering/WallSprites.cs WallSpriteBlocking)
    static WallSpriteBlocking wallSpriteBlocking = null!;       // made in InitServices

    internal const string WallSpriteFlag = WallSprites.WallSpriteFlag;
    internal static bool IsWallSprite(Entities.Actors.Actor actor) => WallSprites.IsWallSprite(actor);
    internal static string WallSpriteAxis(objdirtypes dir) => WallSprites.WallSpriteAxis(dir);
    internal static WallSpriteSpan? GetWallSpriteSpan(Entities.Actors.Actor actor) => WallSprites.GetWallSpriteSpan(actor);

    static int wallspritefromx { get => wallSpriteBlocking.wallspritefromx; set => wallSpriteBlocking.wallspritefromx = value; }
    static int wallspritefromy { get => wallSpriteBlocking.wallspritefromy; set => wallSpriteBlocking.wallspritefromy = value; }
    internal static WallSpriteSpan? WallSpriteHitByBox(int xl, int yl, int xh, int yh, long x, long y, long size, bool player) =>
        wallSpriteBlocking.WallSpriteHitByBox(xl, yl, xh, yh, x, y, size, player);
    internal static bool WallSpriteBlocksLine(long x1, long y1, long x2, long y2) => wallSpriteBlocking.WallSpriteBlocksLine(x1, y1, x2, y2);

    // Drawing wall sprites (Rendering/WallSpriteDrawing.cs)
    static WallSpriteDrawing wallSpriteDrawing = null!;     // made in InitServices

    static WallSpriteDrawing MakeWallSpriteDrawing() => new(_assetManager, _mapManager, lighting, render, spriteScaler, FloorRow);

    internal static bool TransformWallSprite(Entities.Actors.Actor actor, visobj_t vis) => wallSpriteDrawing.TransformWallSprite(actor, vis);
    internal static bool WallSpriteSeen(Entities.Actors.Actor actor) => wallSpriteDrawing.WallSpriteSeen(actor);
    internal static void ScaleWallSprite(visobj_t vis) => wallSpriteDrawing.ScaleWallSprite(vis);
}