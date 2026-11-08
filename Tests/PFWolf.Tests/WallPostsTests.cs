using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Tests;

public class WallPostsTests
{
    private const int Tile = 1 << MapConstants.TileShift;

    private MapManager _map = null!;
    private RenderState _render = null!;
    private WallPosts _posts = null!;

    [SetUp]
    public void MakeView()
    {
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession(), null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        // Looking east from the middle of tile (10, 10), the horizon at row 80
        _render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.NoShade)
        {
            ViewX = 10 * Tile + Tile / 2, ViewY = 10 * Tile + Tile / 2, ViewCos = 0x10000, ViewSin = 0,
            HeightNumerator = 0x100000, CenterY = 80, ViewEyeZ = 32,
        };
        _posts = new WallPosts(null!, _map, null!, null!, _render, null!, new GameSession(),
            (int _, int _, string _, out bool mirrored) => { mirrored = false; return null; });
    }

    [Test]
    public void A_Wall_Twice_As_Far_Along_The_View_Is_Half_As_Tall()
    {
        // Act
        short near = _posts.HeightAt(12 * Tile + Tile / 2, 10 * Tile + Tile / 2);
        short far = _posts.HeightAt(14 * Tile + Tile / 2, 10 * Tile + Tile / 2);

        // Assert
        Assert.That(near, Is.EqualTo(2 * far));
    }

    [Test]
    public void Height_Is_Along_The_View_So_Off_To_The_Side_Is_No_Shorter()
    {
        Assert.That(_posts.HeightAt(12 * Tile + Tile / 2, 14 * Tile), Is.EqualTo(_posts.HeightAt(12 * Tile + Tile / 2, 10 * Tile + Tile / 2)));
    }

    [Test]
    public void Measuring_A_Spot_Leaves_The_Trace_Where_It_Was()
    {
        // Arrange
        (_render.XIntercept, _render.YIntercept) = (123, 456);

        // Act
        _posts.HeightAt(12 * Tile, 10 * Tile);

        // Assert
        Assert.That((_render.XIntercept, _render.YIntercept), Is.EqualTo((123, 456)));
    }

    [Test]
    public void Too_Close_Is_As_Tall_As_At_The_Nearest_Distance()
    {
        Assert.That(_posts.HeightAt(_render.ViewX, _render.ViewY), Is.EqualTo(_posts.HeightAt(_render.ViewX + (int)GameConstants.MinDist, _render.ViewY)));
    }

    [TestCase(32, 32, 80 + 32)]     // eyes half a story up: the floor half a story's pixels below the horizon
    [TestCase(10, 32, 80 + 10)]
    [TestCase(32, 0, 80)]           // eyes on the floor: the floor is the horizon
    public void The_Floor_Row_Goes_With_Eye_Height(int half, int eyez, int row)
    {
        _render.ViewEyeZ = eyez;
        Assert.That(_posts.FloorRow(half), Is.EqualTo(row));
    }

    [TestCase(true, 1, 1, "west")]          // heading east, the trace meets a wall's west face
    [TestCase(true, -1, 1, "east")]
    [TestCase(false, 1, 1, "north")]        // heading south, a horizontal wall's north face
    [TestCase(false, 1, -1, "south")]
    public void The_Face_Seen_Is_The_One_Facing_The_Trace(bool vertical, int xstep, int ystep, string face)
    {
        (_render.XTileStep, _render.YTileStep) = ((short)xstep, (short)ystep);
        Assert.That(_posts.SeenFace(vertical), Is.EqualTo(face));
    }

    [TestCase(0, false, 0)]
    [TestCase(0x8000, false, 32 * 64)]      // half way across a tile: column 32 (times 64, as post offsets are)
    [TestCase(0x8000, true, 31 * 64)]       // mirrored
    [TestCase(Tile + 0x4000, false, 16 * 64)]   // only the part within its tile counts
    public void A_Lintels_Column_Is_Where_The_Trace_Meets_The_Door(int along, bool flip, int column)
    {
        Assert.That(WallPosts.LintelColumn(along, flip), Is.EqualTo(column));
    }

    [Test]
    public void The_Edge_Entered_Is_The_Near_Side_Of_The_Tile_The_Way_The_Trace_Goes()
    {
        // Arrange: heading east and south into tile (12, 14)
        (_render.XTile, _render.YTile, _render.XTileStep, _render.YTileStep) = (12, 14, 1, 1);

        // Act / Assert
        Assert.That((_posts.VertEdge, _posts.HorizEdge), Is.EqualTo((12 * Tile, 14 * Tile)));
        (_render.XTileStep, _render.YTileStep) = (-1, -1);      // heading west and north: their far sides
        Assert.That((_posts.VertEdge, _posts.HorizEdge), Is.EqualTo((13 * Tile, 15 * Tile)));
    }

    [Test]
    public void The_Trace_Goes_On_From_Where_It_Was_Saved()
    {
        // Arrange
        (_render.XTile, _render.YTile, _render.XIntercept, _render.YIntercept, _render.TexDelta) = (5, 6, 700, 800, 9);
        _posts.SaveTrace();
        (_render.XTile, _render.YTile, _render.XIntercept, _render.YIntercept, _render.TexDelta) = (1, 1, 1, 1, 1);

        // Act
        _posts.RestoreTrace();

        // Assert
        Assert.That((_render.XTile, _render.YTile, _render.XIntercept, _render.YIntercept, _render.TexDelta), Is.EqualTo(((short)5, (short)6, 700, 800, (ushort)9)));
        Assert.That(_render.PastWall, Is.True);
    }

    [Test]
    public void A_Queued_Post_Goes_At_The_End_Of_The_Columns_Queue()
    {
        // Arrange
        var brick = new TextureAsset { RawData = new byte[64 * 64] };
        _render.PostCount = 1;

        // Act
        _posts.QueuePost(100, 0, 2, brick, 64, brick, 128, light: Lighting.NoShade, patchofs: 5, patchstory: 1);

        // Assert
        Assert.That(_render.PostCount, Is.EqualTo(2));
        Assert.That((_render.PostHeights[1], _render.PostStories[1], _render.PostLowerOfs[1], _render.PostUpperOfs[1]), Is.EqualTo(((short)100, (sbyte)2, 64, 128)));
        Assert.That((_render.PostPatchOfs[1], _render.PostPatchStory[1]), Is.EqualTo((5, (byte)1)));
    }

    [TestCase(0, 0)]                    // the player's floor: the floor row
    [TestCase(1, -1)]                   // a story up: a whole story's pixels higher
    [TestCase(-2, 2)]                   // two down
    public void A_Storys_Row_Is_Measured_From_The_Floor_Row(int story, int stories)
    {
        short height = 160;             // half a story is 20 pixels
        Assert.That(_posts.RowAt(height, story), Is.EqualTo(_posts.FloorRow(20) + stories * 40));
    }

    [Test]
    public void Rows_Covered_From_The_Top_Or_Bottom_Close_In_And_Those_In_The_Middle_Dont()
    {
        // Arrange: a 160-row view, all open
        _render.ViewHeight = 160;
        _posts.StartColumn();

        // Act / Assert
        _posts.Occlude(-5, 20);
        Assert.That((_render.OpenTop, _render.OpenBottom), Is.EqualTo((21, 159)));
        _posts.Occlude(150, 200);
        Assert.That((_render.OpenTop, _render.OpenBottom), Is.EqualTo((21, 149)));
        _posts.Occlude(60, 70);         // a floating block: rows in the middle
        Assert.That((_render.OpenTop, _render.OpenBottom), Is.EqualTo((21, 149)));
        _posts.Occlude(10, 150);        // everything left
        Assert.That(_render.ColumnOpen, Is.False);
    }

    [Test]
    public void A_Walls_Stories_Are_Counted_From_The_Players_Floor()
    {
        _map.LevelWallStories = 8;
        _map.Story = 3;
        _posts.SetHitBlock(11, 10);
        Assert.That((_render.HitFirst, _render.HitStories), Is.EqualTo((-2, 6)));     // stories 1 to 8, from story 3

        _posts.SetHitBlock(11, 10, door: true);
        Assert.That(_render.HitFirst, Is.Zero);                                         // a door is on the player's story
    }

    [Test]
    public void A_Door_Is_A_Story_Tall_When_The_Walls_Dont_Reach_The_Players_Story()
    {
        _map.LevelWallStories = 1;
        _map.Story = 3;
        _posts.SetHitBlock(11, 10, door: true);
        Assert.That((_render.HitFirst, _render.HitStories), Is.EqualTo((0, 1)));
    }

    [Test]
    public void Each_Rows_Depth_Starts_As_The_Floor_Or_Ceiling_Would_Put_It()
    {
        // Arrange: a 160-row view, eyes half a story up, an opaque ceiling a story over the floor
        (_render.ViewWidth, _render.ViewHeight, _render.DepthOn) = (4, 160, true);
        (_render.CeilingStory, _render.OpaqueCeiling) = (1, true);

        // Act
        _posts.StartDepth();

        // Assert: a row the floor's on for something half pixels to half a story has its height
        // (8 a half-story pixel); the ceiling's rows likewise; nearer rows are nearer
        int half = 20;
        Assert.That(_render.DepthRows[_posts.FloorRow(half)], Is.EqualTo(half * 8).Within(8));
        Assert.That(_render.DepthRows[_posts.RowAt((short)(half * 8), 1)], Is.EqualTo(half * 8).Within(8));
        Assert.That(_render.DepthRows[150], Is.GreaterThan(_render.DepthRows[_posts.FloorRow(half)]));

        // with a sky there's no ceiling: the rows over the horizon are far off
        _render.OpaqueCeiling = false;
        _posts.StartDepth();
        Assert.That(_render.DepthRows[10], Is.Zero);
    }

    [Test]
    public void A_Moving_Pushwall_Is_Hit_At_Its_Pieces_Stories()
    {
        _map.Story = 2;
        _posts.SetHitSpan((2, 3));
        Assert.That((_render.HitFirst, _render.HitStories), Is.EqualTo((0, 2)));
    }

    // A frame on story 3 of a level with a sky: the ground two stories down; a smaller view, so
    // the tiles' rows are on screen
    private void OnStory3()
    {
        _map.Story = 3;
        _map.LevelWallStories = 3;
        _render.ViewHeight = 160;
        _render.HeightNumerator = 0x10000;
        (_render.GroundStory, _render.CeilingStory, _render.TallestStory, _render.OpaqueCeiling) = (-2, 1, 1, false);
        _posts.StartColumn();
    }

    [Test]
    public void The_Ground_Under_Open_Tiles_Below_The_Player_Is_One_Cap_Across_Them()
    {
        // Arrange
        OnStory3();

        // Act: across two open tiles east of the view's
        _posts.OpenTile(11, 10, 11 * Tile, 10 * Tile + Tile / 2, vertical: true);
        _posts.CloseTile(12 * Tile, 10 * Tile + Tile / 2);
        _posts.OpenTile(12, 10, 12 * Tile, 10 * Tile + Tile / 2, vertical: true);
        _posts.CloseTile(13 * Tile, 10 * Tile + Tile / 2);
        _posts.FinishColumn();

        // Assert: a floor cap two stories down, from the first's near edge to the second's far one,
        // drawn in the rows that were open
        Assert.That(_render.PostCount, Is.EqualTo(1));
        Assert.That((_render.PostCapUp[0], _render.PostCapStory[0]), Is.EqualTo((true, (sbyte)-2)));
        Assert.That(_render.PostHeights[0], Is.EqualTo(_posts.HeightAt(11 * Tile, 10 * Tile + Tile / 2)));
        Assert.That(_render.PostUnderside[0], Is.EqualTo(_posts.HeightAt(13 * Tile, 10 * Tile + Tile / 2)));
        Assert.That((_render.PostClipTop[0], _render.PostClipBottom[0]), Is.EqualTo((0, 159)));
        Assert.That(_render.OpenBottom, Is.EqualTo(_posts.RowAt(_render.PostUnderside[0], -2) - 1));
    }

    [Test]
    public void Open_Tiles_At_The_Players_Floor_Queue_Nothing_But_Cover_The_Floor_Rows()
    {
        // Arrange: story 1, so the ground is the player's floor; a smaller view, so the rows are on screen
        _render.ViewHeight = 160;
        _render.HeightNumerator = 0x10000;
        (_render.GroundStory, _render.CeilingStory, _render.TallestStory, _render.OpaqueCeiling) = (0, 1, 1, true);
        _posts.StartColumn();

        // Act: the view's own tile, then the next
        _posts.OpenTile(10, 10, 0, 0, vertical: false, focal: true);
        _posts.CloseTile(11 * Tile, 10 * Tile + Tile / 2);
        _posts.OpenTile(11, 10, 11 * Tile, 10 * Tile + Tile / 2, vertical: true);
        _posts.CloseTile(12 * Tile, 10 * Tile + Tile / 2);
        _posts.FinishColumn();

        // Assert: the level's flats draw it later; the rows it and the ceiling over it cover close in
        short exit = _posts.HeightAt(12 * Tile, 10 * Tile + Tile / 2);
        Assert.That(_render.PostCount, Is.Zero);
        Assert.That(_render.OpenBottom, Is.EqualTo(_posts.RowAt(exit, 0) - 1));
        Assert.That(_render.OpenTop, Is.EqualTo(_posts.RowAt(exit, 1)));
    }

    [Test]
    public void Whats_Queued_Past_A_Nearer_Floor_Is_Clipped_Above_It()
    {
        // Arrange: on story 3, rows 120 down covered by a nearer floor
        OnStory3();
        _posts.Occlude(120, 159);

        // Act
        _posts.QueuePost(100, -2, 1, _render.NoTexture, 0, _render.NoTexture, 0, light: Lighting.NoShade);

        // Assert: it's only drawn in the rows still open
        Assert.That((_render.PostClipTop[0], _render.PostClipBottom[0]), Is.EqualTo((0, 119)));
    }

    [Test]
    public void A_Floor_Block_Below_The_Player_Has_Its_Top_Drawn_At_Its_Height()
    {
        // Arrange: on story 3, a floor block from story 1 to 1 east of the view's tile
        OnStory3();
        _map.SpanStart = new byte[MapConstants.ClassicMapArea];
        _map.SpanStop = new byte[MapConstants.ClassicMapArea];
        (_map.SpanStart[_map.TileIndex(11, 10)], _map.SpanStop[_map.TileIndex(11, 10)]) = (1, 1);

        // Act
        _posts.OpenTile(11, 10, 11 * Tile, 10 * Tile + Tile / 2, vertical: true);
        _posts.CloseTile(12 * Tile, 10 * Tile + Tile / 2);
        _posts.FinishColumn();

        // Assert: its top is a story below the player's floor
        Assert.That(_render.PostCount, Is.EqualTo(1));
        Assert.That((_render.PostCapUp[0], _render.PostCapStory[0]), Is.EqualTo((true, (sbyte)-1)));
    }

    // Gives tile (11, 10) blocks for OpenTile: a second stop of 0 for one
    private void Blocks(int start, int stop, int start2 = 0, int stop2 = 0)
    {
        _map.SpanStart = new byte[MapConstants.ClassicMapArea];
        _map.SpanStop = new byte[MapConstants.ClassicMapArea];
        _map.UpperSpanStart = new byte[MapConstants.ClassicMapArea];
        _map.UpperSpanStop = new byte[MapConstants.ClassicMapArea];
        int i = _map.TileIndex(11, 10);
        (_map.SpanStart[i], _map.SpanStop[i], _map.UpperSpanStart[i], _map.UpperSpanStop[i]) = ((byte)start, (byte)stop, (byte)start2, (byte)stop2);
    }

    private void CrossTile11()
    {
        _posts.OpenTile(11, 10, 11 * Tile, 10 * Tile + Tile / 2, vertical: true);
        _posts.CloseTile(12 * Tile, 10 * Tile + Tile / 2);
        _posts.FinishColumn();
    }

    [Test]
    public void The_Ground_Shows_Under_A_Floor_Block_That_Doesnt_Stand_On_It()
    {
        // Arrange: on story 4, a floor block on story 2 only
        OnStory3();
        _map.Story = 4;
        _render.GroundStory = -3;
        _render.HeightNumerator = 0x2000;       // smaller, so the gap's rows are on screen
        Blocks(2, 2);

        // Act
        CrossTile11();

        // Assert: its top a story down, then the ground three down, only below the block's bottom
        short entry = _posts.HeightAt(11 * Tile, 10 * Tile + Tile / 2);
        Assert.That(_render.PostCount, Is.EqualTo(2));
        Assert.That((_render.PostCapUp[0], _render.PostCapStory[0]), Is.EqualTo((true, (sbyte)-1)));
        Assert.That((_render.PostCapUp[1], _render.PostCapStory[1]), Is.EqualTo((true, (sbyte)-3)));
        Assert.That(_render.PostClipTop[1], Is.EqualTo(_posts.RowAt(entry, -2)));
    }

    [Test]
    public void A_Lower_Blocks_Top_Shows_Through_The_Gap_Under_A_Higher_One()
    {
        // Arrange: on story 5, blocks on story 1 and stories 3 to 4 (its top the player's floor)
        OnStory3();
        _map.Story = 5;
        _render.GroundStory = -4;
        _render.HeightNumerator = 0x1000;       // smaller, so the gap's rows are on screen
        Blocks(1, 1, 3, 4);

        // Act
        CrossTile11();

        // Assert: the player's floor is a cap too (rows under it are open through the gap, so the
        // level's flats can't be left to fill it); the lower one's top (three down) shows only
        // below the higher block's bottom
        short entry = _posts.HeightAt(11 * Tile, 10 * Tile + Tile / 2);
        var caps = Enumerable.Range(0, _render.PostCount).Where(p => _render.PostCapUp[p]).ToArray();
        Assert.That(caps.Select(p => _render.PostCapStory[p]), Is.EqualTo(new sbyte[] { 0, -3 }));
        Assert.That(_render.PostClipTop[caps[1]], Is.EqualTo(_posts.RowAt(entry, -2)));
    }

    [Test]
    public void A_Higher_Overhead_Blocks_Underside_Shows_Only_Above_The_Lower_Ones_Top()
    {
        // Arrange: on story 1 with a sky, blocks on story 2 and story 4
        _map.Story = 1;
        _render.ViewHeight = 160;
        _render.HeightNumerator = 0x10000;
        (_render.GroundStory, _render.CeilingStory, _render.TallestStory, _render.OpaqueCeiling) = (0, 4, 4, false);
        _posts.StartColumn();
        Blocks(2, 2, 4, 4);
        typeof(MapManager).GetField("mapsegs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(_map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());

        // Act
        _posts.OpenTile(11, 10, 11 * Tile, 10 * Tile + Tile / 2, vertical: true);

        // Assert: both are overhead, the higher's underside over the lower's top
        Assert.That(_render.CellBlocks, Is.EqualTo(2));
        Assert.That((_render.CellFirst[0], _render.CellTop[0], _render.CellFirst[1], _render.CellTop[1]), Is.EqualTo((1, 2, 3, 4)));
        Assert.That((_render.CellUnderClip[0], _render.CellUnderClip[1]), Is.EqualTo((int.MinValue, 2)));
    }

    [Test]
    public void A_Wall_Solid_With_One_Block_Shows_The_Other_As_An_Open_Tiles()
    {
        // Arrange: on story 3, a wall from story 3 to 4 over a block on story 1
        OnStory3();
        Blocks(1, 1, 3, 4);
        _map.TileMap[11, 10] = 5;

        // Act
        _posts.OpenTile(11, 10, 11 * Tile, 10 * Tile + Tile / 2, vertical: true);
        _posts.SetHitBlock(11, 10);

        // Assert: the wall's hit is the upper block; the lower is a block seen from outside, its top the floor
        Assert.That((_render.HitFirst, _render.HitStories), Is.EqualTo((0, 2)));
        Assert.That(_render.CellBlocks, Is.EqualTo(1));
        Assert.That((_render.CellFirst[0], _render.CellTop[0]), Is.EqualTo((-2, -1)));
        Assert.That(_render.CellFloor, Is.EqualTo(-1));
    }

    [Test]
    public void An_Arch_Is_An_Open_Floor_Tile_With_A_Block_Of_Its_Own()
    {
        typeof(MapManager).GetField("mapsegs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(_map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        (_map.BlockStarts[3, 3], _map.BlockStops[3, 3]) = (2, 3);
        (_map.BlockStarts[4, 3], _map.BlockStops[4, 3]) = (1, 1);
        _map.TileMap[4, 3] = 250;                           // solid at story 1 (MapManager.BlockWall)
        Assert.That(_posts.IsArch(3, 3), Is.True);
        Assert.That(_posts.IsArch(4, 3), Is.False);
        Assert.That(_posts.IsArch(5, 3), Is.False);         // no block: open floor
        _map.TileMap[4, 3] = 0;
        _map.Story = 2;
        Assert.That(_posts.IsArch(4, 3), Is.True);          // a raised floor under the player's story
    }
}
