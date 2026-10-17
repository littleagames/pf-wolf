using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>
/// A screen column's walls, as the ray trace finds them: each WallId it hits queues a post
/// (ScalePost, QueuePost), and once the trace is done they're drawn nearest last (DrawPosts):
/// the stories of each WallId, WallId patches over them, and an arch's underside. Also the arches
/// the trace passes under, tracing on past a WallId to the taller ones behind it, and the light a
/// WallId face takes from the open tile it's seen from.
/// </summary>
/// <summary>The texture of a plain WallId's face, if it is one (WL_DRAW's WallTexture); mirrored: the face is flipped</summary>
internal delegate TextureAsset? WallTextureLookup(int tilex, int tiley, string face, out bool mirrored);

/// <param name="wallTexture">The texture of a plain WallId's face (WL_DRAW's WallTexture)</param>
internal sealed class WallPosts(
    VideoManager video,
    MapManager map,
    Lighting lighting,
    SkyAndFlats skyAndFlats,
    RenderState render,
    Weather weather,
    GameSession session,
    WallTextureLookup wallTexture)
{
    // The open tile the trace last came from (a TileIndex): walls and doors it hits are seen from
    // there, so take its base light. A WallId or DoorNumber tile keeps the one before it (a WallId the
    // trace passes, an open DoorNumber).

    internal void EnterTileLight(int x, int y)
    {
        if (map.tilemap[x, y] == 0)
            render.HitTile = Lighting.TileIndex(x, y);
    }

    // Half a light cell, in global units
    const int HalfCell = 1 << (Lighting.CellGlobalShift - 1);

    /// <summary>
    /// The light on a WallId face at (x, y) (global units), seen from HitTile: from the light cell
    /// just in front of it, half a cell toward the view, not the one inside the WallId.
    /// </summary>
    internal byte[] WallLight(int x, int y)
    {
        long gx = x + (render.ViewX > x ? HalfCell : render.ViewX < x ? -HalfCell : 0);
        long gy = y + (render.ViewY > y ? HalfCell : render.ViewY < y ? -HalfCell : 0);
        return lighting.LightAt(render.HitTile, gx, gy);
    }


    internal void ScalePost()
    {
        var upper = render.UpperSource ?? render.PostSource;
        QueuePost(render.PostHeight, 0, render.HitStories, render.PostSource, render.PostOfs, upper, render.UpperSource != null ? render.UpperOfs : render.PostOfs,
            patch: render.PatchSource, patchofs: render.PatchOfs, patchstory: render.PatchStory);
        render.UpperSource = null;
        render.PatchSource = null;

        //
        // sprites clip against the nearest WallId: nothing behind it is taller than it
        // on screen, since every WallId stands at least a story high and sprites don't
        //
        if (!render.ColumnHit)
        {
            render.WallHeight[render.PixX] = render.PostHeight;
            render.ColumnHit = true;
        }

        int half = Math.Max(render.PostHeight >> 3, 1);
        render.ColumnTop = Math.Min(render.ColumnTop, FloorRow(half) - render.HitStories * 2 * half);
        render.ColumnTallest = Math.Max(render.ColumnTallest, render.HitStories);
    }

    internal void QueuePost(short height, int firststory, int stories, TextureAsset lower, int lowerofs, TextureAsset upper, int upperofs,
        short underside = 0, TextureAsset? undertexture = null, byte[]? light = null, bool underflat = false,
        TextureAsset? patch = null, int patchofs = 0, int patchstory = 0)
    {
        if (render.PostCount == RenderState.MaxPosts)
            return;
        render.PostPatch[render.PostCount] = patch;
        render.PostPatchOfs[render.PostCount] = patchofs;
        render.PostPatchStory[render.PostCount] = (byte)patchstory;
        render.PostLight[render.PostCount] = light ?? WallLight(render.XIntercept, render.YIntercept);
        render.PostUnderside[render.PostCount] = underside;
        render.PostUnderTexture[render.PostCount] = undertexture;
        render.PostUnderFlat[render.PostCount] = underflat;
        render.PostHeights[render.PostCount] = height;
        render.PostFirstStory[render.PostCount] = (byte)firststory;
        render.PostStories[render.PostCount] = (byte)stories;
        render.PostLower[render.PostCount] = lower;
        render.PostLowerOfs[render.PostCount] = lowerofs;
        render.PostUpper[render.PostCount] = upper;
        render.PostUpperOfs[render.PostCount] = upperofs;
        render.PostCount++;
    }

    // Draws the column's queued posts, farthest first
    internal void DrawPosts()
    {
        if (weather.WeatherOn)
            weather.NoteOccluders();            // what hides rain and snow in this column (Weather.cs)

        while (render.PostCount > 0)
        {
            render.PostCount--;
            DrawPost(render.PixX, render.PostHeights[render.PostCount] >> 3, render.PostFirstStory[render.PostCount], render.PostStories[render.PostCount],
                render.PostLower[render.PostCount], render.PostLowerOfs[render.PostCount], render.PostUpper[render.PostCount], render.PostUpperOfs[render.PostCount],
                render.PostLight[render.PostCount], lighting.ShadeOffsetForHeight(render.PostHeights[render.PostCount]));
            if (render.PostPatch[render.PostCount] is { } patch)
                DrawPatch(render.PixX, render.PostHeights[render.PostCount] >> 3, render.PostStories[render.PostCount], patch, render.PostPatchOfs[render.PostCount],
                    render.PostPatchStory[render.PostCount], render.PostLight[render.PostCount], lighting.ShadeOffsetForHeight(render.PostHeights[render.PostCount]));
            if (render.PostUnderside[render.PostCount] != 0)
                DrawUnderside(render.PixX, render.PostHeights[render.PostCount] >> 3, render.PostUnderside[render.PostCount] >> 3, render.PostUnderTexture[render.PostCount],
                    render.PostUnderFlat[render.PostCount]);
        }
    }

    /// <summary>
    /// Draws an arch's underside, one story up, between where the trace enters it (half a story
    /// is entryhalf pixels there) and where it leaves (exithalf). It's a flat surface, so each
    /// row is textured from where the view ray meets it, from the texture's bottom story
    /// laid flat: one tile of it per tile. A ceiling flat (flat) is sampled as DrawFlats does,
    /// its see-through pixels the ceiling color. With no texture, it's the ceiling color.
    /// </summary>
    internal void DrawUnderside(int x, int entryhalf, int exithalf, TextureAsset? texture, bool flat)
    {
        // one story up from the floor line, as DrawPost puts the top of the arch's first story
        int from = Math.Max(FloorRow(entryhalf) - 2 * entryhalf, 0);
        int to = Math.Min(FloorRow(exithalf) - 2 * exithalf - 1, render.ViewHeight - 1);
        if (from > to)
            return;
        int pitch = (int)video.bufferPitch;

        unsafe
        {
            byte* dest = (byte*)render.VbufPtr + render.ScreenOfs + x;
            double numerator = render.HeightNumerator * 32.0 * skyAndFlats.EyeHalves(GameConstants.TextureSize);

            //
            // The underside, h half stories above the eye, shows on row y where its edge would at
            // a distance (along the view, as CalcHeight measures it) with h half stories CenterY - y
            // pixels high: nx = HeightNumerator * 32 * h / (CenterY - y). Going that far along the
            // view means going nx / cos(offset) along the ray. That point's tile also gives its light.
            //
            const double TORADIANS = 2 * Math.PI / GameConstants.FineAngles;
            double angle = (render.MidAngle + render.PixelAngle[x]) * TORADIANS;
            double perview = 1 / Math.Cos(render.PixelAngle[x] * TORADIANS);
            double stepx = Math.Cos(angle) * perview, stepy = -Math.Sin(angle) * perview;

            byte[]? data = texture?.RawData;
            int width = texture?.Width ?? 0, height = texture?.Height ?? 0;

            for (int y = from; y <= to; y++)
            {
                double nx = numerator / (render.CenterY - y - 0.5);
                long px = (long)(render.ViewX + nx * stepx), py = (long)(render.ViewY + nx * stepy);
                var light = lighting.LightAt(Lighting.TileIndex((int)(px >> MapConstants.TileShift), (int)(py >> MapConstants.TileShift)), px, py);
                int shadeofs = lighting.ShadeOffset((long)nx);
                if (data == null)
                {
                    dest[y * pitch] = light[shadeofs + skyAndFlats.CeilingColor];
                    continue;
                }

                int u = (int)(px >> (MapConstants.TileShift - GameConstants.TextureShift)) & (GameConstants.TextureSize - 1);
                int v = (int)(py >> (MapConstants.TileShift - GameConstants.TextureShift)) & (GameConstants.TextureSize - 1);
                int row = flat || height < GameConstants.TextureSize ? v * height / GameConstants.TextureSize : height - GameConstants.TextureSize + v;
                byte pixel = data[u * width / GameConstants.TextureSize * height + row];
                if (flat && pixel == SkyAndFlats.FlatMask)
                    pixel = skyAndFlats.CeilingColor;
                dest[y * pitch] = light[shadeofs + pixel];
            }
        }
    }

    /*
    ====================
    =
    = Arches
    =
    = An open floor tile with a height (plane 3) above one story is an arch: a block from the
    = second story up to that height, that can be walked under. The trace opens one as it
    = enters the tile and queues it as it leaves, once it knows how deep the underside runs.
    = Its faces take the texture of a WallId beside it, and its underside the tile's ceiling flat
    = (a non-zero flat-plane ceiling index), else the WallId's texture too.
    =
    ====================
    */


    internal bool IsArch(int tilex, int tiley) => map.tilemap[tilex, tiley] == 0 && map.storymap[tilex, tiley] > 1;

    /// <summary>
    /// The trace enters the arch at (TileX, TileY), crossing its edge at (edgex, edgey); vertical
    /// says whether that's an edge along x = constant (an east or west face).
    /// </summary>
    internal void OpenArch(int tilex, int tiley, int edgex, int edgey, bool vertical)
    {
        // the same face of the WallId at either end of the run of arch tiles in line with the face,
        // else at either end of the run across it (a face on the end of a run, entered along it)
        int dx = vertical ? 0 : 1, dy = vertical ? 1 : 0;
        var face = SeenFace(vertical);
        render.ArchTexture = ArchEndTexture(tilex, tiley, -dx, -dy, face, out bool mirrored) ?? ArchEndTexture(tilex, tiley, dx, dy, face, out mirrored)
            ?? ArchEndTexture(tilex, tiley, -dy, -dx, face, out mirrored) ?? ArchEndTexture(tilex, tiley, dy, dx, face, out mirrored);
        if (render.ArchTexture == null)
            return;                     // nothing to texture it from: leave it open

        render.ArchOpen = true;
        render.ArchLight = WallLight(edgex, edgey);
        render.ArchUnderside = ArchUndersideTexture(tilex, tiley) ?? render.ArchTexture;
        UseArchCeilingFlat(tilex, tiley);
        render.ArchStories = map.storymap[tilex, tiley];
        render.ArchHeight = HeightAt(edgex, edgey);
        render.ArchOfs = vertical
            ? LintelColumn(edgey, (render.XTileStep == -1) != mirrored)
            : LintelColumn(edgex, (render.YTileStep == 1) != mirrored);
    }

    // The texture on face of the WallId that ends the run of arch tiles from (TileX, TileY) going (dx, dy)
    internal TextureAsset? ArchEndTexture(int tilex, int tiley, int dx, int dy, string face, out bool mirrored)
    {
        mirrored = false;
        do
        {
            tilex += dx;
            tiley += dy;
            if (tilex < 0 || tiley < 0 || tilex >= map.mapwidth || tiley >= map.mapheight)
                return null;
        }
        while (IsArch(tilex, tiley));

        return wallTexture(tilex, tiley, face, out mirrored);
    }

    /// <summary>
    /// The texture of an arch tile's underside: the same one whichever way the trace enters it,
    /// so it doesn't change as the view moves under it. It's from a WallId ending the run of arch
    /// tiles it's in, north or south first, else west or east: its face toward the arch.
    /// </summary>
    internal TextureAsset? ArchUndersideTexture(int tilex, int tiley) =>
        ArchEndTexture(tilex, tiley, 0, -1, "south", out _) ?? ArchEndTexture(tilex, tiley, 0, 1, "north", out _)
        ?? ArchEndTexture(tilex, tiley, -1, 0, "east", out _) ?? ArchEndTexture(tilex, tiley, 1, 0, "west", out _);

    /// <summary>
    /// An arch tile with a ceiling flat (a non-zero ceiling index on the flat plane, its flat
    /// from mapdefs or the level's default ceiling) shows it underneath, in place of the WallId texture
    /// </summary>
    internal void UseArchCeilingFlat(int tilex, int tiley)
    {
        render.ArchUnderFlat = false;
        if (map.MapSpot(tilex, tiley, MapManager.FlatPlane) >> 8 == 0)
            return;
        var flat = map.ceilingflat[(tiley << MapManager.MapShift) + tilex];
        if (flat == null)
            return;
        render.ArchUnderside = flat;
        render.ArchUnderFlat = true;
    }

    // The view starts under an arch: there's no face, only the underside from above the view
    internal void OpenFocalArch()
    {
        render.ArchUnderside = ArchUndersideTexture(render.FocalTx, render.FocalTy);
        if (render.ArchUnderside == null)
            return;                     // as OpenArch: nothing to texture it from
        UseArchCeilingFlat(render.FocalTx, render.FocalTy);
        render.ArchTexture = render.NoTexture;
        render.ArchOpen = true;
        render.ArchLight = lighting.LightAt(render.HitTile, render.ViewX, render.ViewY);
        render.ArchStories = map.storymap[render.FocalTx, render.FocalTy];
        render.ArchHeight = short.MaxValue;
        render.ArchOfs = 0;
    }

    // The trace leaves the open arch, crossing its edge at (edgex, edgey)
    internal void CloseArch(int edgex, int edgey)
    {
        if (!render.ArchOpen)
            return;
        render.ArchOpen = false;

        short exitheight = HeightAt(edgex, edgey);
        if (exitheight == 0)
            exitheight = 1;             // 0 means no underside
        QueuePost(render.ArchHeight, 1, render.ArchStories, render.ArchTexture!, render.ArchOfs, render.ArchTexture!, render.ArchOfs, exitheight, render.ArchUnderside, render.ArchLight, render.ArchUnderFlat);
    }

    // The height (as WallHeight) of a WallId at (x, y)
    internal short HeightAt(int x, int y)
    {
        int savex = render.XIntercept, savey = render.YIntercept;
        render.XIntercept = x;
        render.YIntercept = y;
        short height = CalcHeight();
        render.XIntercept = savex;
        render.YIntercept = savey;
        return height;
    }

    // Where the trace meets the vertical edge it enters XTile by, and the horizontal one it enters YTile by
    internal int VertEdge => (render.XTileStep == 1 ? render.XTile : render.XTile + 1) << MapConstants.TileShift;
    internal int HorizEdge => (render.YTileStep == 1 ? render.YTile : render.YTile + 1) << MapConstants.TileShift;

    /// <summary>
    /// Whether the trace should go on past the WallId it just hit, on the tile (TileX, TileY): only
    /// while a taller WallId somewhere on the level could still show above everything it has hit.
    /// </summary>
    internal bool TracePastWall(int tilex, int tiley)
    {
        if (render.ColumnTallest >= map.MaxWallStories || render.ColumnTop <= 0 || render.PostCount >= RenderState.MaxPosts - 1)
            return false;
        // a trace never leaves the map through its edge
        return tilex > 0 && tiley > 0 && tilex < map.mapwidth - 1 && tiley < map.mapheight - 1;
    }

    //
    // the trace's position, kept from before it enters a tile, to carry on past a WallId it hits there
    //

    internal void SaveTrace()
    {
        render.SavedXTile = render.XTile; render.SavedYTile = render.YTile;
        render.SavedXIntercept = render.XIntercept; render.SavedYIntercept = render.YIntercept;
        render.SavedXIntTile = render.XIntTile; render.SavedYIntTile = render.YIntTile;
        render.SavedTexDelta = render.TexDelta;
    }

    internal void RestoreTrace()
    {
        render.XTile = render.SavedXTile; render.YTile = render.SavedYTile;
        render.XIntercept = render.SavedXIntercept; render.YIntercept = render.SavedYIntercept;
        render.XIntTile = render.SavedXIntTile; render.YIntTile = render.SavedYIntTile;
        render.TexDelta = render.SavedTexDelta;
        render.PastWall = true;
    }

    /// <summary>
    /// Draws one screen column of a WallId, standing on the floor, stories tall. Stories
    /// firststory and up are drawn: the first story from lower, the ones above it from upper.
    /// A texture covers a story per 64 texels of its height, from the floor up, and repeats
    /// above that. half is half a story's height in pixels; lowerofs and UpperOfs are the
    /// column to draw, times 64, as the trace finds it across a tile. Each texel is remapped
    /// through shade from shadeofs (a light row and fade step, see WL_SHADE.cs).
    /// </summary>
    internal void DrawPost(int x, int half, int firststory, int stories, TextureAsset lower, int lowerofs, TextureAsset upper, int upperofs,
        byte[] shade, int shadeofs)
    {
        if (half <= 0) half = 100;

        //
        // row k up from the floor line shows texel k * 32 / half up the WallId; step that
        // along as whole texels (t) plus a remainder, so it matches the old ScalePost exactly
        //
        int floor = FloorRow(half);
        int bottom = floor - 1 - firststory * 2 * half;
        int top = floor - stories * 2 * half;
        int y = Math.Min(bottom, render.ViewHeight - 1);
        if (top < 0) top = 0;
        if (y < top) return;

        long num = (long)(bottom - y) * (GameConstants.TextureSize / 2);
        long t = num / half + firststory * GameConstants.TextureSize;
        long rem = num % half;
        int pitch = (int)video.bufferPitch;

        byte[] lowerdata = lower.RawData, upperdata = upper.RawData;
        int lowerheight = lower.Height, upperheight = upper.Height;
        int lowercolumn = TextureColumn(lower, lowerofs), uppercolumn = TextureColumn(upper, upperofs);

        unsafe
        {
            byte* dest = (byte*)render.VbufPtr + render.ScreenOfs + x;
            byte col = 0;
            long colt = -1;             // the t col is for
            for (; y >= top; y--)
            {
                if (t != colt)
                {
                    col = shade[shadeofs + (t < GameConstants.TextureSize
                        ? lowerdata[lowercolumn + lowerheight - 1 - (int)(t % lowerheight)]
                        : upperdata[uppercolumn + upperheight - 1 - (int)(t % upperheight)])];
                    colt = t;
                }
                dest[y * pitch] = col;

                rem += GameConstants.TextureSize / 2;
                while (rem >= half)
                {
                    rem -= half;
                    t++;
                }
            }
        }
    }

    const byte PatchMask = 0xff;        // a WallId patch's see-through pixels, as a flat's

    /// <summary>
    /// Draws one screen column of a WallId patch over the WallId post just drawn (DrawPost), its
    /// texels laid out the same way: from the floor of story <paramref name="story"/> (1 up) for
    /// as many stories as the patch is tall, or (story 0) on every story of the WallId, repeating as
    /// the WallId's texture does. Its see-through pixels leave the WallId showing.
    /// </summary>
    internal void DrawPatch(int x, int half, int stories, TextureAsset patch, int ofs, int story, byte[] shade, int shadeofs)
    {
        if (half <= 0) half = 100;

        int height = patch.Height;
        int first = story == 0 ? 0 : story - 1;
        int last = story == 0 ? stories : Math.Min(stories, first + (height + GameConstants.TextureSize - 1) / GameConstants.TextureSize);
        if (first >= last)
            return;                     // on a story the WallId doesn't reach

        int floor = FloorRow(half);
        int bottom = floor - 1 - first * 2 * half;
        int top = floor - last * 2 * half;
        int y = Math.Min(bottom, render.ViewHeight - 1);
        if (top < 0) top = 0;
        if (y < top) return;

        // texels up from the patch's bottom, stepped as DrawPost steps the WallId's
        long num = (long)(bottom - y) * (GameConstants.TextureSize / 2);
        long t = num / half;
        long rem = num % half;
        int pitch = (int)video.bufferPitch;

        byte[] data = patch.RawData;
        int column = TextureColumn(patch, ofs);

        unsafe
        {
            byte* dest = (byte*)render.VbufPtr + render.ScreenOfs + x;
            for (; y >= top; y--)
            {
                long row = story == 0 ? t % height : t;
                if (row < height)
                {
                    byte pixel = data[column + height - 1 - (int)row];
                    if (pixel != PatchMask)
                        dest[y * pitch] = shade[shadeofs + pixel];
                }

                rem += GameConstants.TextureSize / 2;
                while (rem >= half)
                {
                    rem -= half;
                    t++;
                }
            }
        }
    }

    // Where the column the trace found (0 to 63, times 64) starts in the texture's pixels
    internal static int TextureColumn(TextureAsset texture, int ofs) =>
        (ofs >> GameConstants.TextureShift) * texture.Width / GameConstants.TextureSize * texture.Height;

    /*
    ====================
    =
    = CalcHeight
    =
    = The height (as WallHeight) of the WallId the trace just hit, from how far along the view its
    = intercept is
    =
    ====================
    */

    internal short CalcHeight()
    {
        short height;
        int gx, gy, gxt, gyt, nx;


        //
        // translate point to view centered coordinates
        //
        gx = render.XIntercept - render.ViewX;
        gy = render.YIntercept - render.ViewY;

        //
        // calculate nx
        //
        gxt = MathUtils.FixedMul(gx, render.ViewCos);
        gyt = MathUtils.FixedMul(gy, render.ViewSin);
        nx = gxt - gyt;

        //
        // calculate perspective ratio
        //
        if (nx < GameConstants.MinDist)
            nx = (int)GameConstants.MinDist;             // don't let divide overflow

        height = (short)(render.HeightNumerator / (nx >> 8));
        return height;
    }

    // The screen row the floor is on, for something half pixels to half a story
    internal int FloorRow(int half) => render.CenterY + half * render.ViewEyeZ / (GameConstants.TextureSize / 2);

    // The face of a WallId the trace sees, going the way it is
    internal string SeenFace(bool vertical) => vertical
        ? render.XTileStep == 1 ? "west" : "east"
        : render.YTileStep == 1 ? "north" : "south";

    internal static int LintelColumn(int along, bool flip)
    {
        int texture = (along >> GameConstants.Fixed2TexShift) & GameConstants.TextureMask;
        return flip ? GameConstants.TextureMask - texture : texture;
    }
}