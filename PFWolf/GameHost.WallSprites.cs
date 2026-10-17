using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf;

internal static partial class GameHost
{
    // Wall sprites' geometry (Rendering/WallSprites.cs) and their blocking on the level
    // (Rendering/WallSprites.cs WallSpriteBlocking)
    static WallSpriteBlocking wallSpriteBlocking = null!;       // made in InitServices

    internal static bool IsWallSprite(Entities.Actors.Actor actor) => WallSprites.IsWallSprite(actor);

    static int wallspritefromx { get => wallSpriteBlocking.wallspritefromx; set => wallSpriteBlocking.wallspritefromx = value; }
    static int wallspritefromy { get => wallSpriteBlocking.wallspritefromy; set => wallSpriteBlocking.wallspritefromy = value; }
    internal static WallSpriteSpan? WallSpriteHitByBox(int xl, int yl, int xh, int yh, long x, long y, long size, bool player) =>
        wallSpriteBlocking.WallSpriteHitByBox(xl, yl, xh, yh, x, y, size, player);
    internal static bool WallSpriteBlocksLine(long x1, long y1, long x2, long y2) => wallSpriteBlocking.WallSpriteBlocksLine(x1, y1, x2, y2);

    // Drawing wall sprites (Rendering/WallSpriteDrawing.cs)
    static WallSpriteDrawing wallSpriteDrawing = null!;     // made in InitServices

    static WallSpriteDrawing MakeWallSpriteDrawing() => new(_assetManager, _mapManager, lighting, render, spriteScaler, FloorRow);
}
