using Wolf3D.Assets;
using Wolf3D.Entities.Actors;
using Wolf3D.Enums;

namespace Wolf3D;

/*
=============================================================================

                        AUTOMAP GRAPHIC MODE

    Draws the automap with the level's own art: seen walls filled with their
    textures, doors and the moving pushwall textured in place, and actors as
    their sprites. The tile layer is drawn a pixel at a time, mapping each view
    pixel back to the map, so zoom and rotation cost the same as a plain view.

=============================================================================
*/

internal partial class Program
{
    // Below this zoom (virtual pixels per tile) sprites are too small to make out, so they're drawn as dots
    const float AUTOMAP_MINSPRITEZOOM = 6f;

    // A door is drawn as a strip this wide (in tiles) through the middle of its tile
    const float AUTOMAP_DOORWIDTH = 0.2f;

    // Sprites are scaled so their visible part fits this much of a tile
    const float AUTOMAP_SPRITESIZE = 0.9f;

    const int AUTOMAP_TEXSIZE = 64;     // wall textures are 64x64, stored column by column

    static readonly Dictionary<string, byte[]?> automapTextures = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, AutomapSprite?> automapSprites = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A sprite and the box around its opaque pixels, which is what the map shows.</summary>
    sealed record AutomapSprite(SpriteAsset Asset, int Left, int Top, int Width, int Height);

    // Filled each frame so the per-pixel loop never looks anything up by name: the textures of each
    // seen wall tile's north/south and east/west faces, and which seen door (index + 1) a tile holds.
    static readonly byte[]?[,] automapWallNorth = new byte[]?[MapManager.MAPSIZE, MapManager.MAPSIZE];
    static readonly byte[]?[,] automapWallEast = new byte[]?[MapManager.MAPSIZE, MapManager.MAPSIZE];
    static readonly int[,] automapDoorAt = new int[MapManager.MAPSIZE, MapManager.MAPSIZE];
    static byte[]?[] automapDoorTextures = [];

    /// <summary>A wall texture's pixels, or null if it's missing or not 64x64.</summary>
    static byte[]? AutomapTexture(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        if (!automapTextures.TryGetValue(name, out var pixels))
        {
            pixels = _assetManager.Find<TextureAsset>(name)?.RawData;
            if (pixels != null && pixels.Length < AUTOMAP_TEXSIZE * AUTOMAP_TEXSIZE)
                pixels = null;
            automapTextures[name] = pixels;
        }

        return pixels;
    }

    /// <summary>The textures a wall tile shows on its north/south and east/west faces.</summary>
    static (byte[]? North, byte[]? East) AutomapWallTextures(MapObjectTranslationAsset? mapDefs, int tile)
    {
        MapTextureTranslation? textures = null;
        if (mapDefs == null || !mapDefs.Walls.TryGetValue(tile, out textures))
            return (null, null);

        return (AutomapTexture(textures.North), AutomapTexture(textures.East));
    }

    /// <summary>
    /// Fills the seen walls, doors and the moving pushwall with their textures, over the backdrop.
    /// </summary>
    static void DrawAutomapTexels(AutomapView view, bool reveal)
    {
        var mapDefs = _mapManager.GetMapData();

        //
        // what each tile shows this frame
        //
        var wallTextures = new Dictionary<int, (byte[]? North, byte[]? East)>();
        for (int x = 0; x < MapManager.MAPSIZE; x++)
        {
            for (int y = 0; y < MapManager.MAPSIZE; y++)
            {
                automapWallNorth[x, y] = automapWallEast[x, y] = null;
                automapDoorAt[x, y] = 0;

                if (!IsAutomapWall(x, y))
                    continue;

                // Revealed, only the walls that face open space: the solid mass behind them could
                // never be seen, and filling it in buries the level's shape. A diagonal always
                // faces its own open half.
                bool visible = reveal
                    ? !IsAutomapWall(x - 1, y) || !IsAutomapWall(x + 1, y) || !IsAutomapWall(x, y - 1) || !IsAutomapWall(x, y + 1)
                      || _mapManager.wallshape[x, y] != WallShape.Square
                    : _mapManager.seen[x, y] != SeenFlags.None;
                if (!visible)
                    continue;

                int tile = _mapManager.tilemap[x, y];
                if (!wallTextures.TryGetValue(tile, out var textures))
                    wallTextures[tile] = textures = AutomapWallTextures(mapDefs, tile);

                (automapWallNorth[x, y], automapWallEast[x, y]) = textures;
            }
        }

        if (automapDoorTextures.Length < lastdoorobj)
            automapDoorTextures = new byte[]?[doorobjlist.Length];

        for (int i = 0; i < lastdoorobj; i++)
        {
            var door = doorobjlist[i];
            if (!reveal && _mapManager.seen[door.tilex, door.tiley] == SeenFlags.None)
                continue;

            automapDoorAt[door.tilex, door.tiley] = i + 1;
            automapDoorTextures[i] = AutomapTexture(door.vertical ? door.xlat.East : door.xlat.North);
        }

        // The pushwall on the move, if any: its corner on the map and its textures
        bool pushwall = false;
        float pushX = 0, pushY = 0;
        (byte[]? North, byte[]? East) pushTextures = default;
        if (pwallstate != 0)
        {
            int dx = dirs[(int)pwalldir][0], dy = dirs[(int)pwalldir][1];
            pushwall = reveal || _mapManager.seen[pwallx, pwally] != SeenFlags.None
                              || _mapManager.seen[pwallx + dx, pwally + dy] != SeenFlags.None;
            pushX = pwallx + dx * pwallpos / 64f;
            pushY = pwally + dy * pwallpos / 64f;
            pushTextures = AutomapWallTextures(mapDefs, pwalltile);
        }

        //
        // each view pixel, mapped back onto the map
        //
        float stepX = view.Cos / view.TileSize;     // map distance per pixel to the right
        float stepY = -view.Sin / view.TileSize;

        IntPtr destPtr = _videoManager.LockSurface();
        if (destPtr == IntPtr.Zero)
            return;

        unsafe
        {
            byte* dest = (byte*)destPtr;

            for (int sy = view.ClipY; sy < view.ClipY + view.ClipHeight; sy++)
            {
                float dy = sy + 0.5f - view.MidY;
                float dx = view.ClipX + 0.5f - view.MidX;
                float mx = view.CenterX + (dx * view.Cos + dy * view.Sin) / view.TileSize;
                float my = view.CenterY + (-dx * view.Sin + dy * view.Cos) / view.TileSize;

                byte* row = dest + _videoManager.ylookup[sy];

                for (int sx = view.ClipX; sx < view.ClipX + view.ClipWidth; sx++, mx += stepX, my += stepY)
                {
                    int texel;

                    if (pushwall && mx >= pushX && mx < pushX + 1 && my >= pushY && my < pushY + 1)
                        texel = AutomapWallTexel(pushTextures.North, pushTextures.East, mx - pushX, my - pushY);
                    else
                        texel = AutomapTileTexel(mx, my);

                    if (texel >= 0)
                        row[sx] = (byte)texel;
                }
            }
        }

        _videoManager.UnlockSurface();
    }

    /// <summary>The color of a map position in the tile layer, or -1 where the backdrop shows through.</summary>
    static int AutomapTileTexel(float mx, float my)
    {
        if (mx < 0 || my < 0)
            return -1;

        int tx = (int)mx, ty = (int)my;
        if (tx >= MapManager.MAPSIZE || ty >= MapManager.MAPSIZE)
            return -1;

        float fx = mx - tx, fy = my - ty;

        var north = automapWallNorth[tx, ty];
        var east = automapWallEast[tx, ty];
        if (north != null || east != null)
        {
            // a diagonal wall fills only its solid half; the backdrop shows through the open one
            var shape = _mapManager.wallshape[tx, ty];
            if (shape != WallShape.Square && !InDiagonalSolid(shape, fx, fy))
                return -1;

            return AutomapWallTexel(north, east, fx, fy);
        }

        int door = automapDoorAt[tx, ty];
        if (door == 0)
            return -1;

        var doorobj = doorobjlist[door - 1];
        var texture = automapDoorTextures[door - 1];
        if (texture == null)
            return -1;

        // Across the strip and along the door: the door is solid from `position` to the far side
        // of its tile, and its texture slides with it, as in the 3D view
        float across = doorobj.vertical ? fx : fy;
        float along = doorobj.vertical ? fy : fx;
        float open = doorobj.position / 65536f;

        float edge = 0.5f - AUTOMAP_DOORWIDTH / 2;
        if (across < edge || across >= edge + AUTOMAP_DOORWIDTH || along < open)
            return -1;

        int column = Math.Min((int)((along - open) * AUTOMAP_TEXSIZE), AUTOMAP_TEXSIZE - 1);
        int texrow = Math.Min((int)((across - edge) / AUTOMAP_DOORWIDTH * AUTOMAP_TEXSIZE), AUTOMAP_TEXSIZE - 1);
        return texture[column * AUTOMAP_TEXSIZE + texrow];
    }

    /// <summary>
    /// A wall tile's color at (fx, fy) within it. The tile is split corner to corner: the parts
    /// nearest its north and south edges use the north/south face texture, the parts nearest east
    /// and west the (darker) east/west one, so each block reads as raised with shaded sides.
    /// </summary>
    static int AutomapWallTexel(byte[]? north, byte[]? east, float fx, float fy)
    {
        bool nearNorthSouth = MathF.Min(fy, 1 - fy) < MathF.Min(fx, 1 - fx);
        var texture = nearNorthSouth ? north ?? east : east ?? north;
        if (texture == null)
            return -1;

        int column = Math.Min((int)(fx * AUTOMAP_TEXSIZE), AUTOMAP_TEXSIZE - 1);
        int row = Math.Min((int)(fy * AUTOMAP_TEXSIZE), AUTOMAP_TEXSIZE - 1);
        return texture[column * AUTOMAP_TEXSIZE + row];
    }

    /// <summary>
    /// Draws an actor as its current sprite, upright whichever way the map is turned, centered on
    /// its screen position (sx, sy). Enemies show their front-facing frame. Returns false if the
    /// actor has no sprite to draw, so the caller can draw a dot instead.
    /// </summary>
    static bool DrawAutomapSprite(AutomapView view, Entities.Actors.Actor actor, float sx, float sy)
    {
        var state = actor.CurrentState!;
        string shape = HasDirectionalSprites(state.Sprite, state.FrameLetter)
            ? $"{state.Sprite}{state.FrameLetter}1"
            : state.GetShapeName(objdirtypes.nodir);

        var sprite = FindAutomapSprite(shape);
        if (sprite == null)
            return false;

        // Screen pixels per sprite pixel, fitting the sprite's visible part into most of a tile
        float scale = view.TileSize * AUTOMAP_SPRITESIZE / Math.Max(sprite.Width, sprite.Height);
        float left = sx - sprite.Width * scale / 2, top = sy - sprite.Height * scale / 2;

        int x0 = Math.Max(view.ClipX, (int)MathF.Floor(left));
        int y0 = Math.Max(view.ClipY, (int)MathF.Floor(top));
        int x1 = Math.Min(view.ClipX + view.ClipWidth, (int)MathF.Ceiling(left + sprite.Width * scale));
        int y1 = Math.Min(view.ClipY + view.ClipHeight, (int)MathF.Ceiling(top + sprite.Height * scale));
        if (x1 <= x0 || y1 <= y0)
            return true;                                    // off the view: nothing to draw, but not missing

        var asset = sprite.Asset;

        IntPtr destPtr = _videoManager.LockSurface();
        if (destPtr == IntPtr.Zero)
            return true;

        unsafe
        {
            byte* dest = (byte*)destPtr;

            for (int y = y0; y < y1; y++)
            {
                int v = (int)((y + 0.5f - top) / scale);
                if (v < 0 || v >= sprite.Height)
                    continue;

                byte* row = dest + _videoManager.ylookup[y];
                int srcRow = (sprite.Top + v) * asset.Width + sprite.Left;

                for (int x = x0; x < x1; x++)
                {
                    int u = (int)((x + 0.5f - left) / scale);
                    if (u < 0 || u >= sprite.Width)
                        continue;

                    if (asset.OpacityMask[srcRow + u] != 0)
                        row[x] = asset.RawData[srcRow + u];
                }
            }
        }

        _videoManager.UnlockSurface();
        return true;
    }

    /// <summary>A sprite trimmed to its opaque pixels, or null if it's missing or blank.</summary>
    static AutomapSprite? FindAutomapSprite(string shape)
    {
        if (automapSprites.TryGetValue(shape, out var cached))
            return cached;

        AutomapSprite? sprite = null;
        var asset = _assetManager.Find<SpriteAsset>(shape);
        if (asset != null && asset.OpacityMask.Length >= asset.Width * asset.Height)
        {
            int left = asset.Width, top = asset.Height, right = -1, bottom = -1;
            for (int y = 0; y < asset.Height; y++)
            {
                for (int x = 0; x < asset.Width; x++)
                {
                    if (asset.OpacityMask[y * asset.Width + x] == 0)
                        continue;

                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }

            if (right >= 0)
                sprite = new AutomapSprite(asset, left, top, right - left + 1, bottom - top + 1);
        }

        automapSprites[shape] = sprite;
        return sprite;
    }
}
