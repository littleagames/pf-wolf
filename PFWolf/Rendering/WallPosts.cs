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
    PointDrawing points,
    GameSession session,
    WallTextureLookup wallTexture)
{
    // The open tile the trace last came from (a TileIndex): walls and doors it hits are seen from
    // there, so take its base light. A wall or door tile keeps the one before it (a wall the
    // trace passes, an open door).

    internal void EnterTileLight(int x, int y)
    {
        if (map.TileMap[x, y] == 0)
            render.HitTile = map.TileIndexOrNone(x, y);
    }

    // Half a light cell, in global units
    const int HalfCell = 1 << (Lighting.CellGlobalShift - 1);

    /// <summary>
    /// The light on a wall face at (x, y) (global units), seen from HitTile: from the light cell
    /// just in front of it, half a cell toward the view, not the one inside the wall.
    /// </summary>
    internal byte[] WallLight(int x, int y)
    {
        long gx = x + (render.ViewX > x ? HalfCell : render.ViewX < x ? -HalfCell : 0);
        long gy = y + (render.ViewY > y ? HalfCell : render.ViewY < y ? -HalfCell : 0);
        return lighting.LightAt(render.HitTile, gx, gy);
    }


    // drawn: how many stories to draw, if not all HitStories (a door under its lintel draws one)
    internal void ScalePost(int drawn = 0)
    {
        var upper = render.UpperSource ?? render.PostSource;
        int first = render.HitFirst;
        int top = drawn > 0 ? first + drawn : render.HitStories;
        if (render.OpaqueCeiling)
            top = Math.Min(top, render.CeilingStory);       // the ceiling hides what's above it
        top = Math.Max(top, first);
        QueuePost(render.PostHeight, first, top, render.PostSource, render.PostOfs, upper, render.UpperSource != null ? render.UpperOfs : render.PostOfs,
            patch: render.PatchSource, patchofs: render.PatchOfs, patchstory: render.PatchStory, scrollrow: render.ScrollRow);
        render.UpperSource = null;
        render.PatchSource = null;
        render.ScrollRow = 0;

        //
        // sprites clip against the nearest wall: nothing behind it is taller than it
        // on screen, since every wall stands at least a story high and sprites don't
        //
        if (!render.ColumnHit)
        {
            render.WallHeight[render.PixX] = render.PostHeight;
            render.ColumnHit = true;
        }

        Occlude(RowAt(render.PostHeight, top), RowAt(render.PostHeight, first) - 1);
    }

    /// <summary>
    /// The story the wall the trace has just hit starts at and the one above its top, counted up
    /// from the player's floor (HitFirst, HitStories); a door is on the player's story, up to its
    /// lintel's top
    /// </summary>
    internal void SetHitBlock(int tilex, int tiley, bool door = false)
    {
        int story = map.Story;
        int i = map.TileIndex(tilex, tiley);
        if (!door && (uint)i < (uint)map.UpperSpanStop.Length && map.UpperSpanStop[i] != 0)
        {
            // two blocks: the one it's solid with (the other's drawn as an open tile's, OpenTile)
            bool upper = map.UpperSpanStart[i] <= story;
            render.HitFirst = (upper ? map.UpperSpanStart[i] : map.SpanStart[i]) - story;
            render.HitStories = (upper ? map.UpperSpanStop[i] : map.SpanStop[i]) - story + 1;
            return;
        }
        render.HitFirst = door ? 0 : map.BlockStart(tilex, tiley) - story;
        render.HitStories = map.WallStories(tilex, tiley) - story + 1;
    }

    /*
    ====================
    =
    = Stories
    =
    = Heights in a column are counted in stories up from the player's floor: story 0 is the
    = player's, those below it negative. A row for a height at a distance is RowAt. The trace
    = queues the posts nearest first, and each is clipped to the rows nothing nearer has covered
    = yet (OpenTop to OpenBottom): the floor at the player's height and the level's ceiling are
    = drawn after the walls into the rows left (SkyAndFlats.DrawLevelFlats), so nothing farther
    = may paint over where they show. The column is done once no rows are left open.
    =
    ====================
    */

    /// <summary>Gets the frame's stories ready, from the player's story and the level's walls</summary>
    internal void StartFrame()
    {
        int story = map.Story;
        render.ZBase = (story - 1) * GameConstants.TextureSize;
        render.GroundStory = 1 - story;
        render.CeilingStory = map.LevelWallStories - story + 1;
        render.TallestStory = map.MaxWallStories - story + 1;
        render.OpaqueCeiling = !skyAndFlats.SkyShows && !map.HasCeilingSkies
            && render.CeilingStory * GameConstants.TextureSize > render.ViewEyeZ;
    }

    /// <summary>Gets a column ready to trace: every row open, no tile or floor run open</summary>
    internal void StartColumn()
    {
        render.OpenTop = 0;
        render.OpenBottom = render.ViewHeight - 1;
        render.CellOpen = render.RunOpen = render.PlainOpen = false;
    }

    /// <summary>Finishes a column's trace: what's still waiting to be queued is</summary>
    internal void FinishColumn()
    {
        FlushPlain();
        FlushRun();
    }

    // Half a story's height in pixels for a post height (as WallHeight), as DrawPost takes it
    static int PostHalf(short height) => height >> 3 is > 0 and var half ? half : 100;

    /// <summary>The screen row of the bottom of story <paramref name="story"/> (from the player's floor) for something height high (as WallHeight)</summary>
    internal int RowAt(short height, int story) => RowAtHalf(PostHalf(height), story);

    int RowAtHalf(int half, int story) => FloorRow(half) - story * 2 * half;     // a half's at most 4095, a story at most 8 either way

    /// <summary>
    /// Something nearer covers rows top to bottom: if they reach the open rows' top or bottom,
    /// those close in (rows in the middle leave them as they are)
    /// </summary>
    internal void Occlude(int top, int bottom)
    {
        if (top < 0) top = 0;
        if (bottom > render.ViewHeight - 1) bottom = render.ViewHeight - 1;
        if (top > bottom)
            return;
        if (top <= render.OpenTop && bottom >= render.OpenTop)
            render.OpenTop = bottom + 1;
        if (bottom >= render.OpenBottom && top <= render.OpenBottom)
            render.OpenBottom = top - 1;
    }

    /// <summary>
    /// The trace enters a tile at (TileX, TileY) across the edge at (edgex, edgey) (focal: the view
    /// is in it); vertical says it's an edge along x = constant. What it shows is queued as the
    /// trace leaves it (CloseTile): each of its blocks' near faces (up to two), their undersides
    /// if they're overhead, and its floors where they aren't at the player's height (a block's top
    /// below the player, the ground). A wall's face, of the block it's solid with, is WallHits'.
    /// </summary>
    internal void OpenTile(int tilex, int tiley, int edgex, int edgey, bool vertical, bool focal = false)
    {
        render.CellOpen = true;
        render.CellFocal = focal;
        render.CellEdgeX = edgex;
        render.CellEdgeY = edgey;

        int i = map.TileIndex(tilex, tiley);
        bool inmap = (uint)i < (uint)map.SpanStop.Length;
        int stop = inmap ? map.SpanStop[i] : 0;
        var tile = map.TileMap[tilex, tiley];
        render.CellPlain = tile == 0 && stop == 0 && render.GroundStory == 0;
        if (render.CellPlain)
            return;                     // most of them: see CloseTile

        int story = map.Story;
        render.CellX = tilex;
        render.CellY = tiley;
        render.CellEntry = focal ? short.MaxValue : HeightAt(edgex, edgey);
        render.CellBlocks = render.CellLowFloors = 0;
        render.CellFloor = 0;
        if ((tile & GameConstants.BitDoor) != 0 || tile == GameConstants.BitWall)
            return;                     // a door's or a moving pushwall's floor is the player's
        if (stop == 0)
        {
            if (tile == 0)
                render.CellFloor = render.GroundStory;   // open all the way down
            return;
        }

        //
        // its blocks from the bottom up, from the player's floor: one it's solid with is a wall
        // (WallHits draws it), the others are seen from outside
        //
        int start = map.SpanStart[i];
        int stop2 = inmap && i < map.UpperSpanStop.Length ? map.UpperSpanStop[i] : 0;
        int start2 = stop2 != 0 ? map.UpperSpanStart[i] : 0;
        int blocks = stop2 != 0 ? 2 : 1;
        Span<int> firsts = [start - story, start2 - story], tops = [stop - story + 1, stop2 - story + 1];
        Span<bool> solid = [tile != 0 && start <= story && story <= stop, tile != 0 && stop2 != 0 && start2 <= story && story <= stop2];

        TextureAsset? texture = null;
        bool mirrored = false, textured = false;
        for (int k = 0; k < blocks; k++)
        {
            if (solid[k])
                continue;
            int n = render.CellBlocks++;
            render.CellFirst[n] = firsts[k];
            render.CellTop[n] = tops[k];
            render.CellOverhead[n] = firsts[k] > 0;
            render.CellUnderClip[n] = k > 0 ? tops[k - 1] : int.MinValue;

            // its own texture, else a wall's at the end of its run, else one beside it
            if (!textured)
            {
                texture = focal ? null : BlockFaceTexture(tilex, tiley, vertical, out mirrored);
                textured = true;
                if (texture != null)
                    render.CellLight = WallLight(edgex, edgey);
            }
            render.CellFace[n] = texture != null;
            if (texture != null)
            {
                render.CellTexture[n] = texture;
                render.CellOfs[n] = vertical
                    ? LintelColumn(edgey, (render.XTileStep == -1) != mirrored)
                    : LintelColumn(edgex, (render.YTileStep == 1) != mirrored);
            }
            if (render.CellOverhead[n])
            {
                (render.CellUnderside[n], render.CellUnderFlat[n]) = UndersideOf(tilex, tiley, texture);
                if (focal)
                    render.CellLight = lighting.LightAt(render.HitTile, render.ViewX, render.ViewY);
            }
        }

        //
        // its floors, from the top down: each block's top below the eyes, then the ground if the
        // lowest doesn't stand on it; the highest is the floor (run with its neighbours'), the
        // others only show through the gap under the block over them
        //
        render.CellFloor = int.MinValue;
        for (int k = blocks - 1; k >= 0; k--)
            if (!solid[k] && tops[k] <= 0)
                AddCellFloor(tops[k], k + 1 < blocks ? firsts[k + 1] : int.MinValue);
        if (firsts[0] > render.GroundStory)
            AddCellFloor(render.GroundStory, firsts[0]);
    }

    // A floor that shows on the tile being opened, under the block starting at story over (int.MinValue: none)
    void AddCellFloor(int floor, int over)
    {
        if (render.CellFloor == int.MinValue)
            render.CellFloor = floor;
        else if (render.CellLowFloors < render.CellLowFloor.Length)
        {
            render.CellLowFloor[render.CellLowFloors] = floor;
            render.CellLowClip[render.CellLowFloors++] = over;
        }
    }

    // The texture on a block's face seen going the way the trace is: its own (a wall's, or a floor
    // tile's flats sides), else the wall ending its run of arch tiles, else a wall beside it
    TextureAsset? BlockFaceTexture(int tilex, int tiley, bool vertical, out bool mirrored)
    {
        int dx = vertical ? 0 : 1, dy = vertical ? 1 : 0;
        var face = SeenFace(vertical);
        return wallTexture(tilex, tiley, face, out mirrored)
            ?? ArchEndTexture(tilex, tiley, -dx, -dy, face, out mirrored) ?? ArchEndTexture(tilex, tiley, dx, dy, face, out mirrored)
            ?? ArchEndTexture(tilex, tiley, -dy, -dx, face, out mirrored) ?? ArchEndTexture(tilex, tiley, dy, dx, face, out mirrored);
    }

    // An overhead block's underside: the tile's ceiling flat, else its own texture, else the run's
    (TextureAsset? Texture, bool Flat) UndersideOf(int tilex, int tiley, TextureAsset? face)
    {
        if (map.MapSpot(tilex, tiley, MapManager.FlatPlane) >> 8 != 0 && map.CeilingFlat[map.TileIndex(tilex, tiley)] is { } flat)
            return (flat, true);
        return (wallTexture(tilex, tiley, "north", out _) ?? ArchUndersideTexture(tilex, tiley) ?? face, false);
    }

    /// <summary>
    /// The trace leaves the tile it's crossing across the edge at (edgex, edgey): its block's face
    /// and underside, and its floor, are queued (a run of floors at one height as one), and the
    /// rows they, the player's floor there and an opaque ceiling over it cover close in
    /// </summary>
    internal void CloseTile(int edgex, int edgey)
    {
        if (!render.CellOpen)
            return;
        render.CellOpen = false;

        if (render.CellPlain)
        {
            // one more tile of a run of them
            if (!render.PlainOpen)
            {
                render.PlainOpen = true;
                (render.PlainEntryX, render.PlainEntryY, render.PlainFocal) = (render.CellEdgeX, render.CellEdgeY, render.CellFocal);
            }
            (render.PlainExitX, render.PlainExitY) = (edgex, edgey);
            return;
        }
        FlushPlain();

        short entry = render.CellEntry;
        short exit = HeightAt(edgex, edgey);
        if (exit == 0)
            exit = 1;

        // its blocks from the bottom up: an overhead one's underside shows over a higher one's
        for (int k = 0; k < render.CellBlocks; k++)
        {
            int first = render.CellFirst[k];
            int top = render.CellTop[k];
            if (render.OpaqueCeiling)
                top = Math.Min(top, render.CeilingStory);
            bool face = render.CellFace[k] && top > first;
            bool under = render.CellOverhead[k] && render.CellUnderside[k] != null && (!render.OpaqueCeiling || first < render.CeilingStory);
            if (!face && !under)
                continue;
            var texture = face ? render.CellTexture[k]! : render.NoTexture;

            // an underside over another block shows only above that one's top
            int clipbottom = render.OpenBottom;
            if (under && render.CellUnderClip[k] != int.MinValue)
                clipbottom = Math.Min(clipbottom, RowAt(entry, render.CellUnderClip[k]) - 1);
            QueuePost(entry, first, Math.Max(top, first), texture, render.CellOfs[k], texture, render.CellOfs[k],
                under ? exit : (short)0, render.CellUnderside[k], render.CellLight, render.CellUnderFlat[k], capstory: first, capup: false,
                cliptop: render.OpenTop, clipbottom: clipbottom);
            if (face)
                Occlude(RowAt(entry, top), RowAt(entry, first) - 1);
            if (under)
                Occlude(RowAt(entry, first), Math.Min(RowAt(exit, first) - 1, clipbottom));
        }

        // its floor: the player's (drawn with the level's flats), else a cap at its height. The
        // level's flats only fill rows nothing else painted, so the player's floor is a cap too
        // where rows under it are still open (a gap under a block): farther things seen through
        // the gap could paint its rows first
        if (render.CellFloor == 0 && RowAt(entry, 0) - 1 >= render.OpenBottom)
        {
            FlushRun();
            Occlude(RowAt(exit, 0), RowAt(entry, 0) - 1);
        }
        else if (render.CellFloor != int.MinValue)
            AddFloorRun(render.CellFloor, entry, exit);

        // floors under it, through the gaps at the tile's side: each shows only below the bottom
        // of the block over it
        for (int k = 0; k < render.CellLowFloors; k++)
        {
            int floor = render.CellLowFloor[k];
            int cliptop = Math.Max(render.OpenTop, RowAt(entry, render.CellLowClip[k]));
            if (cliptop > render.OpenBottom)
                continue;
            QueuePost(entry, floor, floor, render.NoTexture, 0, render.NoTexture, 0, exit, null, render.CellLight, true,
                capstory: floor, capup: true, cliptop: cliptop, clipbottom: render.OpenBottom);
            Occlude(Math.Max(RowAt(exit, floor), cliptop), RowAt(entry, floor) - 1);
        }

        // the ceiling over it covers from the top; with a sky, nothing farther shows above the
        // tallest block there is
        if (render.OpaqueCeiling)
            Occlude(RowAt(entry, render.CeilingStory), RowAt(exit, render.CeilingStory) - 1);
        else
            render.OpenTop = Math.Max(render.OpenTop, RowAt(exit, render.TallestStory));
    }

    // A run of plain tiles ends: the player's floor and the ceiling over them close in the open rows
    void FlushPlain()
    {
        if (!render.PlainOpen)
            return;
        render.PlainOpen = false;
        FlushRun();
        short entry = render.PlainFocal ? short.MaxValue : HeightAt(render.PlainEntryX, render.PlainEntryY);
        short exit = HeightAt(render.PlainExitX, render.PlainExitY);
        if (exit == 0)
            exit = 1;
        Occlude(RowAt(exit, 0), RowAt(entry, 0) - 1);
        if (render.OpaqueCeiling)
            Occlude(RowAt(entry, render.CeilingStory), RowAt(exit, render.CeilingStory) - 1);
        else
            render.OpenTop = Math.Max(render.OpenTop, RowAt(exit, render.TallestStory));
    }

    // A floor at story floor from where the trace entered a tile to where it left: one more tile of
    // the run of floors at that height, or the start of a run
    void AddFloorRun(int floor, short entry, short exit)
    {
        if (!(render.RunOpen && render.RunFloor == floor))
        {
            FlushRun();
            render.RunOpen = true;
            render.RunFloor = floor;
            render.RunEntry = entry;
            render.RunClipTop = render.OpenTop;
            render.RunClipBottom = render.OpenBottom;
        }
        render.RunExit = exit;
        Occlude(RowAt(exit, floor), RowAt(entry, floor) - 1);
    }

    // Queues the run of floors, if there is one: a cap with each tile's floor flat
    void FlushRun()
    {
        if (!render.RunOpen)
            return;
        render.RunOpen = false;
        QueuePost(render.RunEntry, render.RunFloor, render.RunFloor, render.NoTexture, 0, render.NoTexture, 0,
            render.RunExit, null, render.CellLight, true, capstory: render.RunFloor, capup: true,
            cliptop: render.RunClipTop, clipbottom: render.RunClipBottom, fromRun: true);
    }

    /// <summary>
    /// Whether the door the trace just met at its plane, its face texture <paramref name="face"/>,
    /// is seen through: a masked texture with no wall nearer in the column (one passed while
    /// looking for taller walls is drawn shut, as it'd show over the wall in front) and room to
    /// keep it
    /// </summary>
    internal bool SeesThrough(TextureAsset face) =>
        face.Masked && !render.ColumnHit && !render.PastWall
        && (uint)render.PixX < (uint)render.SeeThroughCount.Length && render.SeeThroughCount[render.PixX] < RenderState.MaxSeeThrough;

    /// <summary>
    /// Keeps the see-through door the trace just met, as ScalePost would queue it, for
    /// SeeThroughPosts to draw among the sprites; drawn: how many stories, if not all HitStories
    /// </summary>
    internal void QueueSeeThrough(int drawn = 0)
    {
        int at = render.PixX * RenderState.MaxSeeThrough + render.SeeThroughCount[render.PixX]++;
        render.SeeThroughHeight[at] = render.PostHeight;
        render.SeeThroughStories[at] = (byte)(drawn > 0 ? drawn : render.HitStories);
        render.SeeThroughLower[at] = render.PostSource;
        render.SeeThroughLowerOfs[at] = render.PostOfs;
        render.SeeThroughUpper[at] = render.UpperSource ?? render.PostSource;
        render.SeeThroughUpperOfs[at] = render.UpperSource != null ? render.UpperOfs : render.PostOfs;
        render.SeeThroughLight[at] = WallLight(render.XIntercept, render.YIntercept);
        render.AnySeeThrough = true;
        render.UpperSource = null;
    }

    /// <summary>
    /// Queues a post: stories firststory up to (not including) stories, counted from the
    /// player's floor, and a cap (underside: the height where the trace leaves, 0 for none) at the
    /// bottom of story capstory, a floor (capup) or an underside. It's clipped to the rows open
    /// now (or cliptop to clipbottom), after any run of floors waiting is queued before it.
    /// </summary>
    internal void QueuePost(short height, int firststory, int stories, TextureAsset lower, int lowerofs, TextureAsset upper, int upperofs,
        short underside = 0, TextureAsset? undertexture = null, byte[]? light = null, bool underflat = false,
        TextureAsset? patch = null, int patchofs = 0, int patchstory = 0, int scrollrow = 0, int cliptop = int.MinValue, int clipbottom = int.MaxValue,
        int capstory = 1, bool capup = false, bool fromRun = false)
    {
        if (!fromRun)
        {
            FlushPlain();
            FlushRun();
        }
        if (render.PostCount == RenderState.MaxPosts)
            return;
        if (cliptop == int.MinValue)
            (cliptop, clipbottom) = (render.OpenTop, render.OpenBottom);
        render.PostClipTop[render.PostCount] = cliptop;
        render.PostClipBottom[render.PostCount] = clipbottom;
        render.PostCapStory[render.PostCount] = (sbyte)capstory;
        render.PostCapUp[render.PostCount] = capup;
        render.PostScrollRow[render.PostCount] = scrollrow;
        render.PostPatch[render.PostCount] = patch;
        render.PostPatchOfs[render.PostCount] = patchofs;
        render.PostPatchStory[render.PostCount] = (byte)patchstory;
        render.PostLight[render.PostCount] = light ?? WallLight(render.XIntercept, render.YIntercept);
        render.PostUnderside[render.PostCount] = underside;
        render.PostUnderTexture[render.PostCount] = undertexture;
        render.PostUnderFlat[render.PostCount] = underflat;
        render.PostHeights[render.PostCount] = height;
        render.PostFirstStory[render.PostCount] = (sbyte)firststory;
        render.PostStories[render.PostCount] = (sbyte)stories;
        render.PostLower[render.PostCount] = lower;
        render.PostLowerOfs[render.PostCount] = lowerofs;
        render.PostUpper[render.PostCount] = upper;
        render.PostUpperOfs[render.PostCount] = upperofs;
        render.PostCount++;
    }

    // Draws the column's queued posts, farthest first
    internal void DrawPosts()
    {
        if (points.On)
            points.NoteOccluders();            // what hides rain, snow and particles in this column (PointDrawing.cs)

        if (render.PixX < render.CoverCount.Length)
            render.CoverCount[render.PixX] = 0;

        while (render.PostCount > 0)
        {
            render.PostCount--;
            int cliptop = render.PostClipTop[render.PostCount], clipbottom = render.PostClipBottom[render.PostCount];
            DrawPost(render.PixX, render.PostHeights[render.PostCount] >> 3, render.PostFirstStory[render.PostCount], render.PostStories[render.PostCount],
                render.PostLower[render.PostCount], render.PostLowerOfs[render.PostCount], render.PostUpper[render.PostCount], render.PostUpperOfs[render.PostCount],
                render.PostLight[render.PostCount], lighting.ShadeOffsetForHeight(render.PostHeights[render.PostCount]), render.PostScrollRow[render.PostCount],
                cliptop, clipbottom);
            if (render.PostPatch[render.PostCount] is { } patch)
                DrawPatch(render.PixX, render.PostHeights[render.PostCount] >> 3, render.PostFirstStory[render.PostCount], render.PostStories[render.PostCount], patch,
                    render.PostPatchOfs[render.PostCount], render.PostPatchStory[render.PostCount], render.PostLight[render.PostCount],
                    lighting.ShadeOffsetForHeight(render.PostHeights[render.PostCount]), cliptop, clipbottom);
            if (render.PostUnderside[render.PostCount] != 0)
                DrawUnderside(render.PixX, PostHalf(render.PostHeights[render.PostCount]), PostHalf(render.PostUnderside[render.PostCount]),
                    render.PostUnderTexture[render.PostCount], render.PostUnderFlat[render.PostCount], render.PostCapStory[render.PostCount],
                    render.PostCapUp[render.PostCount], cliptop, clipbottom);
        }
    }

    /// <summary>
    /// Draws a cap: an underside at the bottom of story <paramref name="story"/> (from the
    /// player's floor), or with <paramref name="up"/> a floor there, between where the trace
    /// enters it (half a story is entryhalf pixels there) and where it leaves (exithalf), in rows
    /// cliptop to clipbottom only. It's a flat surface, so each row is textured from where the
    /// view ray meets it, from the texture's bottom story laid flat: one tile of it per tile. A
    /// flat (flat) is sampled as DrawFlats does, its see-through pixels the ceiling (or floor)
    /// color; a floor with no texture takes each tile's floor flat. With neither, it's the color.
    /// </summary>
    internal void DrawUnderside(int x, int entryhalf, int exithalf, TextureAsset? texture, bool flat, int story = 1, bool up = false,
        int cliptop = 0, int clipbottom = int.MaxValue)
    {
        // where DrawPost puts the bottom of that story: an underside from the near edge back, a
        // floor from the far edge down to the near one
        int from = up ? RowAtHalf(exithalf, story) : RowAtHalf(entryhalf, story);
        int to = (up ? RowAtHalf(entryhalf, story) : RowAtHalf(exithalf, story)) - 1;
        from = Math.Max(from, Math.Max(cliptop, 0));
        to = Math.Min(to, Math.Min(clipbottom, render.ViewHeight - 1));
        if (from > to)
            return;
        byte color = up ? skyAndFlats.FloorColorNow : skyAndFlats.CeilingColor;
        int pitch = (int)video.BufferPitch;
        render.Cover(x, from, to);

        unsafe
        {
            byte* dest = (byte*)render.VbufPtr + render.ScreenOfs + x;
            double numerator = render.HeightNumerator * 32.0 * skyAndFlats.EyeHalves(story * GameConstants.TextureSize);

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

            bool perTile = up && texture == null;   // a floor: each tile's own floor flat
            for (int y = from; y <= to; y++)
            {
                double nx = numerator / Math.Abs(render.CenterY - y - 0.5);
                long px = (long)(render.ViewX + nx * stepx), py = (long)(render.ViewY + nx * stepy);
                int tile = map.TileIndexOrNone((int)(px >> MapConstants.TileShift), (int)(py >> MapConstants.TileShift));
                var light = lighting.LightAt(tile, px, py);
                int shadeofs = lighting.ShadeOffset((long)nx);
                if (perTile)
                {
                    var floor = tile >= 0 ? map.FloorFlat[tile] : null;
                    data = floor?.RawData;
                    (width, height) = (floor?.Width ?? 0, floor?.Height ?? 0);
                    flat = true;
                }
                if (data == null)
                {
                    dest[y * pitch] = light[shadeofs + color];
                    continue;
                }

                int u = (int)(px >> (MapConstants.TileShift - GameConstants.TextureShift)) & (GameConstants.TextureSize - 1);
                int v = (int)(py >> (MapConstants.TileShift - GameConstants.TextureShift)) & (GameConstants.TextureSize - 1);
                int row = flat || height < GameConstants.TextureSize ? v * height / GameConstants.TextureSize : height - GameConstants.TextureSize + v;
                byte pixel = data[u * width / GameConstants.TextureSize * height + row];
                if (flat && pixel == SkyAndFlats.FlatMask)
                    pixel = color;
                dest[y * pitch] = light[shadeofs + pixel];
            }
        }
    }

    /*
    ====================
    =
    = Arches
    =
    = An open tile whose block (plane 3) starts above the player's story is an arch, walked
    = under: OpenTile and CloseTile queue its face and underside. The textures for one come from
    = here: its own, else the wall at the end of its run of arch tiles. A door's tile taller than
    = the player's story is capped the same way, its lintel flush with the wall the door is set
    = into (OpenDoorArch, CloseArch).
    =
    ====================
    */


    internal bool IsArch(int tilex, int tiley) => map.IsOverhead(tilex, tiley);

    /// <summary>
    /// The trace enters a door tile at (TileX, TileY) as OpenArch enters an arch. Above the first
    /// story, a door is capped like an arch: faces flush with the wall the door is set into (the
    /// lintel) and an underside over the doorway, up to where the trace leaves the tile or meets a
    /// shut door (WallHits.HitVertDoor). doorVertical: the door runs north to south.
    /// </summary>
    internal void OpenDoorArch(int tilex, int tiley, bool doorVertical, int edgex, int edgey, bool vertical)
    {
        int stories = map.WallStories(tilex, tiley) - map.Story + 1;    // its top, from the player's floor
        if (stories <= 1)
            return;

        // the wall on either side in line with the door
        int dx = doorVertical ? 0 : 1, dy = doorVertical ? 1 : 0;
        var face = SeenFace(vertical);
        var texture = wallTexture(tilex - dx, tiley - dy, face, out bool mirrored) ?? wallTexture(tilex + dx, tiley + dy, face, out mirrored);
        if (texture == null)
            return;                     // set into nothing: a shut door carries on up (WallHits.SetLintelPost)

        StartArch(tilex, tiley, 1, stories, texture, mirrored, DoorArchUndersideTexture(tilex, tiley, doorVertical), edgex, edgey, vertical);
    }

    /// <summary>
    /// The texture under a door's lintel, the same whichever way the trace enters: the face of the
    /// wall beside the door that looks into the doorway, as the wall above its jamb shows it
    /// </summary>
    internal TextureAsset? DoorArchUndersideTexture(int tilex, int tiley, bool doorVertical) => doorVertical
        ? wallTexture(tilex, tiley - 1, "south", out _) ?? wallTexture(tilex, tiley + 1, "north", out _)
        : wallTexture(tilex - 1, tiley, "east", out _) ?? wallTexture(tilex + 1, tiley, "west", out _);

    // Opens an arch on (TileX, TileY), from first stories above the floor up to stories, its face
    // texture entered across (edgex, edgey)
    void StartArch(int tilex, int tiley, int first, int stories, TextureAsset texture, bool mirrored, TextureAsset? underside,
        int edgex, int edgey, bool vertical)
    {
        render.ArchTexture = texture;
        render.ArchOpen = true;
        render.ArchLight = WallLight(edgex, edgey);
        render.ArchUnderside = underside ?? texture;
        UseArchCeilingFlat(tilex, tiley);
        render.ArchFirst = first;
        render.ArchStories = stories;
        render.ArchHeight = HeightAt(edgex, edgey);
        render.ArchOfs = vertical
            ? LintelColumn(edgey, (render.XTileStep == -1) != mirrored)
            : LintelColumn(edgex, (render.YTileStep == 1) != mirrored);
    }

    // The texture on face of the wall that ends the run of arch tiles from (TileX, TileY) going (dx, dy)
    internal TextureAsset? ArchEndTexture(int tilex, int tiley, int dx, int dy, string face, out bool mirrored)
    {
        mirrored = false;
        do
        {
            tilex += dx;
            tiley += dy;
            if (tilex < 0 || tiley < 0 || tilex >= map.MapWidth || tiley >= map.MapHeight)
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
        render.ArchUnderFlat = false;
        if (map.MapSpot(tilex, tiley, MapManager.FlatPlane) >> 8 == 0)
            return;
        var flat = map.CeilingFlat[map.TileIndex(tilex, tiley)];
        if (flat == null)
            return;
        render.ArchUnderside = flat;
        render.ArchUnderFlat = true;
    }

    // The view starts in a door's tile, under its lintel (OpenDoorArch)
    internal void OpenFocalDoorArch(bool doorVertical)
    {
        int stories = map.WallStories(render.FocalTx, render.FocalTy) - map.Story + 1;
        if (stories > 1)
            StartFocalArch(1, stories, DoorArchUndersideTexture(render.FocalTx, render.FocalTy, doorVertical));
    }

    void StartFocalArch(int first, int stories, TextureAsset? underside)
    {
        render.ArchFirst = first;
        render.ArchUnderside = underside;
        if (render.ArchUnderside == null)
            return;                     // as OpenArch: nothing to texture it from
        UseArchCeilingFlat(render.FocalTx, render.FocalTy);
        render.ArchTexture = render.NoTexture;
        render.ArchOpen = true;
        render.ArchLight = lighting.LightAt(render.HitTile, render.ViewX, render.ViewY);
        render.ArchStories = stories;
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
        int first = render.ArchFirst;
        int top = render.OpaqueCeiling ? Math.Min(render.ArchStories, render.CeilingStory) : render.ArchStories;
        if (top <= first)
            return;                     // all of it above the ceiling
        QueuePost(render.ArchHeight, first, top, render.ArchTexture!, render.ArchOfs, render.ArchTexture!, render.ArchOfs, exitheight,
            render.ArchUnderside, render.ArchLight, render.ArchUnderFlat, capstory: first);
        Occlude(RowAt(render.ArchHeight, top), RowAt(render.ArchHeight, first) - 1);
        Occlude(RowAt(render.ArchHeight, first), RowAt(exitheight, first) - 1);
    }

    // The height (as WallHeight) of a wall at (x, y)
    internal short HeightAt(int x, int y)
    {
        // a tile's edge is measured as the trace leaves one tile and again as it enters the next
        if (x == lastheightx && y == lastheighty && render.ViewX == lastviewx && render.ViewY == lastviewy && render.ViewCos == lastviewcos)
            return lastheight;

        int nx = MathUtils.FixedMul(x - render.ViewX, render.ViewCos) - MathUtils.FixedMul(y - render.ViewY, render.ViewSin);
        if (nx < GameConstants.MinDist)
            nx = (int)GameConstants.MinDist;
        short height = (short)(render.HeightNumerator / (nx >> 8));     // as CalcHeight
        (lastheightx, lastheighty, lastviewx, lastviewy, lastviewcos, lastheight) = (x, y, render.ViewX, render.ViewY, render.ViewCos, height);
        return height;
    }

    int lastheightx = int.MinValue, lastheighty, lastviewx, lastviewy, lastviewcos;
    short lastheight;

    // Where the trace meets the vertical edge it enters XTile by, and the horizontal one it enters YTile by
    internal int VertEdge => (render.XTileStep == 1 ? render.XTile : render.XTile + 1) << MapConstants.TileShift;
    internal int HorizEdge => (render.YTileStep == 1 ? render.YTile : render.YTile + 1) << MapConstants.TileShift;

    /// <summary>
    /// Whether the trace should go on past the wall it just hit, on the tile (TileX, TileY): only
    /// while rows are left that something farther could show in (above it, or under it).
    /// </summary>
    internal bool TracePastWall(int tilex, int tiley)
    {
        if (!render.ColumnOpen || render.PostCount >= RenderState.MaxPosts - 1)
            return false;
        // a trace never leaves the map through its edge
        return tilex > 0 && tiley > 0 && tilex < map.MapWidth - 1 && tiley < map.MapHeight - 1;
    }

    //
    // the trace's position, kept from before it enters a tile, to carry on past a wall it hits there
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
    /// Draws one screen column of a wall: stories firststory up to (not including) stories,
    /// counted from the player's floor (story 0, the player's; below it negative). The player's
    /// story is drawn from lower (a door's jamb), the others from upper. A texture covers a story
    /// per 64 texels of its height, from the ground up, and repeats above that. half is half a
    /// story's height in pixels; lowerofs and UpperOfs are the column to draw, times 64, as the
    /// trace finds it across a tile. Each texel is remapped through shade from shadeofs (a light
    /// row and fade step, see WL_SHADE.cs). scrollrow moves the texels that many down (a
    /// scroller's, Rendering/Scrollers.cs). Only rows cliptop to clipbottom are drawn.
    /// </summary>
    internal void DrawPost(int x, int half, int firststory, int stories, TextureAsset lower, int lowerofs, TextureAsset upper, int upperofs,
        byte[] shade, int shadeofs, int scrollrow = 0, int cliptop = 0, int clipbottom = int.MaxValue)
    {
        if (half <= 0) half = 100;

        //
        // row k up from the floor line shows texel k * 32 / half up the wall; step that
        // along as whole texels (t) plus a remainder, so it matches the old ScalePost exactly
        //
        int floor = FloorRow(half);
        int bottom = floor - 1 - firststory * 2 * half;
        int top = Math.Max(floor - stories * 2 * half, cliptop);
        int y = Math.Min(Math.Min(bottom, render.ViewHeight - 1), clipbottom);
        if (top < 0) top = 0;
        if (y < top) return;
        render.Cover(x, top, y);

        long num = (long)(bottom - y) * (GameConstants.TextureSize / 2);
        long t = num / half + firststory * GameConstants.TextureSize;   // texels up from the player's floor
        long rem = num % half;
        long zbase = render.ZBase + scrollrow;                          // where the texture repeats from: the ground
        int pitch = (int)video.BufferPitch;

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
                    long s = t + zbase;         // the texel up the wall (that's moved down to here)
                    col = shade[shadeofs + ((ulong)t < GameConstants.TextureSize
                        ? lowerdata[lowercolumn + lowerheight - 1 - Wrap(s, lowerheight)]
                        : upperdata[uppercolumn + upperheight - 1 - Wrap(s, upperheight)])];
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

    const byte PatchMask = 0xff;        // a wall patch's see-through pixels, as a flat's

    // s wrapped into 0 to size - 1, below 0 too (a texel under the floor)
    static int Wrap(long s, int size)
    {
        int r = (int)(s % size);
        return r < 0 ? r + size : r;
    }

    /// <summary>
    /// Draws one screen column of a wall patch over the wall post just drawn (DrawPost), its
    /// texels laid out the same way: from the floor of story <paramref name="story"/> (1 up from
    /// the ground) for as many stories as the patch is tall, or (story 0) on every story of the
    /// wall from firststory, repeating up from the ground as the wall's texture does. Its
    /// see-through pixels leave the wall showing. Only rows cliptop to clipbottom are drawn, as DrawPost.
    /// </summary>
    internal void DrawPatch(int x, int half, int firststory, int stories, TextureAsset patch, int ofs, int story, byte[] shade, int shadeofs,
        int cliptop = 0, int clipbottom = int.MaxValue)
    {
        if (half <= 0) half = 100;

        int height = patch.Height;
        int first = story == 0 ? firststory : story - map.Story;
        int last = story == 0 ? stories : Math.Min(stories, first + (height + GameConstants.TextureSize - 1) / GameConstants.TextureSize);
        if (first >= last || first < firststory)
            return;                     // on a story the wall doesn't reach

        int floor = FloorRow(half);
        int bottom = floor - 1 - first * 2 * half;
        int top = Math.Max(floor - last * 2 * half, cliptop);
        int y = Math.Min(Math.Min(bottom, render.ViewHeight - 1), clipbottom);
        if (top < 0) top = 0;
        if (y < top) return;

        // texels up from the patch's bottom, stepped as DrawPost steps the wall's; on every story
        // it repeats from the floor up, so under the floor it's in step with the stories above
        long num = (long)(bottom - y) * (GameConstants.TextureSize / 2);
        long t = num / half;
        long rem = num % half;
        long phase = story == 0 ? (long)first * GameConstants.TextureSize + render.ZBase : 0;
        int pitch = (int)video.BufferPitch;

        byte[] data = patch.RawData;
        int column = TextureColumn(patch, ofs);

        unsafe
        {
            byte* dest = (byte*)render.VbufPtr + render.ScreenOfs + x;
            for (; y >= top; y--)
            {
                long row = story == 0 ? Wrap(t + phase, height) : t;
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
    = The height (as WallHeight) of the wall the trace just hit, from how far along the view its
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

    // The face of a wall the trace sees, going the way it is
    internal string SeenFace(bool vertical) => vertical
        ? render.XTileStep == 1 ? "west" : "east"
        : render.YTileStep == 1 ? "north" : "south";

    internal static int LintelColumn(int along, bool flip)
    {
        int texture = (along >> GameConstants.Fixed2TexShift) & GameConstants.TextureMask;
        return flip ? GameConstants.TextureMask - texture : texture;
    }
}