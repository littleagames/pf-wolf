using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>
/// A screen column's walls, as the ray trace finds them: each wall it hits queues a post
/// (ScalePost, QueuePost), and once the trace is done they're drawn nearest last (DrawPosts):
/// the stories of each wall, wall patches over them, and an arch's underside. Also the arches
/// the trace passes under, tracing on past a wall to the taller ones behind it, and the light a
/// wall face takes from the open tile it's seen from.
/// </summary>
/// <summary>The texture of a plain wall's face, if it is one (WL_DRAW's WallTexture); mirrored: the face is flipped</summary>
internal delegate TextureAsset? WallTextureLookup(int tilex, int tiley, string face, out bool mirrored);

/// <param name="wallTexture">The texture of a plain wall's face (WL_DRAW's WallTexture)</param>
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
    // there, so take its base light. A wall or door tile keeps the one before it (a wall the
    // trace passes, an open door).

    internal void EnterTileLight(int x, int y)
    {
        if (map.tilemap[x, y] == 0)
            render.hittile = Lighting.TileIndex(x, y);
    }

    // Half a light cell, in global units
    const int HALFCELL = 1 << (Lighting.CELLGLOBALSHIFT - 1);

    /// <summary>
    /// The light on a wall face at (x, y) (global units), seen from hittile: from the light cell
    /// just in front of it, half a cell toward the view, not the one inside the wall.
    /// </summary>
    internal byte[] WallLight(int x, int y)
    {
        long gx = x + (render.viewx > x ? HALFCELL : render.viewx < x ? -HALFCELL : 0);
        long gy = y + (render.viewy > y ? HALFCELL : render.viewy < y ? -HALFCELL : 0);
        return lighting.LightAt(render.hittile, gx, gy);
    }


    internal void ScalePost()
    {
        var upper = render.uppersource ?? render.postsource;
        QueuePost(render.postheight, 0, render.hitstories, render.postsource, render.postofs, upper, render.uppersource != null ? render.upperofs : render.postofs,
            patch: render.patchsource, patchofs: render.patchofs, patchstory: render.patchstory);
        render.uppersource = null;
        render.patchsource = null;

        //
        // sprites clip against the nearest wall: nothing behind it is taller than it
        // on screen, since every wall stands at least a story high and sprites don't
        //
        if (!render.columnhit)
        {
            render.wallheight[render.pixx] = render.postheight;
            render.columnhit = true;
        }

        int half = Math.Max(render.postheight >> 3, 1);
        render.columntop = Math.Min(render.columntop, FloorRow(half) - render.hitstories * 2 * half);
        render.columntallest = Math.Max(render.columntallest, render.hitstories);
    }

    internal void QueuePost(short height, int firststory, int stories, TextureAsset lower, int lowerofs, TextureAsset upper, int upperofs,
        short underside = 0, TextureAsset? undertexture = null, byte[]? light = null, bool underflat = false,
        TextureAsset? patch = null, int patchofs = 0, int patchstory = 0)
    {
        if (render.postcount == RenderState.MAXPOSTS)
            return;
        render.postpatch[render.postcount] = patch;
        render.postpatchofs[render.postcount] = patchofs;
        render.postpatchstory[render.postcount] = (byte)patchstory;
        render.postlight[render.postcount] = light ?? WallLight(render.xintercept, render.yintercept);
        render.postunderside[render.postcount] = underside;
        render.postundertexture[render.postcount] = undertexture;
        render.postunderflat[render.postcount] = underflat;
        render.postheights[render.postcount] = height;
        render.postfirststory[render.postcount] = (byte)firststory;
        render.poststories[render.postcount] = (byte)stories;
        render.postlower[render.postcount] = lower;
        render.postlowerofs[render.postcount] = lowerofs;
        render.postupper[render.postcount] = upper;
        render.postupperofs[render.postcount] = upperofs;
        render.postcount++;
    }

    // Draws the column's queued posts, farthest first
    internal void DrawPosts()
    {
        if (weather.weatheron)
            weather.NoteOccluders();            // what hides rain and snow in this column (Weather.cs)

        while (render.postcount > 0)
        {
            render.postcount--;
            DrawPost(render.pixx, render.postheights[render.postcount] >> 3, render.postfirststory[render.postcount], render.poststories[render.postcount],
                render.postlower[render.postcount], render.postlowerofs[render.postcount], render.postupper[render.postcount], render.postupperofs[render.postcount],
                render.postlight[render.postcount], lighting.ShadeOffsetForHeight(render.postheights[render.postcount]));
            if (render.postpatch[render.postcount] is { } patch)
                DrawPatch(render.pixx, render.postheights[render.postcount] >> 3, render.poststories[render.postcount], patch, render.postpatchofs[render.postcount],
                    render.postpatchstory[render.postcount], render.postlight[render.postcount], lighting.ShadeOffsetForHeight(render.postheights[render.postcount]));
            if (render.postunderside[render.postcount] != 0)
                DrawUnderside(render.pixx, render.postheights[render.postcount] >> 3, render.postunderside[render.postcount] >> 3, render.postundertexture[render.postcount],
                    render.postunderflat[render.postcount]);
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
        int to = Math.Min(FloorRow(exithalf) - 2 * exithalf - 1, render.viewheight - 1);
        if (from > to)
            return;
        int pitch = (int)video.bufferPitch;

        unsafe
        {
            byte* dest = (byte*)render.vbufPtr + render.screenofs + x;
            double numerator = render.heightnumerator * 32.0 * skyAndFlats.EyeHalves(GameConstants.TEXTURESIZE);

            //
            // The underside, h half stories above the eye, shows on row y where its edge would at
            // a distance (along the view, as CalcHeight measures it) with h half stories centery - y
            // pixels high: nx = heightnumerator * 32 * h / (centery - y). Going that far along the
            // view means going nx / cos(offset) along the ray. That point's tile also gives its light.
            //
            const double TORADIANS = 2 * Math.PI / GameConstants.FINEANGLES;
            double angle = (render.midangle + render.pixelangle[x]) * TORADIANS;
            double perview = 1 / Math.Cos(render.pixelangle[x] * TORADIANS);
            double stepx = Math.Cos(angle) * perview, stepy = -Math.Sin(angle) * perview;

            byte[]? data = texture?.RawData;
            int width = texture?.Width ?? 0, height = texture?.Height ?? 0;

            for (int y = from; y <= to; y++)
            {
                double nx = numerator / (render.centery - y - 0.5);
                long px = (long)(render.viewx + nx * stepx), py = (long)(render.viewy + nx * stepy);
                var light = lighting.LightAt(Lighting.TileIndex((int)(px >> MapConstants.TILESHIFT), (int)(py >> MapConstants.TILESHIFT)), px, py);
                int shadeofs = lighting.ShadeOffset((long)nx);
                if (data == null)
                {
                    dest[y * pitch] = light[shadeofs + skyAndFlats.ceilingcolor];
                    continue;
                }

                int u = (int)(px >> (MapConstants.TILESHIFT - GameConstants.TEXTURESHIFT)) & (GameConstants.TEXTURESIZE - 1);
                int v = (int)(py >> (MapConstants.TILESHIFT - GameConstants.TEXTURESHIFT)) & (GameConstants.TEXTURESIZE - 1);
                int row = flat || height < GameConstants.TEXTURESIZE ? v * height / GameConstants.TEXTURESIZE : height - GameConstants.TEXTURESIZE + v;
                byte pixel = data[u * width / GameConstants.TEXTURESIZE * height + row];
                if (flat && pixel == SkyAndFlats.FLATMASK)
                    pixel = skyAndFlats.ceilingcolor;
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
    = Its faces take the texture of a wall beside it, and its underside the tile's ceiling flat
    = (a non-zero flat-plane ceiling index), else the wall's texture too.
    =
    ====================
    */


    internal bool IsArch(int tilex, int tiley) => map.tilemap[tilex, tiley] == 0 && map.storymap[tilex, tiley] > 1;

    /// <summary>
    /// The trace enters the arch at (tilex, tiley), crossing its edge at (edgex, edgey); vertical
    /// says whether that's an edge along x = constant (an east or west face).
    /// </summary>
    internal void OpenArch(int tilex, int tiley, int edgex, int edgey, bool vertical)
    {
        // the same face of the wall at either end of the run of arch tiles in line with the face,
        // else at either end of the run across it (a face on the end of a run, entered along it)
        int dx = vertical ? 0 : 1, dy = vertical ? 1 : 0;
        var face = SeenFace(vertical);
        render.archtexture = ArchEndTexture(tilex, tiley, -dx, -dy, face, out bool mirrored) ?? ArchEndTexture(tilex, tiley, dx, dy, face, out mirrored)
            ?? ArchEndTexture(tilex, tiley, -dy, -dx, face, out mirrored) ?? ArchEndTexture(tilex, tiley, dy, dx, face, out mirrored);
        if (render.archtexture == null)
            return;                     // nothing to texture it from: leave it open

        render.archopen = true;
        render.archlight = WallLight(edgex, edgey);
        render.archunderside = ArchUndersideTexture(tilex, tiley) ?? render.archtexture;
        UseArchCeilingFlat(tilex, tiley);
        render.archstories = map.storymap[tilex, tiley];
        render.archheight = HeightAt(edgex, edgey);
        render.archofs = vertical
            ? LintelColumn(edgey, (render.xtilestep == -1) != mirrored)
            : LintelColumn(edgex, (render.ytilestep == 1) != mirrored);
    }

    // The texture on face of the wall that ends the run of arch tiles from (tilex, tiley) going (dx, dy)
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
    /// so it doesn't change as the view moves under it. It's from a wall ending the run of arch
    /// tiles it's in, north or south first, else west or east: its face toward the arch.
    /// </summary>
    internal TextureAsset? ArchUndersideTexture(int tilex, int tiley) =>
        ArchEndTexture(tilex, tiley, 0, -1, "south", out _) ?? ArchEndTexture(tilex, tiley, 0, 1, "north", out _)
        ?? ArchEndTexture(tilex, tiley, -1, 0, "east", out _) ?? ArchEndTexture(tilex, tiley, 1, 0, "west", out _);

    /// <summary>
    /// An arch tile with a ceiling flat (a non-zero ceiling index on the flat plane, its flat
    /// from mapdefs or the level's default ceiling) shows it underneath, in place of the wall texture
    /// </summary>
    internal void UseArchCeilingFlat(int tilex, int tiley)
    {
        render.archunderflat = false;
        if (map.MAPSPOT(tilex, tiley, MapManager.FLATPLANE) >> 8 == 0)
            return;
        var flat = map.ceilingflat[(tiley << MapManager.MAPSHIFT) + tilex];
        if (flat == null)
            return;
        render.archunderside = flat;
        render.archunderflat = true;
    }

    // The view starts under an arch: there's no face, only the underside from above the view
    internal void OpenFocalArch()
    {
        render.archunderside = ArchUndersideTexture(render.focaltx, render.focalty);
        if (render.archunderside == null)
            return;                     // as OpenArch: nothing to texture it from
        UseArchCeilingFlat(render.focaltx, render.focalty);
        render.archtexture = render.notexture;
        render.archopen = true;
        render.archlight = lighting.LightAt(render.hittile, render.viewx, render.viewy);
        render.archstories = map.storymap[render.focaltx, render.focalty];
        render.archheight = short.MaxValue;
        render.archofs = 0;
    }

    // The trace leaves the open arch, crossing its edge at (edgex, edgey)
    internal void CloseArch(int edgex, int edgey)
    {
        if (!render.archopen)
            return;
        render.archopen = false;

        short exitheight = HeightAt(edgex, edgey);
        if (exitheight == 0)
            exitheight = 1;             // 0 means no underside
        QueuePost(render.archheight, 1, render.archstories, render.archtexture!, render.archofs, render.archtexture!, render.archofs, exitheight, render.archunderside, render.archlight, render.archunderflat);
    }

    // The height (as wallheight) of a wall at (x, y)
    internal short HeightAt(int x, int y)
    {
        int savex = render.xintercept, savey = render.yintercept;
        render.xintercept = x;
        render.yintercept = y;
        short height = CalcHeight();
        render.xintercept = savex;
        render.yintercept = savey;
        return height;
    }

    // Where the trace meets the vertical edge it enters xtile by, and the horizontal one it enters ytile by
    internal int VertEdge => (render.xtilestep == 1 ? render.xtile : render.xtile + 1) << MapConstants.TILESHIFT;
    internal int HorizEdge => (render.ytilestep == 1 ? render.ytile : render.ytile + 1) << MapConstants.TILESHIFT;

    /// <summary>
    /// Whether the trace should go on past the wall it just hit, on the tile (tilex, tiley): only
    /// while a taller wall somewhere on the level could still show above everything it has hit.
    /// </summary>
    internal bool TracePastWall(int tilex, int tiley)
    {
        if (render.columntallest >= map.MaxWallStories || render.columntop <= 0 || render.postcount >= RenderState.MAXPOSTS - 1)
            return false;
        // a trace never leaves the map through its edge
        return tilex > 0 && tiley > 0 && tilex < map.mapwidth - 1 && tiley < map.mapheight - 1;
    }

    //
    // the trace's position, kept from before it enters a tile, to carry on past a wall it hits there
    //

    internal void SaveTrace()
    {
        render.savedxtile = render.xtile; render.savedytile = render.ytile;
        render.savedxintercept = render.xintercept; render.savedyintercept = render.yintercept;
        render.savedxinttile = render.xinttile; render.savedyinttile = render.yinttile;
        render.savedtexdelta = render.texdelta;
    }

    internal void RestoreTrace()
    {
        render.xtile = render.savedxtile; render.ytile = render.savedytile;
        render.xintercept = render.savedxintercept; render.yintercept = render.savedyintercept;
        render.xinttile = render.savedxinttile; render.yinttile = render.savedyinttile;
        render.texdelta = render.savedtexdelta;
        render.pastwall = true;
    }

    /// <summary>
    /// Draws one screen column of a wall, standing on the floor, stories tall. Stories
    /// firststory and up are drawn: the first story from lower, the ones above it from upper.
    /// A texture covers a story per 64 texels of its height, from the floor up, and repeats
    /// above that. half is half a story's height in pixels; lowerofs and upperofs are the
    /// column to draw, times 64, as the trace finds it across a tile. Each texel is remapped
    /// through shade from shadeofs (a light row and fade step, see WL_SHADE.cs).
    /// </summary>
    internal void DrawPost(int x, int half, int firststory, int stories, TextureAsset lower, int lowerofs, TextureAsset upper, int upperofs,
        byte[] shade, int shadeofs)
    {
        if (half <= 0) half = 100;

        //
        // row k up from the floor line shows texel k * 32 / half up the wall; step that
        // along as whole texels (t) plus a remainder, so it matches the old ScalePost exactly
        //
        int floor = FloorRow(half);
        int bottom = floor - 1 - firststory * 2 * half;
        int top = floor - stories * 2 * half;
        int y = Math.Min(bottom, render.viewheight - 1);
        if (top < 0) top = 0;
        if (y < top) return;

        long num = (long)(bottom - y) * (GameConstants.TEXTURESIZE / 2);
        long t = num / half + firststory * GameConstants.TEXTURESIZE;
        long rem = num % half;
        int pitch = (int)video.bufferPitch;

        byte[] lowerdata = lower.RawData, upperdata = upper.RawData;
        int lowerheight = lower.Height, upperheight = upper.Height;
        int lowercolumn = TextureColumn(lower, lowerofs), uppercolumn = TextureColumn(upper, upperofs);

        unsafe
        {
            byte* dest = (byte*)render.vbufPtr + render.screenofs + x;
            byte col = 0;
            long colt = -1;             // the t col is for
            for (; y >= top; y--)
            {
                if (t != colt)
                {
                    col = shade[shadeofs + (t < GameConstants.TEXTURESIZE
                        ? lowerdata[lowercolumn + lowerheight - 1 - (int)(t % lowerheight)]
                        : upperdata[uppercolumn + upperheight - 1 - (int)(t % upperheight)])];
                    colt = t;
                }
                dest[y * pitch] = col;

                rem += GameConstants.TEXTURESIZE / 2;
                while (rem >= half)
                {
                    rem -= half;
                    t++;
                }
            }
        }
    }

    const byte PATCHMASK = 0xff;        // a wall patch's see-through pixels, as a flat's

    /// <summary>
    /// Draws one screen column of a wall patch over the wall post just drawn (DrawPost), its
    /// texels laid out the same way: from the floor of story <paramref name="story"/> (1 up) for
    /// as many stories as the patch is tall, or (story 0) on every story of the wall, repeating as
    /// the wall's texture does. Its see-through pixels leave the wall showing.
    /// </summary>
    internal void DrawPatch(int x, int half, int stories, TextureAsset patch, int ofs, int story, byte[] shade, int shadeofs)
    {
        if (half <= 0) half = 100;

        int height = patch.Height;
        int first = story == 0 ? 0 : story - 1;
        int last = story == 0 ? stories : Math.Min(stories, first + (height + GameConstants.TEXTURESIZE - 1) / GameConstants.TEXTURESIZE);
        if (first >= last)
            return;                     // on a story the wall doesn't reach

        int floor = FloorRow(half);
        int bottom = floor - 1 - first * 2 * half;
        int top = floor - last * 2 * half;
        int y = Math.Min(bottom, render.viewheight - 1);
        if (top < 0) top = 0;
        if (y < top) return;

        // texels up from the patch's bottom, stepped as DrawPost steps the wall's
        long num = (long)(bottom - y) * (GameConstants.TEXTURESIZE / 2);
        long t = num / half;
        long rem = num % half;
        int pitch = (int)video.bufferPitch;

        byte[] data = patch.RawData;
        int column = TextureColumn(patch, ofs);

        unsafe
        {
            byte* dest = (byte*)render.vbufPtr + render.screenofs + x;
            for (; y >= top; y--)
            {
                long row = story == 0 ? t % height : t;
                if (row < height)
                {
                    byte pixel = data[column + height - 1 - (int)row];
                    if (pixel != PATCHMASK)
                        dest[y * pitch] = shade[shadeofs + pixel];
                }

                rem += GameConstants.TEXTURESIZE / 2;
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
        (ofs >> GameConstants.TEXTURESHIFT) * texture.Width / GameConstants.TEXTURESIZE * texture.Height;

    /*
    ====================
    =
    = CalcHeight
    =
    = The height (as wallheight) of the wall the trace just hit, from how far along the view its
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
        gx = render.xintercept - render.viewx;
        gy = render.yintercept - render.viewy;

        //
        // calculate nx
        //
        gxt = MathUtils.FixedMul(gx, render.viewcos);
        gyt = MathUtils.FixedMul(gy, render.viewsin);
        nx = gxt - gyt;

        //
        // calculate perspective ratio
        //
        if (nx < GameConstants.MINDIST)
            nx = (int)GameConstants.MINDIST;             // don't let divide overflow

        height = (short)(render.heightnumerator / (nx >> 8));
        return height;
    }

    // The screen row the floor is on, for something half pixels to half a story
    internal int FloorRow(int half) => render.centery + half * render.vieweyez / (GameConstants.TEXTURESIZE / 2);

    // The face of a wall the trace sees, going the way it is
    internal string SeenFace(bool vertical) => vertical
        ? render.xtilestep == 1 ? "west" : "east"
        : render.ytilestep == 1 ? "north" : "south";

    internal static int LintelColumn(int along, bool flip)
    {
        int texture = (along >> GameConstants.FIXED2TEXSHIFT) & GameConstants.TEXTUREMASK;
        return flip ? GameConstants.TEXTUREMASK - texture : texture;
    }
}