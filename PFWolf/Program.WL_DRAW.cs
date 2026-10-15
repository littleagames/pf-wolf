using SDL2;
using System.Runtime.InteropServices;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf;

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

    static int vbuf { get => render.vbuf; set => render.vbuf = value; }   // pointer index
    static IntPtr vbufPtr { get => render.vbufPtr; set => render.vbufPtr = value; }

    static visobj_t[] vislist = new visobj_t[MAXVISABLE];

    static int lasttimecount;
    static int frameon;
    static bool fpscounter = false;

    static int fps_frames = 0, fps_time = 0, fps = 0;

#if USE_FLOORCEILINGTEXT || USE_CLOUDSKY
    short[] spanstart;
#endif

    internal static short[] wallheight { get => render.wallheight; set => render.wallheight = value; }

    //
    // math tables
    //
    internal static short[] pixelangle { get => render.pixelangle; set => render.pixelangle = value; }
    internal static int[] finetangent = new int[FINEANGLES / 4];
    internal static int[] sintable = new int[ANGLES + ANGLES / 4];
    internal static int[] costable = new int[ANGLES];   // sintable from ANGLES/4 on (filled with it in BuildTables)

    //
    // refresh variables
    internal static int viewx { get => render.viewx; set => render.viewx = value; }
    internal static int viewy { get => render.viewy; set => render.viewy = value; }
    internal static short viewangle { get => render.viewangle; set => render.viewangle = value; }
    internal static int viewsin { get => render.viewsin; set => render.viewsin = value; }
    internal static int viewcos { get => render.viewcos; set => render.viewcos = value; }

    internal static int postx { get => render.postx; set => render.postx = value; }
    static readonly TextureAsset notexture = new() { RawData = new byte[TEXTURESIZE * TEXTURESIZE] };

    // The renderer's shared per-frame state (Rendering/RenderState.cs): the fields below, under their old names, are its
    internal static readonly RenderState render = new(notexture, Lighting.noshade);

    internal static TextureAsset postsource { get => render.postsource; set => render.postsource = value; }   // the texture of the post ScalePost draws
    internal static int postofs { get => render.postofs; set => render.postofs = value; }   // its column, times 64 (as the texture's across a tile)
    static TextureAsset? uppersource { get => render.uppersource; set => render.uppersource = value; }   // stories above the first, if not postsource (door lintels)
    static int upperofs { get => render.upperofs; set => render.upperofs = value; }

    //
    // tall walls: every wall is wallstories 64 unit stories tall, built up from the floor
    //
    internal const int MAXWALLSTORIES = 8;
    internal static int wallstories { get => render.wallstories; set => render.wallstories = value; }

    //
    // ray tracing variables
    //
    internal static short focaltx { get => render.focaltx; set => render.focaltx = value; }
    internal static short focalty { get => render.focalty; set => render.focalty = value; }
    internal static uint xpartialup { get => render.xpartialup; set => render.xpartialup = value; }
    internal static uint xpartialdown { get => render.xpartialdown; set => render.xpartialdown = value; }
    internal static uint ypartialup { get => render.ypartialup; set => render.ypartialup = value; }
    internal static uint ypartialdown { get => render.ypartialdown; set => render.ypartialdown = value; }

    internal static short midangle { get => render.midangle; set => render.midangle = value; }

    internal static ushort tilehit { get => render.tilehit; set => render.tilehit = value; }
    internal static int pixx { get => render.pixx; set => render.pixx = value; }

    internal static short xtile { get => render.xtile; set => render.xtile = value; }
    internal static short ytile { get => render.ytile; set => render.ytile = value; }
    internal static short xtilestep { get => render.xtilestep; set => render.xtilestep = value; }
    internal static short ytilestep { get => render.ytilestep; set => render.ytilestep = value; }
    internal static int xintercept { get => render.xintercept; set => render.xintercept = value; }
    internal static int yintercept { get => render.yintercept; set => render.yintercept = value; }
    internal static int xinttile { get => render.xinttile; set => render.xinttile = value; }
    internal static int yinttile { get => render.yinttile; set => render.yinttile = value; }
    internal static ushort texdelta { get => render.texdelta; set => render.texdelta = value; }


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

    /*
    ====================
    =
    = Pitch (y-shearing)
    =
    = Looking up or down doesn't tilt the view: it moves the horizon (centery) down or up the
    = screen, by as many pixels as the pitch's tangent at the projection's focal length (scale,
    = in pixels). Walls stay upright, so everything draws as before, only measured from the
    = moved horizon. It's kept on the screen, which limits how far the view can look.
    =
    ====================
    */

    internal static double viewpitch { get => render.viewpitch; set => render.viewpitch = value; }   // degrees, up positive; this frame's, from the camera

    /// <summary>The steepest pitch, in degrees, that keeps the horizon a row inside the view</summary>
    internal static double MaxPitch() =>
        scale > 0 ? Math.Atan((basecentery - 1) / (double)scale) * 180 / Math.PI : 0;

    static void SetupPitch()
    {
        double max = MaxPitch();
        viewpitch = Math.Clamp(viewpitch, -max, max);
        int offset = (int)Math.Round(scale * Math.Tan(viewpitch * Math.PI / 180));
        offset = Math.Clamp(offset, 1 - basecentery, basecentery - 1);
        centery = (short)(basecentery + offset);
    }

    /*
    ====================
    =
    = Eye height
    =
    = How high above the floor the view is, in texels (64 a story). Vanilla's eye is half a
    = story up. It stays under one story: wall tops are never drawn, so a view over a one
    = story wall would show the floor behind it through it.
    =
    ====================
    */

    internal const int EYEDEFAULT = TEXTURESIZE / 2, MINEYE = 4, MAXEYE = TEXTURESIZE - 4;

    internal static int vieweyez { get => render.vieweyez; set => render.vieweyez = value; }   // this frame's, from the camera

    static bool markseen { get => render.markseen; set => render.markseen = value; }   // whether this frame's walls and floor go on the automap: only the player's view

    /// <summary>Marks a wall face the rays hit as seen, for the automap, when the view is the player's</summary>
    static void MarkSeen(int x, int y, SeenFlags face)
    {
        if (markseen)
            _mapManager.seen[x, y] |= face;
    }


    /// <summary>How many half stories a floor or ceiling z texels up is from the eye</summary>
    static double EyeHalves(int z) => skyAndFlats.EyeHalves(z);

    internal static void Setup3DView()
    {
        viewangle = camera.Angle;
        midangle = (short)(viewangle * (FINEANGLES / ANGLES));

        viewpitch = camera.Pitch;
        vieweyez = Math.Clamp(camera.EyeZ, MINEYE, MAXEYE);
        SetupPitch();

        viewsin = sintable[viewangle];
        viewcos = costable[viewangle];

        viewx = camera.X - MathUtils.FixedMul(focallength, viewcos);
        viewy = camera.Y + MathUtils.FixedMul(focallength, viewsin);

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
    const int MAXPOSTS = RenderState.MAXPOSTS;
    static int postcount { get => render.postcount; set => render.postcount = value; }
    static short[] postheights => render.postheights;
    static byte[] postfirststory => render.postfirststory;
    static byte[] poststories => render.poststories;
    static TextureAsset[] postlower => render.postlower;
    static int[] postlowerofs => render.postlowerofs;
    static TextureAsset[] postupper => render.postupper;
    static int[] postupperofs => render.postupperofs;
    static short[] postunderside => render.postunderside;   // an arch's: the height where the trace leaves it, 0 for none
    static TextureAsset?[] postundertexture => render.postundertexture;
    static bool[] postunderflat => render.postunderflat;   // the underside's texture is a ceiling flat
    static byte[][] postlight => render.postlight;   // its face's light row
    static TextureAsset?[] postpatch => render.postpatch;   // a wall patch over it (mapdefs patches)
    static int[] postpatchofs => render.postpatchofs;   // the patch's column: postlowerofs, unless the wall's face is flipped
    static byte[] postpatchstory => render.postpatchstory;   // the patch's story (MapPatchTranslation.Story)

    // The open tile the trace last came from (a TileIndex): walls and doors it hits are seen from
    // there, so take its base light. A wall or door tile keeps the one before it (a wall the
    // trace passes, an open door).
    static int hittile { get => render.hittile; set => render.hittile = value; }

    internal static short postheight { get => render.postheight; set => render.postheight = value; }   // the height (as wallheight) of the post ScalePost queues
    static int hitstories { get => render.hitstories; set => render.hitstories = value; }   // how many stories tall the wall the trace just hit is
    static bool columnhit { get => render.columnhit; set => render.columnhit = value; }   // this column's trace has hit a wall: wallheight is set
    static int columntallest { get => render.columntallest; set => render.columntallest = value; }   // the most stories of any wall it has hit
    static int columntop { get => render.columntop; set => render.columntop = value; }   // the highest screen row any of them reach

    // A column's posts and the arches the trace passes under (Rendering/WallPosts.cs)
    static WallPosts wallPosts = null!;     // made in InitServices

    static WallPosts MakeWallPosts() =>
        new(_videoManager, _mapManager, lighting, skyAndFlats, render, weather, session, WallTexture);

    static void EnterTileLight(int x, int y) => wallPosts.EnterTileLight(x, y);
    static byte[] WallLight(int x, int y) => wallPosts.WallLight(x, y);
    internal static void ScalePost() => wallPosts.ScalePost();
    static void QueuePost(short height, int firststory, int stories, TextureAsset lower, int lowerofs, TextureAsset upper, int upperofs,
        short underside = 0, TextureAsset? undertexture = null, byte[]? light = null, bool underflat = false,
        TextureAsset? patch = null, int patchofs = 0, int patchstory = 0) =>
        wallPosts.QueuePost(height, firststory, stories, lower, lowerofs, upper, upperofs, underside, undertexture, light, underflat, patch, patchofs, patchstory);
    static void DrawPosts() => wallPosts.DrawPosts();
    static bool IsArch(int tilex, int tiley) => wallPosts.IsArch(tilex, tiley);
    static void OpenArch(int tilex, int tiley, int edgex, int edgey, bool vertical) => wallPosts.OpenArch(tilex, tiley, edgex, edgey, vertical);
    static void OpenFocalArch() => wallPosts.OpenFocalArch();
    static void CloseArch(int edgex, int edgey) => wallPosts.CloseArch(edgex, edgey);
    static short HeightAt(int x, int y) => wallPosts.HeightAt(x, y);
    static int VertEdge => wallPosts.VertEdge;
    static int HorizEdge => wallPosts.HorizEdge;
    static bool TracePastWall(int tilex, int tiley) => wallPosts.TracePastWall(tilex, tiley);
    static void SaveTrace() => wallPosts.SaveTrace();
    static void RestoreTrace() => wallPosts.RestoreTrace();
    internal static short CalcHeight() => wallPosts.CalcHeight();
    static int FloorRow(int half) => wallPosts.FloorRow(half);
    static string SeenFace(bool vertical) => wallPosts.SeenFace(vertical);
    static int LintelColumn(int along, bool flip) => WallPosts.LintelColumn(along, flip);

    static bool archopen { get => render.archopen; set => render.archopen = value; }
    static short archheight { get => render.archheight; set => render.archheight = value; }   // where the trace entered it (short.MaxValue: the view is inside)
    static int archstories { get => render.archstories; set => render.archstories = value; }
    static TextureAsset? archtexture { get => render.archtexture; set => render.archtexture = value; }
    static TextureAsset? archunderside { get => render.archunderside; set => render.archunderside = value; }
    static bool archunderflat { get => render.archunderflat; set => render.archunderflat = value; }   // archunderside is the tile's ceiling flat
    static int archofs { get => render.archofs; set => render.archofs = value; }
    static byte[] archlight { get => render.archlight; set => render.archlight = value; }   // its face's light row: where the trace entered it, seen from the tile before

    //
    // the trace's position, kept from before it enters a tile, to carry on past a wall it hits there
    //
    static short savedxtile { get => render.savedxtile; set => render.savedxtile = value; }
    static short savedytile { get => render.savedytile; set => render.savedytile = value; }
    static int savedxintercept { get => render.savedxintercept; set => render.savedxintercept = value; }
    static int savedyintercept { get => render.savedyintercept; set => render.savedyintercept = value; }
    static int savedxinttile { get => render.savedxinttile; set => render.savedxinttile = value; }
    static int savedyinttile { get => render.savedyinttile; set => render.savedyinttile = value; }
    static ushort savedtexdelta { get => render.savedtexdelta; set => render.savedtexdelta = value; }
    static bool pastwall { get => render.pastwall; set => render.pastwall = value; }   // tracing on past the nearest wall: tiles aren't visible


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
        var face = SeenFace(vertical: true);
        bool jamb = false;
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
                // the door's jamb on the side the ray left it from
                var door = doorobjlist[doortile & ~BIT_DOOR];
                wallpic = DoorFace(door, xtilestep == 1 ? "east" : "west");
                SetUpperPost((mapTexture ?? MapTextureTranslation.None).WallFace(face),
                    FlipColumn(mapTexture, face, texture), xtile, yinttile);   // the wall above the door frame
                jamb = true;
            }
            else
            {
                wallpic = (mapTexture ?? MapTextureTranslation.None).WallFace(face);
            }
        }
        else
        {
            mapDefs?.Walls.TryGetValue(tilehit, out mapTexture);
            wallpic = (mapTexture ?? MapTextureTranslation.None).WallFace(face);
        }

        var textureAsset = WorldTexture(wallpic, xtile, yinttile);
        if (textureAsset == null)
        {
            uppersource = null;
            return;
        }
        postsource = textureAsset;
        postofs = jamb ? texture : FlipColumn(mapTexture, face, texture);
        if (!jamb)                      // a door's jamb takes no patch
            SetPatchPost(xtile, yinttile, face, texture);
        else
            SeeThroughJamb(mapTexture, face, texture, xtile, yinttile);
        ScalePost();
    }

    /// <summary>
    /// A door jamb with clear pixels (a masked texture from a pk3) is drawn over the face of the
    /// wall it's on, as a patch is, so the wall shows through it. Call with the jamb as postsource.
    /// </summary>
    static void SeeThroughJamb(MapTextureTranslation? mapTexture, string face, int texture, int tilex, int tiley)
    {
        if (!postsource.Masked)
            return;
        var wall = WorldTexture((mapTexture ?? MapTextureTranslation.None).WallFace(face), tilex, tiley);
        if (wall == null)
            return;                     // nothing behind it: the jamb as it is

        patchsource = postsource;
        patchofs = texture;
        patchstory = 1;
        postsource = wall;
        postofs = FlipColumn(mapTexture, face, texture);
    }

    // A wall face's texture column, mirrored when the wall flips that face (mapdefs walls flip)
    static int FlipColumn(MapTextureTranslation? mapTexture, string face, int texture) =>
        mapTexture != null && mapTexture.IsFlipped(face) ? TEXTUREMASK - texture : texture;

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
        var face = SeenFace(vertical: false);
        bool jamb = false;
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
                wallpic = DoorFace(door, ytilestep == 1 ? "south" : "north");
                SetUpperPost((mapTexture ?? MapTextureTranslation.None).WallFace(face),
                    FlipColumn(mapTexture, face, texture), xinttile, ytile);  // the wall above the door frame
                jamb = true;
            }
            else
            {
                wallpic = (mapTexture ?? MapTextureTranslation.None).WallFace(face);
            }
        }
        else
        {
            mapDefs?.Walls.TryGetValue(tilehit, out mapTexture);
            wallpic = (mapTexture ?? MapTextureTranslation.None).WallFace(face);
        }

        var textureAsset = WorldTexture(wallpic, xinttile, ytile);
        if (textureAsset == null)
        {
            uppersource = null;
            return;
        }

        postsource = textureAsset;
        postofs = jamb ? texture : FlipColumn(mapTexture, face, texture);
        if (!jamb)                      // a door's jamb takes no patch
            SetPatchPost(xinttile, ytile, face, texture);
        else
            SeeThroughJamb(mapTexture, face, texture, xinttile, ytile);
        ScalePost();
    }

    internal static void HitVertDoor()
    {
        string doorpage = "";
        int doornumtile;
        int texture;

        doornumtile = tilehit & ~BIT_DOOR;
        texture = (doorobjlist[doornumtile].TextureAlong(yintercept) >> FIXED2TEXSHIFT) & TEXTUREMASK;

        postheight = CalcHeight();
        postx = pixx;

        // the face on the viewer's side: heading east, the ray meets the door's west face
        var door = doorobjlist[doornumtile];
        doorpage = DoorFace(door, xtilestep == 1 ? "west" : "east");

        var doorTextureAsset = WorldTexture(doorpage, door.tilex, door.tiley);
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
        texture = (doorobjlist[doornumtile].TextureAlong(xintercept) >> FIXED2TEXSHIFT) & TEXTUREMASK;

        postheight = CalcHeight();
        postx = pixx;

        // heading south, the ray meets the door's north face
        var door = doorobjlist[doornumtile];
        doorpage = DoorFace(door, ytilestep == 1 ? "north" : "south");
        var doorTextureAsset = WorldTexture(doorpage, door.tilex, door.tiley);
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

    /// <summary>
    /// A wall, door or sky texture as it shows now: an animation (animdefs) on the frame the
    /// level's clock is at, so every tile showing it is in step, and it stops while the game does
    /// </summary>
    internal static TextureAsset? WorldTexture(string name) => _assetManager.Find<TextureAsset>(name, gamestate.TimeCount);

    /// <summary>
    /// A texture on a tile's wall or door: in place of any A_ChangeTexture swapped on the tile, and
    /// an animation that plays once from when the tile last changed (a looping one in step with
    /// every other tile)
    /// </summary>
    static TextureAsset? WorldTexture(string name, int tilex, int tiley)
        => _assetManager.Find<TextureAsset>(_mapManager.ChangedTexture(tilex, tiley, name), gamestate.TimeCount,
            _mapManager.animstart[(tiley << MapManager.MAPSHIFT) + tilex]);

    static TextureAsset? patchsource { get => render.patchsource; set => render.patchsource = value; }   // the wall patch for the post ScalePost queues, if it has one
    static int patchofs { get => render.patchofs; set => render.patchofs = value; }   // its column (never mirrored with a flipped face)
    static int patchstory { get => render.patchstory; set => render.patchstory = value; }
    static bool hitpushwall { get => render.hitpushwall; set => render.hitpushwall = value; }   // the wall being hit is a moving pushwall: its tile is pwallx, pwally

    /// <summary>
    /// Sets the patch (mapdefs patches) on the face the trace sees of the wall tile it hit, for
    /// the post being drawn: none if the tile has none or it isn't on that face. A moving
    /// pushwall's patch is on its own tile, whatever tile the trace is in. ofs is the face's
    /// column before any flip, so a patch reads the right way round on a flipped face.
    /// </summary>
    static void SetPatchPost(int tilex, int tiley, string face, int ofs)
    {
        patchofs = ofs;
        patchsource = null;
        if (hitpushwall)
            (tilex, tiley) = (pwallx, pwally);

        int id = _mapManager.GetPatch(tilex, tiley);
        if (id == 0 || _mapManager.GetMapData()?.Patches.TryGetValue(id, out var patch) != true || !patch!.IsOn(face))
            return;
        patchsource = WorldTexture(patch.Texture, tilex, tiley);
        patchstory = patch.Story;
    }

    // Points the stories above the first at the named wall texture, for the post being drawn
    static void SetUpperPost(string wallpic, int texture, int tilex, int tiley)
    {
        if (hitstories == 1)
            return;
        uppersource = WorldTexture(wallpic, tilex, tiley);
        upperofs = texture;
    }

    /// <summary>
    /// The face ("north", "south", "east" or "west") the trace sees on what it hits: vertical for
    /// an edge along x = constant. Heading east, it meets a wall's west face, and so on.
    /// </summary>

    /// <summary>
    /// The texture of the wall a door is set into, from the wall tile on either side of it in
    /// line with the door, on the face the trace sees; null if neither side is a plain wall.
    /// mirrored: that wall flips the face (mapdefs walls flip).
    /// </summary>
    static TextureAsset? LintelTexture(doorobj_t door, out bool mirrored)
    {
        int dx = door.vertical ? 0 : 1, dy = door.vertical ? 1 : 0;
        var face = SeenFace(door.vertical);
        return WallTexture(door.tilex - dx, door.tiley - dy, face, out mirrored)
            ?? WallTexture(door.tilex + dx, door.tiley + dy, face, out mirrored);
    }

    // A wall tile's texture on face ("north", "south", "east" or "west"), and whether it flips it
    static TextureAsset? WallTexture(int tilex, int tiley, string face, out bool mirrored)
    {
        mirrored = false;
        var tile = _mapManager.tilemap[tilex, tiley];
        if ((tile & BIT_DOOR) != 0 || (tile & ~BIT_WALL) == 0)
            return null;                // open floor, a door or a pushwall

        MapTextureTranslation? mapTexture = null;
        if (_mapManager.GetMapData()?.Walls.TryGetValue(tile & ~BIT_WALL, out mapTexture) != true || mapTexture == null)
            return null;
        mirrored = mapTexture.IsFlipped(face);
        return WorldTexture(mapTexture.WallFace(face), tilex, tiley);
    }

    // The lintel column where the trace meets the door plane at along (y for a vertical door, x
    // for a horizontal one); flip mirrors it like the wall faces seen from the same side

    // Caps the door post being drawn with its lintel (the door texture again if it has no wall)
    static void SetLintelPost(doorobj_t door, int along, bool flip)
    {
        if (hitstories == 1)
            return;
        var lintel = LintelTexture(door, out bool mirrored);
        uppersource = lintel ?? postsource;
        upperofs = LintelColumn(along, lintel != null && mirrored ? !flip : flip);
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

        var texture = LintelTexture(door, out bool mirrored);
        if (texture == null)
            return;                     // set into nothing: leave the view above it open

        short height = HeightAt(planex, planey);
        int ofs = door.vertical
            ? LintelColumn(planey, (xtilestep == -1) != mirrored)
            : LintelColumn(planex, (ytilestep == 1) != mirrored);
        QueuePost(height, 1, stories, texture, ofs, texture, ofs, light: WallLight(planex, planey));
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

    static int diaghitu { get => render.diaghitu; set => render.diaghitu = value; }   // where the last TraceDiagonal hit, as the tile-local x (0..TILEGLOBAL)

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

        var textureAsset = WorldTexture(wallpic, tilex, tiley);
        if (textureAsset == null)
            return;
        postsource = textureAsset;
        postofs = texture;
        ScalePost();
    }

    // The ceiling and floor, the sky and the flats (Rendering/SkyAndFlats.cs)
    static SkyAndFlats skyAndFlats = null!;     // made in InitServices

    static SkyAndFlats MakeSkyAndFlats() =>
        new(_gameEngineManager, _assetManager, _videoManager, _mapManager, lighting, actorLights, render, session, WorldTexture);

    static byte ceilingcolor => skyAndFlats.ceilingcolor;
    const byte FLATMASK = SkyAndFlats.FLATMASK;
    internal static string? levelsky { get => skyAndFlats.levelsky; set => skyAndFlats.levelsky = value; }
    internal static void VGAClearScreen() => skyAndFlats.VGAClearScreen();

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
            hittile = TileIndex(focaltx, focalty);
            columnhit = pastwall = archopen = false;
            columntallest = 0;
            columntop = viewheight;
            bool focalhit = false;              // hit a wall in the view's own tile: no trace

            //
            // special treatment when player is in back tile of pushwall
            //
            hitpushwall = _mapManager.tilemap[focaltx, focalty] == BIT_WALL;
            if (hitpushwall)
            {
                SelectPushWall(focaltx, focalty);
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
                    MarkSeen(focaltx, focalty, SeenFlags.DiagonalFace);
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
        EnterTileLight(xtile - xtilestep, yinttile);
        tilehit = _mapManager.tilemap[xtile, yinttile];
        hitpushwall = tilehit == BIT_WALL;
        if (hitpushwall)
            SelectPushWall(hitx, hity);     // which of the moving pushwalls this is
        hitstories =tilehit == BIT_WALL ? _mapManager.WallStories(pwallx, pwally) : _mapManager.WallStories(hitx, hity);

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
                    if (door.OpenAt(yinttemp))
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
                    MarkSeen(hitx, hity, SeenFlags.DiagonalFace);
                    return true;
                }
                else
                {
                    xintercept = (xtile << MapConstants.TILESHIFT);

                    HitVertWall();
                }
            }

            MarkSeen(hitx, hity, xtilestep == 1 ? SeenFlags.WestFace : SeenFlags.EastFace);
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
        EnterTileLight(xinttile, ytile - ytilestep);
        tilehit = _mapManager.tilemap[xinttile, ytile];
        hitpushwall = tilehit == BIT_WALL;
        if (hitpushwall)
            SelectPushWall(hitx, hity);     // which of the moving pushwalls this is
        hitstories =tilehit == BIT_WALL ? _mapManager.WallStories(pwallx, pwally) : _mapManager.WallStories(hitx, hity);

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
                    if (door.OpenAt(xinttemp))
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
                    MarkSeen(hitx, hity, SeenFlags.DiagonalFace);
                    return true;
                }
                else
                {
                    yintercept = ytile << MapConstants.TILESHIFT;

                    HitHorizWall();
                }
            }

            MarkSeen(hitx, hity, ytilestep == 1 ? SeenFlags.NorthFace : SeenFlags.SouthFace);
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
    // the fixed-point X/Y that Monster's movement code maintains, so enemies,
    // projectiles and the BJ-victory actor can be both rendered and (enemies) shot at.
    internal static void TransformActor(Entities.Actors.Actor ob) =>
        TransformActorFrom(ob, viewx, viewy, viewsin, viewcos);

    /// <summary>TransformActor from a view other than this frame's (the player's, see PlayerSight)</summary>
    internal static void TransformActorFrom(Entities.Actors.Actor ob, int fromx, int fromy, int fromsin, int fromcos)
    {
        var gx = ob.X - fromx;
        var gy = ob.Y - fromy;

        var gxt = MathUtils.FixedMul(gx, fromcos);
        var gyt = MathUtils.FixedMul(gy, fromsin);
        var nx = gxt - gyt - ACTORSIZE;

        gxt = MathUtils.FixedMul(gx, fromsin);
        gyt = MathUtils.FixedMul(gy, fromcos);
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
    // frame per letter (HANSA0, HANSB0, ...) with no rotations, so they default to 0.
    // Any of the 8 views counts: a mod can add views 2-8 to a frame and leave the front to
    // the game's own single view (multiplayer.pk3's shooting guards, GARDF2..F8 beside GARDF0).
    internal static bool HasDirectionalSprites(string sprite, string frameLetter)
    {
        var cacheKey = $"{sprite}{frameLetter}";
        if (_directionalSpriteCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var hasRotations = Enumerable.Range(1, 8).Any(r => SpriteExists($"{sprite}{frameLetter}{r}"));
        _directionalSpriteCache[cacheKey] = hasRotations;
        return hasRotations;
    }

    /// <summary>
    /// The picture of an actor's frame from where it's seen (after TransformActor): the view
    /// for its rotation when the frame has views, else its single view (letter 0). A view the
    /// frame lacks falls back to the single view, when there is one.
    /// </summary>
    internal static string ActorShapeName(string sprite, string frameLetter, Entities.Actors.Actor actor)
    {
        var single = $"{sprite}{frameLetter}0";
        if (!HasDirectionalSprites(sprite, frameLetter))
            return single;

        var view = $"{sprite}{frameLetter}{CalcRotate(actor)}";
        return SpriteExists(view) || !SpriteExists(single) ? view : single;
    }

    /// <summary>The picture of a frame seen from the front: view 1, else its single view</summary>
    internal static string FrontShapeName(string sprite, string frameLetter) =>
        SpriteExists($"{sprite}{frameLetter}1") ? $"{sprite}{frameLetter}1" : $"{sprite}{frameLetter}0";

    internal static int CalcRotate(Entities.Actors.Actor ob)
    {
        var viewangle = (int)(Program.viewangle + (centerx - ob.ViewX) / (8 * projectionwidth / 320.0));

        // A projectile (Rocket, the Death Knight's HeavyRocket) has no Dir -- it flies at an
        // arbitrary angle -- so it rotates by its heading. So does the player's body.
        int angle;
        if (ob.Flags.Contains("PROJECTILE", StringComparer.OrdinalIgnoreCase) || IsPlayerBody(ob))
            angle = (viewangle - 180) - ob.Angle;
        else if (AimAngle(ob) is { } facing)
            angle = (viewangle - 180) - facing;
        else if (ob.Dir == objdirtypes.nodir)
            return 1;                                   // facing no way: toward whoever looks
        else
            angle = (viewangle - 180) - dirangle[(byte)ob.Dir];

        angle += ANGLES / 16;
        while (angle >= ANGLES) angle -= ANGLES;
        while (angle < 0) angle += ANGLES;

        return (angle / (ANGLES / 8)) + 1;
    }

    /// <summary>
    /// The way an enemy is drawn facing, when not the way it's going: at the player it's after
    /// while it attacks. The original only drew attacks from the front (it always faced you);
    /// with views for them (multiplayer.pk3) the others see it aim at whoever it shoots. Only
    /// for show: reads the target without picking one (Monster.Target would).
    /// </summary>
    static int? AimAngle(Entities.Actors.Actor ob)
    {
        if (ob is not Entities.Actors.Monster { TargetIfAny: { } target }
            || ob.CurrentState?.StateName?.Equals("Attack", StringComparison.OrdinalIgnoreCase) != true)
            return null;

        var fangle = Math.Atan2(ob.Y - target.Y, target.X - ob.X);
        if (fangle < 0)
            fangle += M_PI * 2;
        return (int)(fangle / (M_PI * 2) * ANGLES) % ANGLES;
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
        var looker = camera.Target;

        var node = actorList.First;
        while (node != null)
        {
            var actor = node.Value;
            node = node.Next;

            if (actor == null)
                continue;                                               // object has been deleted

            // Its colors (the pawn's, for a player's body)
            var translation = _translationManager.TableOf(actor);

            if (actor is PlayerPawn pawn)
            {
                if (VisiblePlayerBody(pawn) is not { } body)
                    continue;                                           // looked through, or with no sprites
                actor = body;                                           // what others see of them (Program.Camera.cs)
            }
            else if (actor == looker)
                continue;                                               // the one looked through

            visobj_t visptr_val = new visobj_t();
            visptr_val.translation = translation;
            //statobj_t statptr_val = statobjlist[statptr];
            if (actor.CurrentState == null)
                continue;
            visptr_val.bright = shading && IsBright(actor);

            // A marker with no sprite (a patrol point) takes no vislist slot. Inventory still
            // goes through below, where touching it is what picks it up.
            if (actor.CurrentState.Sprite == "TNT1" && actor is not Inventory)
                continue;

            // Enemies (Entities.Actors.Monster) move between tiles and need 8-way rotation, so
            // they're transformed like legacy "active objects" (TransformActor/CalcRotate,
            // fixed-point X/Y, checked against all 9 surrounding spotvis tiles) instead of
            // the tile-snapped, always-front-facing path decorations/pickups use below.
            // Projectiles and their smoke/explosions (spawned Active by SpawnAtActor) fly
            // between tiles too, so they take the same path.
            if (actor.ResolvedStates.ContainsKey("Chase") || actor.Active == activetypes.ac_yes)
            {
                var atx = actor.TileX;
                var aty = actor.TileY;

                // A neighbouring tile only counts while it's open floor: a wall the player
                // sees isn't somewhere the actor could be showing from
                bool Seen(int x, int y) => _mapManager.spotvis[x, y] && _mapManager.tilemap[x, y] == 0;
                if (!(_mapManager.spotvis[atx, aty]
                    || Seen(atx - 1, aty)
                    || Seen(atx + 1, aty)
                    || Seen(atx, aty - 1)
                    || Seen(atx - 1, aty - 1)
                    || Seen(atx + 1, aty - 1)
                    || Seen(atx, aty + 1)
                    || Seen(atx - 1, aty + 1)
                    || Seen(atx + 1, aty + 1)))
                {
                    actor.RuntimeFlags &= ~objflags.FL_VISABLE;
                    continue;
                }

                // Once near enough to the view, an enemy is awake for good (MapManager.DoActor);
                // with others, what they see wakes it instead (WakeSeenEnemies)
                if (actor is Entities.Actors.Monster && gamemode == GameMode.Single)
                    actor.Active = activetypes.ac_yes;

                // Too close or far away to draw: as the original, it keeps whether it was
                // on screen last frame (enemy aim reads that)
                TransformActor(actor);
                if (actor.ViewHeight == 0)
                    continue;

                actor.RuntimeFlags |= objflags.FL_VISABLE;

                var sprite = DrawnSprite(actor);                        // a player's body shows their weapon
                visptr_val.shapenum = ActorShapeName(sprite, actor.CurrentState.FrameLetter, actor);
                visptr_val.viewx = actor.ViewX;
                visptr_val.viewheight = (short)actor.ViewHeight;

                // A cloaked enemy shows for a frame when it's hit (bstone's FL2_DAMAGE_CLOAK)
                visptr_val.cloaked = actor.Cloaked && !actor.CloakHit && actor.Hitpoints > 0;
                actor.CloakHit = false;

                if (visptr < MAXVISABLE - 1)
                {
                    visptr_val.tilex = atx;
                    visptr_val.tiley = aty;
                    visptr_val.worldx = actor.X;
                    visptr_val.worldy = actor.Y;
                    vislist[visptr] = visptr_val;
                    visptr++;
                }
                continue;
            }

            visptr_val.shapenum = actor.CurrentState.GetShapeName(objdirtypes.nodir);

            // A panel (Program.WallSprites.cs) is drawn column by column, clipped per column,
            // so it's placed by its own geometry rather than as a billboard in its tile.
            if (IsWallSprite(actor))
            {
                if (WallSpriteSeen(actor) && TransformWallSprite(actor, visptr_val) && visptr < MAXVISABLE - 1)
                {
                    visptr_val.tilex = actor.TileX;
                    visptr_val.tiley = actor.TileY;
                    (visptr_val.worldx, visptr_val.worldy) = TileCenter(actor.TileX, actor.TileY);
                    vislist[visptr] = visptr_val;
                    visptr++;
                }
                continue;
            }

            if (!_mapManager.spotvis[(int)actor.Position.X, (int)actor.Position.Y])
                continue;                                               // not visable

            // Picking it up is TouchItems' job (Program.PlayerSight.cs), not the renderer's
            TransformTile((int)actor.Position.X, (int)actor.Position.Y, ref visptr_val.viewx, ref visptr_val.viewheight);

            if (visptr_val.viewheight == 0)
                continue;                                               // to close to the object

            if (visptr < (MAXVISABLE - 1))    // don't let it overflow
            {
                visptr_val.tilex = (byte)actor.Position.X;
                visptr_val.tiley = (byte)actor.Position.Y;
                (visptr_val.worldx, visptr_val.worldy) = TileCenter(visptr_val.tilex, visptr_val.tiley);
                //visptr_val.flags = actor.Flags;
                vislist[visptr] = visptr_val;
                visptr++;
            }
        }

        //
        // draw from back to front
        //
        numvisable = (int)(visptr);

        // Rain and snow go in among the sprites by distance (Program.Weather.cs)
        ProjectWeather();

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
                DrawWeatherBeyond(farthest_obj.viewheight);
                if (farthest_obj.wallsprite != null)
                    ScaleWallSprite(farthest_obj);
                else
                    ScaleShape(farthest_obj);

                farthest_obj.viewheight = 32000;
            }
        }

        DrawWeatherBeyond(int.MaxValue);    // and what's nearer than every sprite
    }

    internal static void DrawPlayerWeapon()
    {
        if (gamestate.victoryflag)
        {
            // The death cam's banner, on whichever frame T_DeathCam has it (TNT1 is the "off" half of its flash)
            if (player.CurrentState?.StateName == PlayerPawn.DeathCamState
                && deathCamSprite?.CurrentState is { Sprite.Length: > 0 } state && state.Sprite != "TNT1")
                SimpleScaleShape(viewwidth / 2, $"{state.Sprite}{state.FrameLetter}0", viewheight + 1);
            return;
        }

        // Only in the player's own view
        if (!camera.OnPlayer)
            return;

        // The frame the weapon's states are on (Program.PlayerWeapon.cs), in the light of the
        // player's tile but not faded: it's at arm's length. Scaled by game-info weapon-scale, it
        // still stands on the view's bottom edge (less any weapon-bob)
        if (WeaponShapeName() is { } shape)
        {
            int height = (int)((viewheight + 1) * weaponScale);
            SimpleScaleShape(viewwidth / 2, shape, height,
                weaponSprite != null && IsBright(weaponSprite) ? noshade : LightAt(TileIndex(player.TileX, player.TileY), player.X, player.Y),
                ((viewheight + 1) >> 1) - (height >> 1) + weaponBob.Drop(viewheight));
        }

        if (demorecord || demoplayback)
            SimpleScaleShape(viewwidth / 2, "DEMOA0", viewheight + 1);
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

        // Detect all sprites over camera fix. Demos need it too: Wolf4SDL 1.7c, which plays
        // the games' own demos through, always had it (enemies beside the player stay seen)
        if (_mapManager.tilemap[camera.TileX, camera.TileY] == 0 ||
         (_mapManager.tilemap[camera.TileX, camera.TileY] & BIT_DOOR) != 0)
            _mapManager.spotvis[camera.TileX, camera.TileY] = true;


        vbuf = 0;
        vbufPtr = _videoManager.LockSurface();
        if (vbufPtr == IntPtr.Zero) return;

        vbuf += (int)screenofs;

        Setup3DView();
        UpdateLightGrid();
        CastActorLights();
        _mapManager.AnimateFlats(gamestate.TimeCount);     // animated floors and ceilings, on the level's clock as walls are

        //
        // follow the walls from there to the right, drawing as we go
        //
        VGAClearScreen();

        markseen = camera.OnPlayer;     // the automap only learns what the player sees
        SetupWeather();
        WallRefresh();

        if (markseen)
            _mapManager.MarkSeenFromSpotvis();      // floor the rays crossed, for the automap

        //
        // draw all the scaled images
        //
        DrawScaleds();                  // draw scaled stuff
        PlayerSight();                  // what the player sees, when the camera is elsewhere

        DrawPlayerWeapon();    // draw player's hands

        _videoManager.UnlockSurface();
        vbuf = 0;

        // For the next save's thumbnail, before the automap or console is drawn over the view
        _videoManager.KeepViewCopy(viewscreenx, viewscreeny, viewwidth, viewheight);

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

            DrawHudMessages();      // over the automap too, but not in the save thumbnail
            DrawNetOverlay();       // waiting for someone, or asking whether to leave (Program.NetPlay.cs)

            if (fpscounter)
            {
                _videoManager.Bar(0, 0, 40, 10, bordercol);
                _graphicManager.DrawText(4, 1, $"{fps} fps", new Fonts.TextStyle(SMALL_FONT, "Grey", "VIEWCOLOR"));
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
