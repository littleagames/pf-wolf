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
    internal const int MaxPosts = 48;

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
    internal readonly sbyte[] PostFirstStory = new sbyte[MaxPosts];  // below 0: stories under the floor
    internal readonly int[] PostClipTop = new int[MaxPosts];         // the only rows it's drawn in
    internal readonly int[] PostClipBottom = new int[MaxPosts];
    internal readonly sbyte[] PostStories = new sbyte[MaxPosts];     // the story above its top (0 or below: under the floor)
    internal readonly TextureAsset[] PostLower = new TextureAsset[MaxPosts];
    internal readonly int[] PostLowerOfs = new int[MaxPosts];
    internal readonly TextureAsset[] PostUpper = new TextureAsset[MaxPosts];
    internal readonly int[] PostUpperOfs = new int[MaxPosts];
    internal readonly int[] PostScrollRow = new int[MaxPosts];          // texels down a scroller has moved it
    internal readonly short[] PostUnderside = new short[MaxPosts];     // a cap's: the height where the trace leaves (0: none)
    internal readonly TextureAsset?[] PostUnderTexture = new TextureAsset?[MaxPosts];
    internal readonly bool[] PostUnderFlat = new bool[MaxPosts];         // the cap's texture is a flat
    internal readonly sbyte[] PostCapStory = new sbyte[MaxPosts];       // the story the cap is at the bottom of
    internal readonly bool[] PostCapUp = new bool[MaxPosts];            // a floor (its top) rather than an underside
    internal readonly byte[][] PostLight = new byte[MaxPosts][];        // its face's light row
    internal readonly TextureAsset?[] PostPatch = new TextureAsset?[MaxPosts];     // a wall patch over it
    internal readonly int[] PostPatchOfs = new int[MaxPosts];
    internal readonly byte[] PostPatchStory = new byte[MaxPosts];

    //
    // the rows each column's walls painted (DrawPost, DrawUnderside), as top and bottom row pairs:
    // the flats are cast after the walls, only into the rows left (SkyAndFlats.DrawFlats)
    //
    internal const int MaxCovers = 2 * MaxPosts;     // a post and its cap each
    internal int[] CoverCount = [];
    internal int[] CoverTop = [], CoverBottom = [];    // column x's are from x * MaxCovers

    //
    // the see-through doors each column's trace went on past (a door with a masked texture), nearest
    // first: drawn among the sprites, after the floor and what's behind them (SeeThroughPosts).
    // Column x's are from x * MaxSeeThrough; SeeThroughCount is how many are still to draw.
    //
    internal const int MaxSeeThrough = 4;
    internal bool AnySeeThrough;        // this frame's trace queued one
    internal int[] SeeThroughCount = [];
    internal short[] SeeThroughHeight = [];
    internal byte[] SeeThroughStories = [];
    internal TextureAsset[] SeeThroughLower = [], SeeThroughUpper = [];
    internal int[] SeeThroughLowerOfs = [], SeeThroughUpperOfs = [];
    internal byte[][] SeeThroughLight = [];

    /// <summary>Makes room for the covers (and see-through doors) of a view width columns across</summary>
    internal void SizeCovers(int width)
    {
        if (CoverCount.Length >= width)
            return;
        CoverCount = new int[width];
        CoverTop = new int[width * MaxCovers];
        CoverBottom = new int[width * MaxCovers];

        SeeThroughCount = new int[width];
        SeeThroughHeight = new short[width * MaxSeeThrough];
        SeeThroughStories = new byte[width * MaxSeeThrough];
        SeeThroughLower = new TextureAsset[width * MaxSeeThrough];
        SeeThroughUpper = new TextureAsset[width * MaxSeeThrough];
        SeeThroughLowerOfs = new int[width * MaxSeeThrough];
        SeeThroughUpperOfs = new int[width * MaxSeeThrough];
        SeeThroughLight = new byte[width * MaxSeeThrough][];
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
    internal int HitFirst;              // the wall the trace just hit: the story its block starts at,
    internal int HitStories;            // and the one it stops below, counted up from the player's floor (0)
    internal bool ColumnHit;            // this column's trace has hit a wall: WallHeight is set

    //
    // stories: heights in the trace are counted in stories up from the player's floor (story 0,
    // the player's story; below it negative). Set for the frame by WallPosts.StartFrame.
    //
    internal int ZBase;                 // texels from the ground up to the player's floor: textures repeat up from the ground
    internal int GroundStory;           // the ground, in stories from the player's floor (0 or below)
    internal int CeilingStory;          // the level's ceiling (the top of its walls)
    internal bool OpaqueCeiling;        // no sky: the ceiling hides what's above it, and clips the walls
    internal int TallestStory;          // the top of the tallest block anywhere

    // the rows of this column nothing nearer has covered yet, from the top or from the bottom:
    // farther posts are clipped to them, and the trace stops once there are none
    internal int OpenTop, OpenBottom;
    internal bool ColumnOpen => OpenTop <= OpenBottom;

    //
    // the tile the trace is crossing, and what it queues as it leaves (WallPosts.OpenTile)
    //
    internal bool CellOpen;
    internal bool CellPlain;            // no block, its floor the player's: only what it covers counts
    internal bool CellFocal;
    internal int CellEdgeX, CellEdgeY;  // where the trace entered it
    internal int CellX, CellY;

    // a run of plain tiles, what they cover worked out once it ends (the edges it starts and ends at)
    internal bool PlainOpen, PlainFocal;
    internal int PlainEntryX, PlainEntryY, PlainExitX, PlainExitY;
    internal short CellEntry;           // the height (as WallHeight) where the trace entered it (short.MaxValue: the view is in it)
    internal bool CellFace;             // its block's near face is queued as it's left
    internal int CellFirst, CellTop;    // that block's stories
    internal TextureAsset? CellTexture;
    internal int CellOfs;
    internal byte[] CellLight;
    internal bool CellOverhead;         // the block is overhead: its underside shows
    internal TextureAsset? CellUnderside;
    internal bool CellUnderFlat;
    internal bool CellFloorCap;         // its floor isn't the player's: drawn at CellFloor as it's left
    internal int CellFloor;
    internal bool CellCeiling;          // the level's ceiling shows over it (opaque: it covers rows)

    // a run of tiles' floors at one height, drawn as one cap from where the run starts to where it
    // ends, clipped to the rows open when it started
    internal bool RunOpen;
    internal int RunFloor;
    internal short RunEntry, RunExit;
    internal int RunClipTop, RunClipBottom;

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
    internal int ArchFirst;             // the stories above the floor it starts at (1 for a door's lintel)
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
        CellLight = noshade;
    }
}
