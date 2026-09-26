using SDL2;
using System.Runtime.InteropServices;
using Wolf3D.Assets;
using Wolf3D.Constants;
using Wolf3D.Entities.Actors;
using Wolf3D.Enums;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                   LOCAL CONSTANTS

    =============================================================================
    */

    internal const int ACTORSIZE = 0x4000;

    /*
    =============================================================================

                                  GLOBAL VARIABLES

    =============================================================================
    */

    static int vbuf; // pointer index
    static IntPtr vbufPtr = IntPtr.Zero;

    static visobj_t[] vislist = new visobj_t[MAXVISABLE];

    static int lasttimecount;
    static int frameon;
    static bool fpscounter = false;

    static int fps_frames = 0, fps_time = 0, fps = 0;

#if USE_FLOORCEILINGTEXT || USE_CLOUDSKY
    short[] spanstart;
#endif

    internal static short[] wallheight;

    //
    // math tables
    //
    internal static short[] pixelangle;
    internal static int[] finetangent = new int[FINEANGLES / 4];
    internal static int[] sintable = new int[ANGLES + ANGLES / 4];
    internal static int[] costable => sintable[(ANGLES/4) ..]; // same as sintable, just offset by ANGLES/4

    //
    // refresh variables
    internal static int viewx, viewy;
    internal static short viewangle;
    internal static int viewsin, viewcos;

    internal static int postx;
    static readonly TextureAsset notexture = new() { RawData = new byte[TEXTURESIZE * TEXTURESIZE] };

    internal static TextureAsset postsource = notexture;    // the texture of the post ScalePost draws
    internal static int postofs;                // its column, times 64 (as the texture's across a tile)
    static TextureAsset? uppersource;           // stories above the first, if not postsource (door lintels)
    static int upperofs;

    //
    // tall walls: every wall is wallstories 64 unit stories tall, built up from the floor
    //
    internal const int MAXWALLSTORIES = 8;
    internal static int wallstories = 1;

    //
    // ray tracing variables
    //
    internal static short focaltx, focalty;
    internal static uint xpartialup, xpartialdown, ypartialup, ypartialdown;

    internal static short midangle;

    internal static ushort tilehit;
    internal static int pixx;

    internal static short xtile, ytile;
    internal static short xtilestep, ytilestep;
    internal static int xintercept, yintercept;
    internal static int xinttile, yinttile;
    internal static ushort texdelta;

    internal static ushort[] horizwall = new ushort[MAXWALLTILES];
    internal static ushort[] vertwall = new ushort[MAXWALLTILES];

    /*
=====================
=
= CalcTics
=
=====================
*/

    internal static void CalcTics()
    {
        //
        // calculate tics since last refresh for adaptive timing
        //
        if (lasttimecount > (int)GameEngineManager.GetTimeCount())
            lasttimecount = (int)GameEngineManager.GetTimeCount();    // if the game was paused a LONG time

        uint curtime = SDL.SDL_GetTicks();
        tics = (uint)((curtime * 7) / 100 - lasttimecount); // TODO: Rounding?
        if (tics == 0)
        {
            // wait until end of current tic
            GameEngineManager.DelayMs((uint)(((lasttimecount + 1) * 100) / 7 - curtime)); // TODO: Rounding error?
            tics = 1;
        }

        lasttimecount += (int)tics;

        if (tics > MAXTICS)
            tics = MAXTICS;
    }

    internal static short CalcHeight()
    {
        short height;
        int gx, gy, gxt, gyt, nx;


        //
        // translate point to view centered coordinates
        //
        gx = xintercept - viewx;
        gy = yintercept - viewy;

        //
        // calculate nx
        //
        gxt = MathUtils.FixedMul(gx, viewcos);
        gyt = MathUtils.FixedMul(gy, viewsin);
        nx = gxt - gyt;

        //
        // calculate perspective ratio
        //
        if (nx < MINDIST)
            nx = (int)MINDIST;             // don't let divide overflow

        height = (short)(heightnumerator / (nx >> 8));
        return height;
    }

    internal static void Setup3DView()
    {
        viewangle = player.Angle;
        midangle = (short)(viewangle * (FINEANGLES / ANGLES));

        viewsin = sintable[viewangle];
        viewcos = costable[viewangle];

        viewx = player.X - MathUtils.FixedMul(focallength, viewcos);
        viewy = player.Y + MathUtils.FixedMul(focallength, viewsin);

        focaltx = (short)(viewx >> (int)MapConstants.TILESHIFT);
        focalty = (short)(viewy >> (int)MapConstants.TILESHIFT);

        xpartialdown = (uint)(viewx & (MapConstants.TILEGLOBAL - 1));
        xpartialup = (uint)(xpartialdown ^ (MapConstants.TILEGLOBAL - 1));
        ypartialdown = (uint)(viewy & (MapConstants.TILEGLOBAL - 1));
        ypartialup = (uint)(ypartialdown ^ (MapConstants.TILEGLOBAL - 1));
    }

    //==========================================================================

    /*
    ===================
    =
    = ScalePost
    =
    ===================
    */
    //
    // A column's trace can go on past a wall, when a taller one behind might show over it. So
    // rather than drawing what it hits straight away, it queues a post for each wall (and door
    // lintel) nearest first, and the queue is drawn farthest first: in one screen column, that
    // puts everything in front of what's behind it.
    //
    const int MAXPOSTS = 16;
    static int postcount;
    static readonly short[] postheights = new short[MAXPOSTS];
    static readonly byte[] postfirststory = new byte[MAXPOSTS];
    static readonly byte[] poststories = new byte[MAXPOSTS];
    static readonly TextureAsset[] postlower = new TextureAsset[MAXPOSTS];
    static readonly int[] postlowerofs = new int[MAXPOSTS];
    static readonly TextureAsset[] postupper = new TextureAsset[MAXPOSTS];
    static readonly int[] postupperofs = new int[MAXPOSTS];
    static readonly short[] postunderside = new short[MAXPOSTS];   // an arch's: the height where the trace leaves it, 0 for none

    internal static short postheight;   // the height (as wallheight) of the post ScalePost queues
    static int hitstories;              // how many stories tall the wall the trace just hit is
    static bool columnhit;              // this column's trace has hit a wall: wallheight is set
    static int columntallest;           // the most stories of any wall it has hit
    static int columntop;               // the highest screen row any of them reach

    internal static void ScalePost()
    {
        var upper = uppersource ?? postsource;
        QueuePost(postheight, 0, hitstories, postsource, postofs, upper, uppersource != null ? upperofs : postofs);
        uppersource = null;

        //
        // sprites clip against the nearest wall: nothing behind it is taller than it
        // on screen, since every wall stands at least a story high and sprites don't
        //
        if (!columnhit)
        {
            wallheight[pixx] = postheight;
            columnhit = true;
        }

        int half = Math.Max(postheight >> 3, 1);
        columntop = Math.Min(columntop, centery + half - hitstories * 2 * half);
        columntallest = Math.Max(columntallest, hitstories);
    }

    static void QueuePost(short height, int firststory, int stories, TextureAsset lower, int lowerofs, TextureAsset upper, int upperofs,
        short underside = 0)
    {
        if (postcount == MAXPOSTS)
            return;
        postunderside[postcount] = underside;
        postheights[postcount] = height;
        postfirststory[postcount] = (byte)firststory;
        poststories[postcount] = (byte)stories;
        postlower[postcount] = lower;
        postlowerofs[postcount] = lowerofs;
        postupper[postcount] = upper;
        postupperofs[postcount] = upperofs;
        postcount++;
    }

    // Draws the column's queued posts, farthest first
    static void DrawPosts()
    {
        while (postcount > 0)
        {
            postcount--;
            DrawPost(pixx, postheights[postcount] >> 3, postfirststory[postcount], poststories[postcount],
                postlower[postcount], postlowerofs[postcount], postupper[postcount], postupperofs[postcount]);
            if (postunderside[postcount] != 0)
                DrawUnderside(pixx, postheights[postcount] >> 3, postunderside[postcount] >> 3);
        }
    }

    /// <summary>
    /// Fills an arch's underside, one story up, between where the trace enters it (half a story
    /// is entryhalf pixels there) and where it leaves (exithalf). Floors and ceilings are flat
    /// colors, so it's the ceiling's.
    /// </summary>
    static void DrawUnderside(int x, int entryhalf, int exithalf)
    {
        int from = Math.Max(centery - entryhalf, 0);
        int to = Math.Min(centery - exithalf - 1, viewheight - 1);
        int pitch = (int)_videoManager.bufferPitch;

        unsafe
        {
            byte* dest = (byte*)vbufPtr + screenofs + x;
            for (int y = from; y <= to; y++)
                dest[y * pitch] = ceilingcolor;
        }
    }

    /*
    ====================
    =
    = Arches
    =
    = An open floor tile with a height (plane 2) above one story is an arch: a block from the
    = second story up to that height, that can be walked under. The trace opens one as it
    = enters the tile and queues it as it leaves, once it knows how deep the underside runs.
    = Its faces take the texture of a wall beside it.
    =
    ====================
    */

    static bool archopen;
    static short archheight;            // where the trace entered it (short.MaxValue: the view is inside)
    static int archstories;
    static TextureAsset? archtexture;
    static int archofs;

    static bool IsArch(int tilex, int tiley) => _mapManager.tilemap[tilex, tiley] == 0 && _mapManager.storymap[tilex, tiley] > 1;

    /// <summary>
    /// The trace enters the arch at (tilex, tiley), crossing its edge at (edgex, edgey); vertical
    /// says whether that's an edge along x = constant (the face then shows its East texture).
    /// </summary>
    static void OpenArch(int tilex, int tiley, int edgex, int edgey, bool vertical)
    {
        // the wall at either end of the run of arch tiles in line with the face, else one across from it
        int dx = vertical ? 0 : 1, dy = vertical ? 1 : 0;
        archtexture = ArchEndTexture(tilex, tiley, -dx, -dy, vertical) ?? ArchEndTexture(tilex, tiley, dx, dy, vertical)
            ?? WallTexture(tilex - dy, tiley - dx, vertical) ?? WallTexture(tilex + dy, tiley + dx, vertical);
        if (archtexture == null)
            return;                     // nothing to texture it from: leave it open

        archopen = true;
        archstories = _mapManager.storymap[tilex, tiley];
        archheight = HeightAt(edgex, edgey);
        archofs = vertical
            ? LintelColumn(edgey, xtilestep == -1)
            : LintelColumn(edgex, ytilestep == 1);
    }

    // The texture of the wall that ends the run of arch tiles from (tilex, tiley) going (dx, dy)
    static TextureAsset? ArchEndTexture(int tilex, int tiley, int dx, int dy, bool vertical)
    {
        do
        {
            tilex += dx;
            tiley += dy;
            if (tilex < 0 || tiley < 0 || tilex >= _mapManager.mapwidth || tiley >= _mapManager.mapheight)
                return null;
        }
        while (IsArch(tilex, tiley));

        return WallTexture(tilex, tiley, vertical);
    }

    // The view starts under an arch: there's no face, only the underside from above the view
    static void OpenFocalArch()
    {
        archtexture = notexture;
        archopen = true;
        archstories = _mapManager.storymap[focaltx, focalty];
        archheight = short.MaxValue;
        archofs = 0;
    }

    // The trace leaves the open arch, crossing its edge at (edgex, edgey)
    static void CloseArch(int edgex, int edgey)
    {
        if (!archopen)
            return;
        archopen = false;

        short exitheight = HeightAt(edgex, edgey);
        if (exitheight == 0)
            exitheight = 1;             // 0 means no underside
        QueuePost(archheight, 1, archstories, archtexture!, archofs, archtexture!, archofs, exitheight);
    }

    // The height (as wallheight) of a wall at (x, y)
    static short HeightAt(int x, int y)
    {
        int savex = xintercept, savey = yintercept;
        xintercept = x;
        yintercept = y;
        short height = CalcHeight();
        xintercept = savex;
        yintercept = savey;
        return height;
    }

    // Where the trace meets the vertical edge it enters xtile by, and the horizontal one it enters ytile by
    static int VertEdge => (xtilestep == 1 ? xtile : xtile + 1) << MapConstants.TILESHIFT;
    static int HorizEdge => (ytilestep == 1 ? ytile : ytile + 1) << MapConstants.TILESHIFT;

    /// <summary>
    /// Whether the trace should go on past the wall it just hit, on the tile (tilex, tiley): only
    /// while a taller wall somewhere on the level could still show above everything it has hit.
    /// </summary>
    static bool TracePastWall(int tilex, int tiley)
    {
        if (columntallest >= _mapManager.MaxWallStories || columntop <= 0 || postcount >= MAXPOSTS - 1)
            return false;
        // a trace never leaves the map through its edge
        return tilex > 0 && tiley > 0 && tilex < _mapManager.mapwidth - 1 && tiley < _mapManager.mapheight - 1;
    }

    //
    // the trace's position, kept from before it enters a tile, to carry on past a wall it hits there
    //
    static short savedxtile, savedytile;
    static int savedxintercept, savedyintercept, savedxinttile, savedyinttile;
    static ushort savedtexdelta;
    static bool pastwall;               // tracing on past the nearest wall: tiles aren't visible

    static void SaveTrace()
    {
        savedxtile = xtile; savedytile = ytile;
        savedxintercept = xintercept; savedyintercept = yintercept;
        savedxinttile = xinttile; savedyinttile = yinttile;
        savedtexdelta = texdelta;
    }

    static void RestoreTrace()
    {
        xtile = savedxtile; ytile = savedytile;
        xintercept = savedxintercept; yintercept = savedyintercept;
        xinttile = savedxinttile; yinttile = savedyinttile;
        texdelta = savedtexdelta;
        pastwall = true;
    }

    /// <summary>
    /// Draws one screen column of a wall, standing on the floor, stories tall. Stories
    /// firststory and up are drawn: the first story from lower, the ones above it from upper.
    /// A texture covers a story per 64 texels of its height, from the floor up, and repeats
    /// above that. half is half a story's height in pixels; lowerofs and upperofs are the
    /// column to draw, times 64, as the trace finds it across a tile.
    /// </summary>
    static void DrawPost(int x, int half, int firststory, int stories, TextureAsset lower, int lowerofs, TextureAsset upper, int upperofs)
    {
        if (half <= 0) half = 100;

        //
        // row k up from the floor line shows texel k * 32 / half up the wall; step that
        // along as whole texels (t) plus a remainder, so it matches the old ScalePost exactly
        //
        int bottom = centery + half - 1 - firststory * 2 * half;
        int top = centery + half - stories * 2 * half;
        int y = Math.Min(bottom, viewheight - 1);
        if (top < 0) top = 0;
        if (y < top) return;

        long num = (long)(bottom - y) * (TEXTURESIZE / 2);
        long t = num / half + firststory * TEXTURESIZE;
        long rem = num % half;
        int pitch = (int)_videoManager.bufferPitch;

        byte[] lowerdata = lower.RawData, upperdata = upper.RawData;
        int lowerheight = lower.Height, upperheight = upper.Height;
        int lowercolumn = TextureColumn(lower, lowerofs), uppercolumn = TextureColumn(upper, upperofs);

        unsafe
        {
            byte* dest = (byte*)vbufPtr + screenofs + x;
            byte col = 0;
            long colt = -1;             // the t col is for
            for (; y >= top; y--)
            {
                if (t != colt)
                {
                    col = t < TEXTURESIZE
                        ? lowerdata[lowercolumn + lowerheight - 1 - (int)(t % lowerheight)]
                        : upperdata[uppercolumn + upperheight - 1 - (int)(t % upperheight)];
                    colt = t;
                }
                dest[y * pitch] = col;

                rem += TEXTURESIZE / 2;
                while (rem >= half)
                {
                    rem -= half;
                    t++;
                }
            }
        }
    }

    // Where the column the trace found (0 to 63, times 64) starts in the texture's pixels
    static int TextureColumn(TextureAsset texture, int ofs) =>
        (ofs >> TEXTURESHIFT) * texture.Width / TEXTURESIZE * texture.Height;


    /*
    ====================
    =
    = HitVertWall
    =
    = tilehit bit 7 is 0, because it's not a door tile
    = if bit 6 is 1 and the adjacent tile is a door tile, use door side pic
    =
    ====================
    */

    internal static void HitVertWall()
    {
        string wallpic;
        int texture;

        texture = ((yintercept - texdelta) >> FIXED2TEXSHIFT) & TEXTUREMASK;

        if (xtilestep == -1)
        {
            texture = TEXTUREMASK - texture;
            xintercept += (int)MapConstants.TILEGLOBAL;
        }
        postheight = CalcHeight();
        postx = pixx;
        var mapDefs = _mapManager.GetMapData();
        MapTextureTranslation? mapTexture = MapTextureTranslation.None;
        if ((tilehit & BIT_WALL) != 0)
        {
            //
            // check for adjacent doors
            //
            var doortile = _mapManager.tilemap[xtile - xtilestep, yinttile];
            mapDefs?.Walls.TryGetValue((tilehit & ~BIT_WALL), out mapTexture);
            if ((doortile & BIT_DOOR) != 0)
            {
                var door = doorobjlist[doortile & ~BIT_DOOR];
                wallpic = door.xlat.East; // West
                SetUpperPost((mapTexture ?? MapTextureTranslation.None).East, texture);   // the wall above the door frame
            }
            else
            {
                wallpic = (mapTexture ?? MapTextureTranslation.None).East;
            }
        }
        else
        {
            mapDefs?.Walls.TryGetValue(tilehit, out mapTexture);
            wallpic = (mapTexture ?? MapTextureTranslation.None).East;
        }

        var textureAsset = _assetManager.Find<TextureAsset>(wallpic);
        if (textureAsset == null)
        {
            uppersource = null;
            return;
        }
        postsource = textureAsset;
        postofs = texture;
        ScalePost();
    }

    /*
    ====================
    =
    = HitHorizWall
    =
    = tilehit bit 7 is 0, because it's not a door tile
    = if bit 6 is 1 and the adjacent tile is a door tile, use door side pic
    =
    ====================
    */

    internal static void HitHorizWall()
    {
        string wallpic;
        int texture;

        texture = ((xintercept - texdelta) >> FIXED2TEXSHIFT) & TEXTUREMASK;

        if (ytilestep == -1)
            yintercept += (int)MapConstants.TILEGLOBAL;
        else
            texture = TEXTUREMASK - texture;

        postheight = CalcHeight();
        postx = pixx;

        var mapDefs = _mapManager.GetMapData();
        MapTextureTranslation? mapTexture = MapTextureTranslation.None;

        if ((tilehit & BIT_WALL) != 0)
        {
            //
            // check for adjacent doors
            //
            var doortile = _mapManager.tilemap[xinttile, ytile - ytilestep];
            mapDefs?.Walls.TryGetValue((tilehit & ~BIT_WALL), out mapTexture);
            if ((doortile & BIT_DOOR) != 0)
            {
                var door = doorobjlist[doortile & ~BIT_DOOR];
                wallpic = door.xlat.North; // South
                SetUpperPost((mapTexture ?? MapTextureTranslation.None).North, texture);  // the wall above the door frame
            }
            else
            {
                wallpic = (mapTexture ?? MapTextureTranslation.None).North;
            }
        }
        else
        {
            mapDefs?.Walls.TryGetValue(tilehit, out mapTexture);
            wallpic = (mapTexture ?? MapTextureTranslation.None).North;
        }

        var textureAsset = _assetManager.Find<TextureAsset>(wallpic);
        if (textureAsset == null)
        {
            uppersource = null;
            return;
        }

        postsource = textureAsset;
        postofs = texture;
        ScalePost();
    }

    internal static void HitVertDoor()
    {
        string doorpage = "";
        int doornumtile;
        int texture;

        doornumtile = tilehit & ~BIT_DOOR;
        texture = ((yintercept - doorobjlist[doornumtile].position) >> FIXED2TEXSHIFT) & TEXTUREMASK;

        postheight = CalcHeight();
        postx = pixx;

        var door = doorobjlist[doornumtile];
        doorpage = door.xlat.East; // West
        //switch ((doortypes)doorobjlist[doornumtile].locknum)
        //{
        //    case doortypes.dr_normal:
        //        doorpage = "DOOR1_2"; // DOORWALL + 1;
        //        break;

        //    case doortypes.dr_lock1:
        //    case doortypes.dr_lock2:
        //    case doortypes.dr_lock3:
        //    case doortypes.dr_lock4:
        //        doorpage = "DOOR3_2"; // "DOORWALL + 7;
        //        break;

        //    case doortypes.dr_elevator:
        //        doorpage = "DOOR2_2"; //DOORWALL + 5;
        //        break;
        //}

        var doorTextureAsset = _assetManager.Find<TextureAsset>(doorpage);
        if (doorTextureAsset == null)
            return;
        postsource = doorTextureAsset;
        postofs = texture;
        SetLintelPost(door, yintercept, xtilestep == -1);

        ScalePost();
    }

    internal static void HitHorizDoor()
    {
        string doorpage = "";
        int doornumtile;
        int texture;

        doornumtile = tilehit & ~BIT_DOOR;
        texture = ((xintercept - doorobjlist[doornumtile].position) >> FIXED2TEXSHIFT) & TEXTUREMASK;

        postheight = CalcHeight();
        postx = pixx;

        var door = doorobjlist[doornumtile];

        //switch ((doortypes)door.locknum)
        //{
        //    case doortypes.dr_normal:
        //        doorpage = door.xlat.North;// "DOOR1_1"; // DOORWALL
        //        break;

        //    case doortypes.dr_lock1:
        //    case doortypes.dr_lock2:
        //    case doortypes.dr_lock3:
        //    case doortypes.dr_lock4:
        //        doorpage = door.xlat.North;// "DOOR3_1"; // DOORWALL + 6;
        //        break;

        //    case doortypes.dr_elevator:
        //        doorpage = "DOOR2_1";// DOORWALL + 4;
        //        break;
        //    }
        doorpage = door.xlat.North; // South
        var doorTextureAsset = _assetManager.Find<TextureAsset>(doorpage);
        if (doorTextureAsset == null)
            return;
        postsource = doorTextureAsset;
        postofs = texture;
        SetLintelPost(door, xintercept, ytilestep == 1);

        ScalePost();
    }

    /*
    ====================
    =
    = Tall walls
    =
    = Above the first story, a door is capped by a lintel on the door plane, as tall as the
    = door tile and textured like the wall the door is set into. It is there whether the door
    = is shut or not: the trace queues an open door's lintel as it passes through.
    =
    ====================
    */

    // Points the stories above the first at the named wall texture, for the post being drawn
    static void SetUpperPost(string wallpic, int texture)
    {
        if (hitstories == 1)
            return;
        uppersource = _assetManager.Find<TextureAsset>(wallpic);
        upperofs = texture;
    }

    /// <summary>
    /// The texture of the wall a door is set into, from the wall tile on either side of it in
    /// line with the door; null if neither side is a plain wall.
    /// </summary>
    static TextureAsset? LintelTexture(doorobj_t door)
    {
        int dx = door.vertical ? 0 : 1, dy = door.vertical ? 1 : 0;
        return WallTexture(door.tilex - dx, door.tiley - dy, door.vertical)
            ?? WallTexture(door.tilex + dx, door.tiley + dy, door.vertical);
    }

    static TextureAsset? WallTexture(int tilex, int tiley, bool vertical)
    {
        var tile = _mapManager.tilemap[tilex, tiley];
        if ((tile & BIT_DOOR) != 0 || (tile & ~BIT_WALL) == 0)
            return null;                // open floor, a door or a pushwall

        MapTextureTranslation? mapTexture = null;
        if (_mapManager.GetMapData()?.Walls.TryGetValue(tile & ~BIT_WALL, out mapTexture) != true || mapTexture == null)
            return null;
        return _assetManager.Find<TextureAsset>(vertical ? mapTexture.East : mapTexture.North);
    }

    // The lintel column where the trace meets the door plane at along (y for a vertical door, x
    // for a horizontal one); flip mirrors it like the wall faces seen from the same side
    static int LintelColumn(int along, bool flip)
    {
        int texture = (along >> FIXED2TEXSHIFT) & TEXTUREMASK;
        return flip ? TEXTUREMASK - texture : texture;
    }

    // Caps the door post being drawn with its lintel (the door texture again if it has no wall)
    static void SetLintelPost(doorobj_t door, int along, bool flip)
    {
        if (hitstories == 1)
            return;
        uppersource = LintelTexture(door) ?? postsource;
        upperofs = LintelColumn(along, flip);
    }

    /// <summary>
    /// Notes the lintel of the door the trace is passing through, meeting its plane at
    /// (planex, planey), in the column's queue. Nothing to do for a one story door.
    /// </summary>
    static void NoteLintel(doorobj_t door, int planex, int planey)
    {
        int stories = _mapManager.WallStories(door.tilex, door.tiley);
        if (stories == 1)
            return;

        var texture = LintelTexture(door);
        if (texture == null)
            return;                     // set into nothing: leave the view above it open

        short height = HeightAt(planex, planey);
        int ofs = door.vertical
            ? LintelColumn(planey, xtilestep == -1)
            : LintelColumn(planex, ytilestep == 1);
        QueuePost(height, 1, stories, texture, ofs, texture, ofs);
    }

    /// <summary>
    /// When the view starts inside a door tile, the trace never enters it, so check whether this
    /// column meets the door plane ahead of the view point.
    /// </summary>
    static void NoteFocalLintel(int xstep, int ystep)
    {
        var door = doorobjlist[_mapManager.tilemap[focaltx, focalty] & ~BIT_DOOR];
        const int HALFTILE = (int)MapConstants.TILEGLOBAL / 2;

        if (door.vertical)
        {
            int planex = (focaltx << MapConstants.TILESHIFT) + HALFTILE;
            int dist = (planex - viewx) * xtilestep;
            if (dist <= 0)
                return;                 // the plane is behind the view
            int planey = viewy + MathUtils.FixedMul(ystep, dist);
            if (planey >> MapConstants.TILESHIFT == focalty)
                NoteLintel(door, planex, planey);
        }
        else
        {
            int planey = (focalty << MapConstants.TILESHIFT) + HALFTILE;
            int dist = (planey - viewy) * ytilestep;
            if (dist <= 0)
                return;
            int planex = viewx + MathUtils.FixedMul(xstep, dist);
            if (planex >> MapConstants.TILESHIFT == focaltx)
                NoteLintel(door, planex, planey);
        }
    }

    /*
    ====================
    =
    = Diagonal walls
    =
    = A diagonal tile (MapManager.wallshape) is solid on the two edges at its named corner, with
    = a 45 degree face between the other two corners. A trace that enters through a solid edge
    = hits it like a square wall. One that enters through an open edge either meets the face or
    = crosses the open half and carries on to the next tile.
    =
    ====================
    */

    static int diaghitu;    // where the last TraceDiagonal hit, as the tile-local x (0..TILEGLOBAL)

    // Whether the tile edge a trace enters by is one of the diagonal's two solid edges
    private static bool IsSolidEdge(WallShape shape, controldirs edge) => edge switch
    {
        controldirs.di_west => shape is WallShape.SolidNW or WallShape.SolidSW,
        controldirs.di_east => shape is WallShape.SolidNE or WallShape.SolidSE,
        controldirs.di_north => shape is WallShape.SolidNW or WallShape.SolidNE,
        _ => shape is WallShape.SolidSW or WallShape.SolidSE,
    };

    /// <summary>
    /// Where a trace from tile-local (eu, ev), heading (du, dv), meets the diagonal face of the
    /// tile at (tilex, tiley). All in 16.16 fixed point. On a hit, sets xintercept/yintercept and
    /// diaghitu; false means it misses the face within the tile (it crosses the open half).
    /// </summary>
    private static bool TraceDiagonal(WallShape shape, int tilex, int tiley, long eu, long ev, long du, long dv)
    {
        const long TILE = MapConstants.TILEGLOBAL;
        long num, den;

        if (shape is WallShape.SolidNE or WallShape.SolidSW)
        {
            num = ev - eu;              // face runs NW to SE: u == v
            den = du - dv;
        }
        else
        {
            num = TILE - eu - ev;       // face runs NE to SW: u + v == TILE
            den = du + dv;
        }

        if (den == 0)
            return false;               // parallel to the face
        if (num != 0 && (num < 0) != (den < 0))
            return false;               // the face is behind the trace

        long hu = eu + num * du / den;
        long hv = ev + num * dv / den;
        if (hu < 0 || hu > TILE || hv < 0 || hv > TILE)
            return false;

        xintercept = (tilex << MapConstants.TILESHIFT) + (int)hu;
        yintercept = (tiley << MapConstants.TILESHIFT) + (int)hv;
        diaghitu = (int)hu;
        return true;
    }

    internal static void HitDiagWall(WallShape shape, int tilex, int tiley)
    {
        //
        // seen from the open side, the face runs left to right along +u for SolidNW/SolidNE,
        // and along -u for SolidSW/SolidSE
        //
        int along = shape is WallShape.SolidSW or WallShape.SolidSE
            ? (int)MapConstants.TILEGLOBAL - 1 - diaghitu
            : diaghitu;
        along = Math.Clamp(along, 0, (int)MapConstants.TILEGLOBAL - 1);
        int texture = (along >> FIXED2TEXSHIFT) & TEXTUREMASK;

        postheight = CalcHeight();
        postx = pixx;

        var wallpic = _mapManager.GetDiagonal(tilex, tiley)?.Texture;
        if (string.IsNullOrEmpty(wallpic))
        {
            MapTextureTranslation? mapTexture = MapTextureTranslation.None;
            _mapManager.GetMapData()?.Walls.TryGetValue(tilehit & ~BIT_WALL, out mapTexture);
            wallpic = (mapTexture ?? MapTextureTranslation.None).North;
        }

        var textureAsset = _assetManager.Find<TextureAsset>(wallpic);
        if (textureAsset == null)
            return;
        postsource = textureAsset;
        postofs = texture;
        ScalePost();
    }

    internal static byte[] vgaCeiling =
    {
        0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0xbf,
        0x4e,0x4e,0x4e,0x1d,0x8d,0x4e,0x1d,0x2d,0x1d,0x8d,
        0x1d,0x1d,0x1d,0x1d,0x1d,0x2d,0xdd,0x1d,0x1d,0x98,

        0x1d,0x9d,0x2d,0xdd,0xdd,0x9d,0x2d,0x4d,0x1d,0xdd,
        0x7d,0x1d,0x2d,0x2d,0xdd,0xd7,0x1d,0x1d,0x1d,0x2d,
        0x1d,0x1d,0x1d,0x1d,0xdd,0xdd,0x7d,0xdd,0xdd,0xdd
    };

    static byte ceilingcolor;           // this frame's ceiling color, for arch undersides too

    /*
    ====================
    =
    = Sky
    =
    = A level can have a sky: a picture drawn above the horizon in place of the ceiling color.
    = It's stretched down to the horizon, and repeats around the full circle every SKYCIRCLE
    = pixels, turning with the view but not moving as the player walks.
    =
    ====================
    */

    const int SKYCIRCLE = 1024;         // sky pixels all the way round: a 256 wide picture shows 4 times

    internal static string? levelsky;   // the level's sky graphic (or wall texture), null for none
    static string? skyname;             // the one skycolumns holds
    static byte[] skycolumns = [];      // its pixels, column by column
    static int skywidth, skyheight;

    /// <summary>Makes skycolumns hold the level's sky; false if it has none, or it can't be found.</summary>
    static bool LoadSky()
    {
        if (string.IsNullOrEmpty(levelsky))
            return false;
        if (levelsky == skyname)
            return skycolumns.Length > 0;

        skyname = levelsky;
        skycolumns = [];

        if (_assetManager.Find<GraphicAsset>(levelsky) is { Width: > 0, Height: > 0 } pic)
        {
            // pictures are stored row by row
            skywidth = pic.Width;
            skyheight = pic.Height;
            skycolumns = new byte[skywidth * skyheight];
            for (int x = 0; x < skywidth; x++)
                for (int y = 0; y < skyheight; y++)
                    skycolumns[x * skyheight + y] = pic.RawData[y * skywidth + x];
        }
        else if (_assetManager.Find<TextureAsset>(levelsky) is { } texture && texture.RawData.Length >= texture.Width * texture.Height)
        {
            // wall textures already are column by column
            skywidth = texture.Width;
            skyheight = texture.Height;
            skycolumns = texture.RawData;
        }

        return skycolumns.Length > 0;
    }

    static void DrawSky()
    {
        int pitch = (int)_videoManager.bufferPitch;

        unsafe
        {
            byte* dest = (byte*)vbufPtr + screenofs;
            for (int x = 0; x < viewwidth; x++)
            {
                int angle = (midangle + pixelangle[x]) % ANG360;
                if (angle < 0)
                    angle += ANG360;

                // angles grow to the left, and the picture runs left to right
                int u = (int)((long)(ANG360 - 1 - angle) * SKYCIRCLE / ANG360 % skywidth);
                int column = u * skyheight;

                for (int y = 0; y < centery; y++)
                    dest[y * pitch + x] = skycolumns[column + y * skyheight / centery];
            }
        }
    }

    internal static void VGAClearScreen()
    {
        var gameInfo = _gameEngineManager.GetGameInfo();
        var mapInfo = gameInfo.Maps[gamestate.mapon];

        string ceiling = mapInfo.CeilingColor ?? gameInfo.DefaultMap.CeilingColor;
        byte ceilingColor = _videoManager.ParseColor(ceiling);
        ceilingcolor = ceilingColor;
        string floor = mapInfo.FloorColor ?? gameInfo.DefaultMap.FloorColor;
        byte floorColor = _videoManager.ParseColor(floor);

        var destIndex = vbuf;
        int y;
        unsafe
        {
            byte* dest = (byte*)vbufPtr;// + destIndex;
            if (LoadSky())
            {
                DrawSky();
                y = centery;
                destIndex += centery * (int)_videoManager.bufferPitch;
            }
            else
            {
                for (y = 0; y < centery; y++, destIndex += (int)_videoManager.bufferPitch)
                    for(var v = 0; v < viewwidth; v++)
                        dest[destIndex+v] = ceilingColor;
                        //Array.Fill(dest, ceiling, destIndex, viewwidth);
            }

            for (; y < viewheight; y++, destIndex += (int)_videoManager.bufferPitch)
                for (var v = 0; v < viewwidth; v++)
                    dest[destIndex + v] = floorColor;
                    //Array.Fill(dest, 0x19, destIndex, viewwidth);
        }
    }


    private static void WallRefresh()
    {
        short angle;
        int xstep = 0, ystep = 0;
        int xinttemp = 0, yinttemp = 0;                            // holds temporary intercept position
        uint xpartial = 0, ypartial = 0;
        int pwallposnorm=0, pwallposinv=0, pwallposi=0;           // holds modified pwallpos

        for (pixx = 0; pixx < viewwidth; pixx++)
        {
            angle = (short)(midangle + pixelangle[pixx]);

            if (angle < 0) // -90 - -1 degree arc
                angle += ANG360; // -90 is the same as 270
            if (angle >= ANG360) // 360-449 degree arc
                angle -= ANG360; // -449 is the same as 89

            //
            // setup xstep/ystep based on angle
            //
            if (angle < ANG90) // 0-89 degree arc
            {
                xtilestep = 1;
                ytilestep = -1;
                xstep = finetangent[ANG90 - 1 - angle];
                ystep = -finetangent[angle];
                xpartial = (uint)xpartialup;
                ypartial = (uint)ypartialdown;
            }
            else if (angle < ANG180) // 90-179 degree arc
            {
                xtilestep = -1;
                ytilestep = -1;
                xstep = -finetangent[angle - ANG90];
                ystep = -finetangent[ANG180 - 1 - angle];
                xpartial = (uint)xpartialdown;
                ypartial = (uint)ypartialdown;
            }
            else if (angle < ANG270) // 180-269 degree arc
            {
                xtilestep = -1;
                ytilestep = 1;
                xstep = -finetangent[ANG270 - 1 - angle];
                ystep = finetangent[angle - ANG180];
                xpartial = (uint)xpartialdown;
                ypartial = (uint)ypartialup;
            }
            else if (angle < ANG360) // 270-359 degree arc
            {
                xtilestep = 1;
                ytilestep = 1;
                xstep = finetangent[angle - ANG270];
                ystep = finetangent[ANG360 - 1 - angle];
                xpartial = (uint)xpartialup;
                ypartial = (uint)ypartialup;
            }

            //
            // initialise variables for intersection testing
            //
            yintercept = MathUtils.FixedMul(ystep, (int)xpartial) + viewy;
            yinttile = yintercept >> (int)MapConstants.TILESHIFT;
            xtile = (short)(focaltx + xtilestep);

            xintercept = MathUtils.FixedMul(xstep, (int)ypartial) + viewx;
            xinttile = xintercept >> (int)MapConstants.TILESHIFT;
            ytile = (short)(focalty + ytilestep);

            texdelta = 0;
            columnhit = pastwall = archopen = false;
            columntallest = 0;
            columntop = viewheight;
            bool focalhit = false;              // hit a wall in the view's own tile: no trace

            //
            // special treatment when player is in back tile of pushwall
            //
            if (_mapManager.tilemap[focaltx, focalty] == BIT_WALL)
            {
                hitstories = _mapManager.WallStories(pwallx, pwally);
                if ((pwalldir == controldirs.di_east && xtilestep == 1) || (pwalldir == controldirs.di_west && xtilestep == -1))
                {
                    yinttemp = yintercept - ((ystep * (64 - pwallpos)) >> 6);

                    //
                    //  trace hit vertical pushwall back?
                    //
                    if (yinttemp >> MapConstants.TILESHIFT == focalty)
                    {
                        if (pwalldir == controldirs.di_east)
                            xintercept = (focaltx << MapConstants.TILESHIFT) + (pwallpos << 10);
                        else
                            xintercept = (int)(((focaltx << MapConstants.TILESHIFT) - MapConstants.TILEGLOBAL) + ((64 - pwallpos) << 10));

                        yintercept = yinttemp;
                        yinttile = yintercept >> MapConstants.TILESHIFT;
                        tilehit = pwalltile;
                        HitVertWall();
                        focalhit = true;
                    }
                }
                else if ((pwalldir == controldirs.di_south && ytilestep == 1) || (pwalldir == controldirs.di_north && ytilestep == -1))
                {
                    xinttemp = xintercept - ((xstep * (64 - pwallpos)) >> 6);

                    //
                    // trace hit horizontal pushwall back?
                    //
                    if (xinttemp >> MapConstants.TILESHIFT == focaltx)
                    {
                        if (pwalldir == controldirs.di_south)
                            yintercept = (focalty << MapConstants.TILESHIFT) + (pwallpos << 10);
                        else
                            yintercept = (int)(((focalty << MapConstants.TILESHIFT) - MapConstants.TILEGLOBAL) + ((64 - pwallpos) << 10));

                        xintercept = xinttemp;
                        xinttile = xintercept >> MapConstants.TILESHIFT;
                        tilehit = pwalltile;
                        HitHorizWall();
                        focalhit = true;
                    }
                }
            }
            else if (_mapManager.wallshape[focaltx, focalty] is var focalshape and not WallShape.Square)
            {
                //
                // the view starts inside a diagonal tile (noclip), so the trace never enters it:
                // check its face from the view point
                //
                if (TraceDiagonal(focalshape, focaltx, focalty,
                    viewx & (MapConstants.TILEGLOBAL - 1), viewy & (MapConstants.TILEGLOBAL - 1),
                    xtilestep * MapConstants.TILEGLOBAL, ystep))
                {
                    tilehit = _mapManager.tilemap[focaltx, focalty];
                    hitstories = _mapManager.WallStories(focaltx, focalty);
                    HitDiagWall(focalshape, focaltx, focalty);
                    _mapManager.seen[focaltx, focalty] |= SeenFlags.DiagonalFace;
                    focalhit = true;
                }
            }
            else if ((_mapManager.tilemap[focaltx, focalty] & BIT_DOOR) != 0)
            {
                NoteFocalLintel(xstep, ystep);
            }
            else if (IsArch(focaltx, focalty))
            {
                OpenFocalArch();
            }

            //
            // trace along this angle until we hit a wall, and on past it while a taller wall
            // could still show over the top: a wall that is hit and passed is traced through
            // from where the trace entered its tile, as if it were open floor
            //
            // CORE LOOP!
            //
            while (!focalhit)
            {
                //
                // check intersections with vertical walls
                //
                if ((xtile - xtilestep) == xinttile && (ytile - ytilestep) == yinttile)
                    yinttile = ytile;

                if ((ytilestep == -1 && yinttile <= ytile) || (ytilestep == 1 && yinttile >= ytile))
                {
                    SaveTrace();
                    if (horizentry(xstep, ystep, xinttemp, ref pwallposnorm, ref pwallposinv, ref pwallposi))
                    {
                        if (!TracePastWall(savedxinttile, savedytile))
                            break;
                        RestoreTrace();
                        passhoriz(xstep);
                    }
                }

                //
                // check intersections with horizontal walls
                //
                if ((xtile - xtilestep) == xinttile && (ytile - ytilestep) == yinttile)
                    xinttile = xtile;

                if ((xtilestep == -1 && xinttile <= xtile) || (xtilestep == 1 && xinttile >= xtile))
                {
                    SaveTrace();
                    if (vertentry(ystep, xstep, yinttemp, ref pwallposnorm, ref pwallposinv, ref pwallposi))
                    {
                        if (!TracePastWall(savedxtile, savedyinttile))
                            break;
                        RestoreTrace();
                        passvert(ystep);
                    }
                }
            }

            DrawPosts();
        }
    }

    private static bool vertentry(int ystep, int xstep,int yinttemp, ref int pwallposnorm, ref int pwallposinv, ref int pwallposi)
    {
        // the pushwall cases below move yinttile, so keep the tile the trace entered for the automap
        int hitx = xtile, hity = yinttile;
        CloseArch(VertEdge, yintercept);
        tilehit = _mapManager.tilemap[xtile, yinttile];
        hitstories = tilehit == BIT_WALL ? _mapManager.WallStories(pwallx, pwally) : _mapManager.WallStories(hitx, hity);

        if (tilehit != 0)
        {
            if ((tilehit & BIT_DOOR) != 0)
            {
                //
                // hit a vertical door, so find which coordinate the door would be
                // intersected at, and check to see if the door is open past that point
                //
                var door = doorobjlist[tilehit & ~BIT_DOOR];
                yinttemp = yintercept + (ystep >> 1);    // add halfstep to current intercept position
                int planex = (int)((xtile << (int)MapConstants.TILESHIFT) + (MapConstants.TILEGLOBAL / 2));

                if (door.action == dooractiontypes.dr_open)
                {
                    if (yinttemp >> MapConstants.TILESHIFT == yinttile)
                        NoteLintel(door, planex, yinttemp);
                    passvert(ystep); // door is open, continue tracing
                    return false;
                }

                //
                // midpoint is outside tile, so it hit the side of the wall before a door
                //
                if (yinttemp >> MapConstants.TILESHIFT != yinttile)
                {
                    passvert(ystep);
                    return false;
                }

                if (door.action != dooractiontypes.dr_closed)
                {
                    //
                    // the trace hit the door plane at pixel position yintercept, see if the door is
                    // closed that much
                    //
                    if ((ushort)yinttemp < door.position)
                    {
                        NoteLintel(door, planex, yinttemp);
                        passvert(ystep);
                        return false;
                    }
                }

                yintercept = yinttemp;
                xintercept = planex;

                HitVertDoor();
            }
            else if (tilehit == BIT_WALL)
            {
                //
                // hit a sliding vertical wall
                //
                if (pwalldir == controldirs.di_west || pwalldir == controldirs.di_east)
                {
                    if (pwalldir == controldirs.di_west)
                    {
                        pwallposnorm = 64 - pwallpos;
                        pwallposinv = pwallpos;
                    }
                    else
                    {
                        pwallposnorm = pwallpos;
                        pwallposinv = 64 - pwallpos;
                    }

                    if ((pwalldir == controldirs.di_east && xtile == pwallx && yinttile == pwally)
                     || (pwalldir == controldirs.di_west && !(xtile == pwallx && yinttile == pwally)))
                    {
                        yinttemp = yintercept + ((ystep * pwallposnorm) >> 6);

                        if (yinttemp >> MapConstants.TILESHIFT != yinttile)
                        {
                            passvert(ystep);
                            return false;
                        }

                        yintercept = yinttemp;
                        xintercept = (int)(((xtile << MapConstants.TILESHIFT) + MapConstants.TILEGLOBAL) - (pwallposinv << 10));
                        yinttile = yintercept >> MapConstants.TILESHIFT;
                        tilehit = pwalltile;

                        HitVertWall();
                    }
                    else
                    {
                        yinttemp = yintercept + ((ystep * pwallposinv) >> 6);

                        if (yinttemp >> MapConstants.TILESHIFT != yinttile)
                        {
                            passvert(ystep);
                            return false;
                        }

                        yintercept = yinttemp;
                        xintercept = (xtile << MapConstants.TILESHIFT) -(pwallposinv << 10);
                        yinttile = yintercept >> MapConstants.TILESHIFT;
                        tilehit = pwalltile;

                        HitVertWall();
                    }
                }
                else
                {
                    if (pwalldir == controldirs.di_north)
                        pwallposi = 64 - pwallpos;
                    else
                        pwallposi = pwallpos;

                    if ((pwalldir == controldirs.di_south && (ushort)yintercept < (pwallposi << 10))
                     || (pwalldir == controldirs.di_north && (ushort)yintercept > (pwallposi << 10)))
                    {
                        if (xtile == pwallx && yinttile == pwally)
                        {
                            if ((pwalldir == controldirs.di_south && (int)((ushort)yintercept) + ystep < (pwallposi << 10))
                             || (pwalldir == controldirs.di_north && (int)((ushort)yintercept) + ystep > (pwallposi << 10)))
                            {
                                //goto passvert;
                                passvert(ystep);
                                return false;
                            }

                            //
                            // set up a horizontal intercept position
                            //
                            if (pwalldir == controldirs.di_south)
                                yintercept = (yinttile << MapConstants.TILESHIFT) + (pwallposi << 10);
                            else
                                yintercept = (int)(((yinttile << MapConstants.TILESHIFT) - MapConstants.TILEGLOBAL) + (pwallposi << 10));

                            xintercept -= (xstep * (64 - pwallpos)) >> 6;
                            xinttile = xintercept >> MapConstants.TILESHIFT;
                            tilehit = pwalltile;

                            HitHorizWall();
                        }
                        else
                        {
                            texdelta = (ushort)(pwallposi << 10);
                            xintercept = xtile << MapConstants.TILESHIFT;
                            tilehit = pwalltile;

                            HitVertWall();
                        }
                    }
                    else
                    {
                        if (xtile == pwallx && yinttile == pwally)
                        {
                            texdelta = (ushort)(pwallposi << 10);
                            xintercept = xtile << MapConstants.TILESHIFT;
                            tilehit = pwalltile;

                            HitVertWall();
                        }
                        else
                        {
                            if ((pwalldir == controldirs.di_south && ((ushort)yintercept) + ystep > (pwallposi << 10))
                             || (pwalldir == controldirs.di_north && ((ushort)yintercept) + ystep < (pwallposi << 10)))
                            {
                                //goto passvert;
                                passvert(ystep);
                                return false;
                            }

                            //
                            // set up a horizontal intercept position
                            //
                            if (pwalldir == controldirs.di_south)
                                yintercept = (yinttile << MapConstants.TILESHIFT) - ((64 - pwallpos) << 10);
                            else
                                yintercept = (yinttile << MapConstants.TILESHIFT) + ((64 - pwallpos) << 10);

                            xintercept -= (xstep * pwallpos) >> 6;
                            xinttile = xintercept >> MapConstants.TILESHIFT;
                            tilehit = pwalltile;

                            HitHorizWall();
                        }
                    }
                }
            }
            else
            {
                var shape = _mapManager.wallshape[xtile, yinttile];
                if (shape != WallShape.Square
                    && !IsSolidEdge(shape, xtilestep == 1 ? controldirs.di_west : controldirs.di_east))
                {
                    //
                    // entered a diagonal by an open edge: hit its face, or cross the open half
                    //
                    long eu = xtilestep == 1 ? 0 : MapConstants.TILEGLOBAL;
                    long ev = yintercept - (yinttile << MapConstants.TILESHIFT);
                    if (!TraceDiagonal(shape, xtile, yinttile, eu, ev, xtilestep * MapConstants.TILEGLOBAL, ystep))
                    {
                        passvert(ystep);
                        return false;
                    }

                    HitDiagWall(shape, xtile, yinttile);
                    _mapManager.seen[hitx, hity] |= SeenFlags.DiagonalFace;
                    return true;
                }
                else
                {
                    xintercept = (xtile << MapConstants.TILESHIFT);

                    HitVertWall();
                }
            }

            _mapManager.seen[hitx, hity] |= xtilestep == 1 ? SeenFlags.WestFace : SeenFlags.EastFace;
            return true;
        }

        //
        // mark the tile as visible and setup for next step
        //
        if (IsArch(xtile, yinttile))
            OpenArch(xtile, yinttile, VertEdge, yintercept, true);
        passvert(ystep);
        return false;
    }

    private static void passvert(int ystep)
    {
        if (!pastwall)
            _mapManager.spotvis[xtile, yinttile] = true;
        xtile += xtilestep;
        yintercept += ystep;
        yinttile = yintercept >> (int)MapConstants.TILESHIFT;
    }

    private static bool horizentry(int xstep,int ystep, int xinttemp, ref int pwallposnorm, ref int pwallposinv, ref int pwallposi)
    {
        // the pushwall cases below move xinttile, so keep the tile the trace entered for the automap
        int hitx = xinttile, hity = ytile;
        CloseArch(xintercept, HorizEdge);
        tilehit = _mapManager.tilemap[xinttile, ytile];
        hitstories = tilehit == BIT_WALL ? _mapManager.WallStories(pwallx, pwally) : _mapManager.WallStories(hitx, hity);

        if (tilehit != 0)
        {
            if ((tilehit & BIT_DOOR) != 0)
            {
                //
                // hit a horizontal door, so find which coordinate the door would be
                // intersected at, and check to see if the door is open past that point
                //
                var door = doorobjlist[tilehit & ~BIT_DOOR];
                xinttemp = xintercept + (xstep >> 1);    // add half step to current intercept position
                int planey = (int)((ytile << (int)MapConstants.TILESHIFT) + (MapConstants.TILEGLOBAL / 2));

                if (door.action == (byte)dooractiontypes.dr_open)
                {
                    if ((xinttemp >> MapConstants.TILESHIFT) == xinttile)
                        NoteLintel(door, xinttemp, planey);
                    passhoriz(xstep); // door is open, continue tracing
                    return false;
                }

                //
                // midpoint is outside tile, so it hit the side of the wall before a door
                //
                if ((xinttemp >> MapConstants.TILESHIFT) != xinttile)
                {
                    passhoriz(xstep);
                    return false;
                }

                if (door.action != dooractiontypes.dr_closed)
                {
                    //
                    // the trace hit the door plane at pixel position xintercept, see if the door is
                    // closed that much
                    //
                    if ((ushort)xinttemp < door.position)
                    {
                        NoteLintel(door, xinttemp, planey);
                        passhoriz(xstep);
                        return false;
                    }
                }

                xintercept = xinttemp;
                yintercept = planey;

                HitHorizDoor();
            }
            else if (tilehit == BIT_WALL)
            {
                //
                // hit a sliding horizontal wall
                //
                if (pwalldir == controldirs.di_north || pwalldir == controldirs.di_south)
                {
                    if (pwalldir == controldirs.di_north)
                    {
                        pwallposnorm = 64 - pwallpos;
                        pwallposinv = pwallpos;
                    }
                    else
                    {
                        pwallposnorm = pwallpos;
                        pwallposinv = 64 - pwallpos;
                    }

                    if ((pwalldir == controldirs.di_south && xinttile == pwallx && ytile == pwally)
                     || (pwalldir == controldirs.di_north && !(xinttile == pwallx && ytile == pwally)))
                    {
                        xinttemp = xintercept + ((xstep * pwallposnorm) >> 6);

                        if (xinttemp >> MapConstants.TILESHIFT != xinttile)
                        {
                            passhoriz(xstep);
                            return false;
                        }

                        xintercept = xinttemp;
                        yintercept = (int)(((ytile << MapConstants.TILESHIFT) +MapConstants.TILEGLOBAL) -(pwallposinv << 10));
                        xinttile = xintercept >> MapConstants.TILESHIFT;
                        tilehit = pwalltile;

                        HitHorizWall();
                    }
                    else
                    {
                        xinttemp = xintercept + ((xstep * pwallposinv) >> 6);

                        if (xinttemp >> MapConstants.TILESHIFT != xinttile)
                        {
                            passhoriz(xstep);
                            return false;
                        }

                        xintercept = xinttemp;
                        yintercept = (ytile << MapConstants.TILESHIFT) -(pwallposinv << 10);
                        xinttile = xintercept >> MapConstants.TILESHIFT;
                        tilehit = pwalltile;

                        HitHorizWall();
                    }
                }
                else
                {
                    if (pwalldir == controldirs.di_west)
                        pwallposi = 64 - pwallpos;
                    else
                        pwallposi = pwallpos;

                    if ((pwalldir == controldirs.di_east && (ushort)xintercept < (pwallposi << 10))
                     || (pwalldir == controldirs.di_west && (ushort)xintercept > (pwallposi << 10)))
                    {
                        if (xinttile == pwallx && ytile == pwally)
                        {
                            if ((pwalldir == controldirs.di_east && (xintercept) + xstep < (pwallposi << 10))
                             || (pwalldir == controldirs.di_west && (xintercept) + xstep > (pwallposi << 10)))

                            {
                                passhoriz(xstep);
                                return false;
                            }

                            //
                            // set up a vertical intercept position
                            //
                            yintercept -= (ystep * (64 - pwallpos)) >> 6;
                            yinttile = yintercept >> MapConstants.TILESHIFT;

                            if (pwalldir == controldirs.di_east)
                                xintercept = (xinttile << MapConstants.TILESHIFT) + (pwallposi << 10);
                            else
                                xintercept = (int)(((xinttile << MapConstants.TILESHIFT) - MapConstants.TILEGLOBAL) + (pwallposi << 10));

                            tilehit = pwalltile;

                            HitVertWall();
                        }
                        else
                        {
                            texdelta = (ushort)(pwallposi << 10);
                            yintercept = ytile << MapConstants.TILESHIFT;
                            tilehit = pwalltile;

                            HitHorizWall();
                        }
                    }
                    else
                    {
                        if (xinttile == pwallx && ytile == pwally)
                        {
                            texdelta = (ushort)(pwallposi << 10);
                            yintercept = ytile << MapConstants.TILESHIFT;
                            tilehit = pwalltile;

                            HitHorizWall();
                        }
                        else
                        {
                            if ((pwalldir == controldirs.di_east && (xintercept) + xstep > (pwallposi << 10))
                             || (pwalldir == controldirs.di_west && (xintercept) + xstep < (pwallposi << 10)))

                            {
                                passhoriz(xstep);
                                return false;
                            }

                            //
                            // set up a vertical intercept position
                            //
                            yintercept -= (ystep * pwallpos) >> 6;
                            yinttile = yintercept >> MapConstants.TILESHIFT;

                            if (pwalldir == controldirs.di_east)
                                xintercept = (xinttile << MapConstants.TILESHIFT) - ((64 - pwallpos) << 10);
                            else
                                xintercept = (xinttile << MapConstants.TILESHIFT) + ((64 - pwallpos) << 10);

                            tilehit = pwalltile;

                            HitVertWall();
                        }
                    }
                }
            }
            else
            {
                var shape = _mapManager.wallshape[xinttile, ytile];
                if (shape != WallShape.Square
                    && !IsSolidEdge(shape, ytilestep == 1 ? controldirs.di_north : controldirs.di_south))
                {
                    //
                    // entered a diagonal by an open edge: hit its face, or cross the open half
                    //
                    long eu = xintercept - (xinttile << MapConstants.TILESHIFT);
                    long ev = ytilestep == 1 ? 0 : MapConstants.TILEGLOBAL;
                    if (!TraceDiagonal(shape, xinttile, ytile, eu, ev, xstep, ytilestep * MapConstants.TILEGLOBAL))
                    {
                        passhoriz(xstep);
                        return false;
                    }

                    HitDiagWall(shape, xinttile, ytile);
                    _mapManager.seen[hitx, hity] |= SeenFlags.DiagonalFace;
                    return true;
                }
                else
                {
                    yintercept = ytile << MapConstants.TILESHIFT;

                    HitHorizWall();
                }
            }

            _mapManager.seen[hitx, hity] |= ytilestep == 1 ? SeenFlags.NorthFace : SeenFlags.SouthFace;
            return true;
        }

        //
        // mark the tile as visible and setup for next step
        //
        if (IsArch(xinttile, ytile))
            OpenArch(xinttile, ytile, xintercept, HorizEdge, false);
        passhoriz(xstep);
        return false;
    }

    private static void passhoriz(int xstep)
    {
        if (!pastwall)
            _mapManager.spotvis[xinttile, ytile] = true;
        ytile += ytilestep;
        xintercept += xstep;
        xinttile = xintercept >> (int)MapConstants.TILESHIFT;
    }
    /*
========================
=
= TransformTile
=
= Takes paramaters:
=   tx,ty               : tile the object is centered in
=
= globals:
=   viewx,viewy         : point of view
=   viewcos,viewsin     : sin/cos of viewangle
=   scale               : conversion from global value to screen value
=
= sets:
=   screenx,transx,transy,screenheight: projected edge location and size
=
= Returns true if the tile is withing getting distance
=
========================
*/

    internal static bool TransformTile(int tx, int ty, ref short dispx, ref short dispheight)
    {
        int gx, gy, gxt, gyt, nx, ny;

        //
        // translate point to view centered coordinates
        //
        gx = ((int)tx << MapConstants.TILESHIFT) + 0x8000 - viewx;
        gy = ((int)ty << MapConstants.TILESHIFT) + 0x8000 - viewy;

        //
        // calculate newx
        //
        gxt = MathUtils.FixedMul(gx, viewcos);
        gyt = MathUtils.FixedMul(gy, viewsin);
        nx = gxt - gyt - 0x2000;            // 0x2000 is size of object

        //
        // calculate newy
        //
        gxt = MathUtils.FixedMul(gx, viewsin);
        gyt = MathUtils.FixedMul(gy, viewcos);
        ny = gyt + gxt;


        //
        // calculate height / perspective ratio
        //
        if (nx < MINDIST)                 // too close, don't overflow the divide
            dispheight = 0;
        else
        {
            dispx = (short)(centerx + ny * scale / nx);
            dispheight = (short)(heightnumerator / (nx >> 8));
        }

        //
        // see if it should be grabbed
        //
        if (nx < MapConstants.TILEGLOBAL && ny > -MapConstants.TILEGLOBAL / 2 && ny < MapConstants.TILEGLOBAL / 2)
            return true;
        else
            return false;
    }

    // Computes an actor's screen-space hit-testing/scale data (ViewX/TransX/ViewHeight) from
    // the fixed-point X/Y that Program.EnemyAI.cs's movement code maintains, so enemies,
    // projectiles and the BJ-victory actor can be both rendered and (enemies) shot at.
    internal static void TransformActor(Entities.Actors.Actor ob)
    {
        var gx = ob.X - viewx;
        var gy = ob.Y - viewy;

        var gxt = MathUtils.FixedMul(gx, viewcos);
        var gyt = MathUtils.FixedMul(gy, viewsin);
        var nx = gxt - gyt - ACTORSIZE;

        gxt = MathUtils.FixedMul(gx, viewsin);
        gyt = MathUtils.FixedMul(gy, viewcos);
        var ny = gyt + gxt;

        ob.TransX = nx;

        if (nx < MINDIST)
        {
            ob.ViewHeight = 0;
            return;
        }

        ob.ViewX = (short)(centerx + ny * scale / nx);
        ob.ViewHeight = (ushort)(heightnumerator / (nx >> 8));
    }

    private static readonly Dictionary<string, bool> _directionalSpriteCache = [];

    // There's no per-frame "rotate" flag in the actordefs schema -- the engine infers it
    // from how many sprite variants actually exist. Walk-cycle sprites (e.g. GARDB1..B8)
    // have 8-directional rotation frames; bosses like Hans only have a single front-facing
    // frame per letter (HANSA0, HANSB0, ...) with no "1" rotation, so they default to 0.
    internal static bool HasDirectionalSprites(string sprite, string frameLetter)
    {
        var cacheKey = $"{sprite}{frameLetter}";
        if (_directionalSpriteCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var hasRotations = _assetManager.Exists<SpriteAsset>($"{sprite}{frameLetter}1");
        _directionalSpriteCache[cacheKey] = hasRotations;
        return hasRotations;
    }

    internal static int CalcRotate(Entities.Actors.Actor ob)
    {
        var viewangle = (int)(player.Angle + (centerx - ob.ViewX) / (8 * viewwidth / 320.0));

        // A projectile (Rocket, the Death Knight's HeavyRocket) has no Dir -- it flies at an
        // arbitrary angle -- so it rotates by its heading.
        var angle = ob.Flags.Contains("PROJECTILE", StringComparer.OrdinalIgnoreCase)
            ? (viewangle - 180) - ob.Angle
            : (viewangle - 180) - dirangle[(byte)ob.Dir];

        angle += ANGLES / 16;
        while (angle >= ANGLES) angle -= ANGLES;
        while (angle < 0) angle += ANGLES;

        return (angle / (ANGLES / 8)) + 1;
    }

    internal static void DrawScaleds()
    {
        int i, least, numvisable, height;
        int statptr;
        int farthest = -1;
        int visptr, visstep;

        visptr = 0;

        //
        // place static objects
        //
        //for (statptr = 0; statptr != laststatobj; statptr++)
        var actorList = _mapManager.GetActors();

        // Manual node walk instead of foreach: GetBonus() can remove the current
        // actor from _actors (picked-up inventory), which invalidates a foreach
        // enumerator on the next MoveNext(). Grabbing the next node before running
        // any code that might remove the current one keeps traversal safe, since
        // removing a LinkedList node doesn't touch its neighbors' Next/Previous.
        var node = actorList.First;
        while (node != null)
        {
            var actor = node.Value;
            node = node.Next;

            if (actor == null)
                continue;                                               // object has been deleted

            if (actor is PlayerPawn)
                continue;                                               // the camera itself, never drawn

            visobj_t visptr_val = new visobj_t();
            //statobj_t statptr_val = statobjlist[statptr];
            if (actor.CurrentState == null)
                continue;

            // A marker with no sprite (a patrol point) takes no vislist slot. Inventory still
            // goes through below, where touching it is what picks it up.
            if (actor.CurrentState.Sprite == "TNT1" && actor is not Inventory)
                continue;

            // Enemies (Program.EnemyAI.cs) move between tiles and need 8-way rotation, so
            // they're transformed like legacy "active objects" (TransformActor/CalcRotate,
            // fixed-point X/Y, checked against all 9 surrounding spotvis tiles) instead of
            // the tile-snapped, always-front-facing path decorations/pickups use below.
            // Projectiles and their smoke/explosions (spawned Active by SpawnAtActor) fly
            // between tiles too, so they take the same path.
            if (actor.ResolvedStates.ContainsKey("Chase") || actor.Active == activetypes.ac_yes)
            {
                var atx = actor.TileX;
                var aty = actor.TileY;
                if (!(_mapManager.spotvis[atx, aty]
                    || _mapManager.spotvis[atx - 1, aty]
                    || _mapManager.spotvis[atx + 1, aty]
                    || _mapManager.spotvis[atx, aty - 1]
                    || _mapManager.spotvis[atx - 1, aty - 1]
                    || _mapManager.spotvis[atx + 1, aty - 1]
                    || _mapManager.spotvis[atx, aty + 1]
                    || _mapManager.spotvis[atx - 1, aty + 1]
                    || _mapManager.spotvis[atx + 1, aty + 1]))
                {
                    actor.RuntimeFlags &= ~objflags.FL_VISABLE;
                    continue;
                }

                TransformActor(actor);
                if (actor.ViewHeight == 0)
                {
                    actor.RuntimeFlags &= ~objflags.FL_VISABLE;
                    continue;
                }

                actor.RuntimeFlags |= objflags.FL_VISABLE;

                var rotationDigit = HasDirectionalSprites(actor.CurrentState.Sprite, actor.CurrentState.FrameLetter)
                    ? CalcRotate(actor)
                    : 0;
                visptr_val.shapenum = $"{actor.CurrentState.Sprite}{actor.CurrentState.FrameLetter}{rotationDigit}";
                visptr_val.viewx = actor.ViewX;
                visptr_val.viewheight = (short)actor.ViewHeight;

                if (visptr < MAXVISABLE - 1)
                {
                    visptr_val.tilex = atx;
                    visptr_val.tiley = aty;
                    vislist[visptr] = visptr_val;
                    visptr++;
                }
                continue;
            }

            visptr_val.shapenum = actor.CurrentState.GetShapeName(objdirtypes.nodir);

            if (!_mapManager.spotvis[(int)actor.Position.X, (int)actor.Position.Y])
                continue;                                               // not visable

            if (TransformTile((int)actor.Position.X, (int)actor.Position.Y,
                ref visptr_val.viewx, ref visptr_val.viewheight) && actor is Inventory inventory)
            {
                GetBonus(inventory);
                if (actorList.Contains(actor) == false)
                    continue;                                           // object has been taken
            }

            if (visptr_val.viewheight == 0)
                continue;                                               // to close to the object

            if (visptr < (MAXVISABLE - 1))    // don't let it overflow
            {
                visptr_val.tilex = (byte)actor.Position.X;
                visptr_val.tiley = (byte)actor.Position.Y;
                //visptr_val.flags = actor.Flags;
                vislist[visptr] = visptr_val;
                visptr++;
            }
        }

        //
        // draw from back to front
        //
        numvisable = (int)(visptr);

        if (numvisable == 0)
            return;                                                                 // no visable objects

        for (i = 0; i < numvisable; i++)
        {
            least = 32000;
            for (visstep = 0; visstep < visptr; visstep++)
            {
                visobj_t visstep_val = vislist[visstep];
                height = visstep_val.viewheight;
                if (height < least)
                {
                    least = height;
                    farthest = visstep;
                }
            }
            //
            // draw farthest
            //
            if (farthest != -1)
            {
                visobj_t farthest_obj = vislist[farthest];
                ScaleShape(farthest_obj);

                farthest_obj.viewheight = 32000;
            }
        }
    }

    static string[][] weaponscale = {
        ["KNIFA0", "KNIFB0", "KNIFC0", "KNIFD0", "KNIFE0"],
        ["PISGA0", "PISGB0", "PISGC0", "PISGD0", "PISGE0"],
        ["MCHGA0", "MCHGB0", "MCHGC0", "MCHGD0", "MCHGE0"],
        ["CHGGA0", "CHGGB0", "CHGGC0", "CHGGD0", "CHGGE0"]
    };

    internal static void DrawPlayerWeapon()
    {
        string shapenum;
        if (gamestate.victoryflag)
        {
            if (player.CurrentState?.StateName == PlayerPawn.DeathCamState && (GameEngineManager.GetTimeCount() & 32) != 0)
                SimpleScaleShape(viewwidth / 2, "DCAMA", viewheight + 1);
            return;
        }

        if (gamestate.weapon != weapontypes.wp_none)
        {
            shapenum = weaponscale[(int)gamestate.weapon][gamestate.weaponframe];
            SimpleScaleShape(viewwidth / 2, shapenum, viewheight + 1);
        }

        if (demorecord || demoplayback)
            SimpleScaleShape(viewwidth / 2, "DEMOA", viewheight + 1);
    }

    internal static void ThreeDRefresh()
    {
        // Create a Span<T> from the multidimensional array's data reference
        Span<bool> data = MemoryMarshal.CreateSpan(
            ref _mapManager.spotvis[0, 0], // Reference to the first element
            _mapManager.spotvis.Length     // Total number of elements
        );

        // Fill the span with the value
        data.Fill(false);

        if (!((demorecord || demoplayback)))
        {
            if (_mapManager.tilemap[player.TileX, player.TileY] == 0 ||
             (_mapManager.tilemap[player.TileX, player.TileY] & BIT_DOOR) != 0)
                _mapManager.spotvis[player.TileX, player.TileY] = true;       // Detect all sprites over player fix
        }


        vbuf = 0;
        vbufPtr = _videoManager.LockSurface();
        if (vbufPtr == IntPtr.Zero) return;

        vbuf += (int)screenofs;

        Setup3DView();

        //
        // follow the walls from there to the right, drawing as we go
        //
        VGAClearScreen();

        WallRefresh();

        _mapManager.MarkSeenFromSpotvis();      // floor the rays crossed, for the automap

        //
        // draw all the scaled images
        //
        DrawScaleds();                  // draw scaled stuff

        DrawPlayerWeapon();    // draw player's hands

        _videoManager.UnlockSurface();
        vbuf = 0;

        //
        // show screen and time last cycle
        //

        if (fizzlein)
        {
            _videoManager.Transition(levelFadeStyle, 0, 0, _videoManager.screenWidth, _videoManager.screenHeight, (uint)levelFadeTics);
            fizzlein = false;

            lasttimecount = (int)GameEngineManager.GetTimeCount();          // don't make a big tic count
        }
        else
        {
            RestoreBorderAfterConsole();

            DrawAutomap();

            if (fpscounter)
            {
                fontnumber = "SmallFont";
                SETFONTCOLOR("Grey", "VIEWCOLOR");
                PrintX = 4; PrintY = 1;
                _videoManager.Bar(0, 0, 40, 10, bordercol);
                US_Print(fps.ToString());
                US_Print(" fps");
            }

            // Taken here, before the console is drawn, so it never shows in the shot.
            if (screenshotPending)
            {
                screenshotPending = false;
                _consoleManager.Print(TakeScreenshot());
            }

            DrawConsole();
            _videoManager.Update();
        }

        if (fpscounter)
        {
            fps_frames++;
            fps_time += (int)tics;

            if (fps_time > 35)
            {
                fps_time -= 35;
                fps = fps_frames << 1;
                fps_frames = 0;
            }
        }
    }
}
