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
    internal int FocalLength;
    internal uint ScreenOfs;
    internal int ViewScreenX, ViewScreenY;
    internal int ViewWidth;
    internal int ViewHeight;
    internal short CenterX, CenterY;    // CenterY is this frame's horizon: BaseCenterY moved by ViewPitch
    internal short BaseCenterY;         // the horizon looking straight ahead, mid view
    internal int ShootDelta;            // pixels away from CenterX a target can be
    internal int ProjectionWidth;       // the view width the field of view is worked out for (see ProjectionWidth)
    internal int Scale;
    internal int HeightNumerator;

    internal int Vbuf;                  // pointer index
    internal IntPtr VbufPtr = IntPtr.Zero;

    internal short[] WallHeight = [];
    internal short[] PixelAngle = [];

    //
    // refresh variables
    //
    internal int ViewX, ViewY;
    internal short ViewAngle;
    internal int ViewSin, ViewCos;
    internal double ViewPitch;          // degrees, up positive; this frame's, from the camera
    internal int ViewEyeZ = GameConstants.EyeDefault;     // this frame's, from the camera
    internal bool MarkSeen;             // whether this frame's walls and floor go on the automap: only the player's view

    internal int PostX;
    internal TextureAsset PostSource;   // the texture of the post ScalePost draws
    internal int PostOfs;               // its column, times 64 (as the texture's across a tile)
    internal TextureAsset? UpperSource; // stories above the first, if not PostSource (door lintels)
    internal int UpperOfs;
    internal int ScrollRow;             // texels down its scroller has moved it (Rendering/Scrollers.cs)

    //
    // ray tracing variables
    //
    internal short FocalTx, FocalTy;
    internal uint XPartialUp, XPartialDown, YPartialUp, YPartialDown;
    internal short MidAngle;
    internal ushort TileHit;
    internal int PixX;
    internal short XTile, YTile;
    internal short XTileStep, YTileStep;
    internal int XIntercept, YIntercept;
    internal int XIntTile, YIntTile;
    internal ushort TexDelta;

    // the trace saved while it looks on past the nearest wall (tall walls behind short ones)
    internal short SavedXTile, SavedYTile;
    internal int SavedXIntercept, SavedYIntercept, SavedXIntTile, SavedYIntTile;
    internal ushort SavedTexDelta;
    internal bool PastWall;             // tracing on past the nearest wall: tiles aren't visible

    //
    // the posts a column draws, nearest last: each wall the trace hits queues one (QueuePost)
    //
    internal int PostCount;
    internal readonly short[] PostHeights = new short[MaxPosts];
    internal readonly byte[] PostFirstStory = new byte[MaxPosts];
    internal readonly byte[] PostStories = new byte[MaxPosts];
    internal readonly TextureAsset[] PostLower = new TextureAsset[MaxPosts];
    internal readonly int[] PostLowerOfs = new int[MaxPosts];
    internal readonly TextureAsset[] PostUpper = new TextureAsset[MaxPosts];
    internal readonly int[] PostUpperOfs = new int[MaxPosts];
    internal readonly int[] PostScrollRow = new int[MaxPosts];          // texels down a scroller has moved it
    internal readonly short[] PostUnderside = new short[MaxPosts];     // an arch's: the height where the trace leaves
    internal readonly TextureAsset?[] PostUnderTexture = new TextureAsset?[MaxPosts];
    internal readonly bool[] PostUnderFlat = new bool[MaxPosts];         // the underside's texture is a ceiling
    internal readonly byte[][] PostLight = new byte[MaxPosts][];        // its face's light row
    internal readonly TextureAsset?[] PostPatch = new TextureAsset?[MaxPosts];     // a wall patch over it
    internal readonly int[] PostPatchOfs = new int[MaxPosts];
    internal readonly byte[] PostPatchStory = new byte[MaxPosts];

    //
    // the rows each column's walls painted (DrawPost, DrawUnderside), as top and bottom row pairs:
    // the flats are cast after the walls, only into the rows left (SkyAndFlats.DrawFlats)
    //
    internal const int MaxCovers = 2 * MaxPosts;     // a post and its arch's underside each
    internal int[] CoverCount = [];
    internal int[] CoverTop = [], CoverBottom = [];    // column x's are from x * MaxCovers

    /// <summary>Makes room for the covers of a view width columns across</summary>
    internal void SizeCovers(int width)
    {
        if (CoverCount.Length >= width)
            return;
        CoverCount = new int[width];
        CoverTop = new int[width * MaxCovers];
        CoverBottom = new int[width * MaxCovers];
    }

    /// <summary>Column x's walls painted rows top to bottom (outside WallRefresh, with no room made, not kept)</summary>
    internal void Cover(int x, int top, int bottom)
    {
        if ((uint)x >= (uint)CoverCount.Length)
            return;
        int n = CoverCount[x];
        if (n == MaxCovers)
            return;                     // can't happen: a column draws at most MaxPosts posts
        CoverTop[x * MaxCovers + n] = top;
        CoverBottom[x * MaxCovers + n] = bottom;
        CoverCount[x] = n + 1;
    }

    internal int HitTile = -1;
    internal short PostHeight;         // the height (as WallHeight) of the post ScalePost queues
    internal int HitStories;            // how many stories tall the wall the trace just hit is
    internal bool ColumnHit;            // this column's trace has hit a wall: WallHeight is set
    internal int ColumnTallest;         // the most stories of any wall it has hit
    internal int ColumnTop;             // the highest screen row any of them reach

    // the wall patch for the post ScalePost queues, if it has one
    internal TextureAsset? PatchSource;
    internal int PatchOfs;              // its column (never mirrored with a flipped face)
    internal int PatchStory;
    internal bool HitPushwall;          // the wall being hit is a moving pushwall: its tile is pwallx, pwally

    internal int DiagHitU;              // where the last TraceDiagonal hit, as the tile-local x (0..TileGlobal)

    //
    // the arch the trace is passing under
    //
    internal bool ArchOpen;
    internal short ArchHeight;          // where the trace entered it (short.MaxValue: the view is inside)
    internal int ArchStories;
    internal TextureAsset? ArchTexture;
    internal TextureAsset? ArchUnderside;
    internal bool ArchUnderFlat;        // ArchUnderside is the tile's ceiling flat
    internal int ArchOfs;
    internal byte[] ArchLight;          // its face's light row: where the trace entered it

    internal readonly TextureAsset NoTexture;    // what a post shows with no texture: nothing

    internal RenderState(TextureAsset notexture, byte[] noshade)
    {
        this.NoTexture = notexture;
        PostSource = notexture;
        ArchLight = noshade;
    }
}
