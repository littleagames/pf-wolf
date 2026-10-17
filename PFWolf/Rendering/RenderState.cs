using PFWolf.Assets;

namespace PFWolf.Rendering;

/// <summary>
/// The renderer's shared state for the frame being drawn: the view and its projection, the
/// ray trace, the queue of posts a screen column draws, and the arch being traced through. The
/// renderer's parts (the trace and wall hits, posts, arches, sprites, weather) all read and
/// write it, so it's plain fields, named as the renderer has always named them.
/// </summary>
internal sealed class RenderState
{
    internal const int MaxPosts = 16;

    //
    // projection: the view window and how the world is projected into it
    //
    internal int focallength;
    internal uint screenofs;
    internal int viewscreenx, viewscreeny;
    internal int viewwidth;
    internal int viewheight;
    internal short centerx, centery;    // centery is this frame's horizon: basecentery moved by viewpitch
    internal short basecentery;         // the horizon looking straight ahead, mid view
    internal int shootdelta;            // pixels away from centerx a target can be
    internal int projectionwidth;       // the view width the field of view is worked out for (see ProjectionWidth)
    internal int scale;
    internal int heightnumerator;

    internal int vbuf;                  // pointer index
    internal IntPtr vbufPtr = IntPtr.Zero;

    internal short[] wallheight = [];
    internal short[] pixelangle = [];

    //
    // refresh variables
    //
    internal int viewx, viewy;
    internal short viewangle;
    internal int viewsin, viewcos;
    internal double viewpitch;          // degrees, up positive; this frame's, from the camera
    internal int vieweyez = GameConstants.EyeDefault;     // this frame's, from the camera
    internal bool markseen;             // whether this frame's walls and floor go on the automap: only the player's view

    internal int postx;
    internal TextureAsset postsource;   // the texture of the post ScalePost draws
    internal int postofs;               // its column, times 64 (as the texture's across a tile)
    internal TextureAsset? uppersource; // stories above the first, if not postsource (door lintels)
    internal int upperofs;

    //
    // ray tracing variables
    //
    internal short focaltx, focalty;
    internal uint xpartialup, xpartialdown, ypartialup, ypartialdown;
    internal short midangle;
    internal ushort tilehit;
    internal int pixx;
    internal short xtile, ytile;
    internal short xtilestep, ytilestep;
    internal int xintercept, yintercept;
    internal int xinttile, yinttile;
    internal ushort texdelta;

    // the trace saved while it looks on past the nearest wall (tall walls behind short ones)
    internal short savedxtile, savedytile;
    internal int savedxintercept, savedyintercept, savedxinttile, savedyinttile;
    internal ushort savedtexdelta;
    internal bool pastwall;             // tracing on past the nearest wall: tiles aren't visible

    //
    // the posts a column draws, nearest last: each wall the trace hits queues one (QueuePost)
    //
    internal int postcount;
    internal readonly short[] postheights = new short[MaxPosts];
    internal readonly byte[] postfirststory = new byte[MaxPosts];
    internal readonly byte[] poststories = new byte[MaxPosts];
    internal readonly TextureAsset[] postlower = new TextureAsset[MaxPosts];
    internal readonly int[] postlowerofs = new int[MaxPosts];
    internal readonly TextureAsset[] postupper = new TextureAsset[MaxPosts];
    internal readonly int[] postupperofs = new int[MaxPosts];
    internal readonly short[] postunderside = new short[MaxPosts];     // an arch's: the height where the trace leaves
    internal readonly TextureAsset?[] postundertexture = new TextureAsset?[MaxPosts];
    internal readonly bool[] postunderflat = new bool[MaxPosts];         // the underside's texture is a ceiling
    internal readonly byte[][] postlight = new byte[MaxPosts][];        // its face's light row
    internal readonly TextureAsset?[] postpatch = new TextureAsset?[MaxPosts];     // a wall patch over it
    internal readonly int[] postpatchofs = new int[MaxPosts];
    internal readonly byte[] postpatchstory = new byte[MaxPosts];

    internal int hittile = -1;
    internal short postheight;          // the height (as wallheight) of the post ScalePost queues
    internal int hitstories;            // how many stories tall the wall the trace just hit is
    internal bool columnhit;            // this column's trace has hit a wall: wallheight is set
    internal int columntallest;         // the most stories of any wall it has hit
    internal int columntop;             // the highest screen row any of them reach

    // the wall patch for the post ScalePost queues, if it has one
    internal TextureAsset? patchsource;
    internal int patchofs;              // its column (never mirrored with a flipped face)
    internal int patchstory;
    internal bool hitpushwall;          // the wall being hit is a moving pushwall: its tile is pwallx, pwally

    internal int diaghitu;              // where the last TraceDiagonal hit, as the tile-local x (0..TileGlobal)

    //
    // the arch the trace is passing under
    //
    internal bool archopen;
    internal short archheight;          // where the trace entered it (short.MaxValue: the view is inside)
    internal int archstories;
    internal TextureAsset? archtexture;
    internal TextureAsset? archunderside;
    internal bool archunderflat;        // archunderside is the tile's ceiling flat
    internal int archofs;
    internal byte[] archlight;          // its face's light row: where the trace entered it

    internal readonly TextureAsset notexture;    // what a post shows with no texture: nothing

    internal RenderState(TextureAsset notexture, byte[] noshade)
    {
        this.notexture = notexture;
        postsource = notexture;
        archlight = noshade;
    }
}
