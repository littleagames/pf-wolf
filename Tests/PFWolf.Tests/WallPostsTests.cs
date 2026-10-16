using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Tests;

public class WallPostsTests
{
    private const int Tile = 1 << MapConstants.TILESHIFT;

    private MapManager _map = null!;
    private RenderState _render = null!;
    private WallPosts _posts = null!;

    [SetUp]
    public void MakeView()
    {
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession())
        {
            tilemap = new byte[MapManager.MAPSIZE, MapManager.MAPSIZE],
        };
        // Looking east from the middle of tile (10, 10), the horizon at row 80
        _render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.noshade)
        {
            viewx = 10 * Tile + Tile / 2, viewy = 10 * Tile + Tile / 2, viewcos = 0x10000, viewsin = 0,
            heightnumerator = 0x100000, centery = 80, vieweyez = 32,
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
        (_render.xintercept, _render.yintercept) = (123, 456);

        // Act
        _posts.HeightAt(12 * Tile, 10 * Tile);

        // Assert
        Assert.That((_render.xintercept, _render.yintercept), Is.EqualTo((123, 456)));
    }

    [Test]
    public void Too_Close_Is_As_Tall_As_At_The_Nearest_Distance()
    {
        Assert.That(_posts.HeightAt(_render.viewx, _render.viewy), Is.EqualTo(_posts.HeightAt(_render.viewx + (int)GameConstants.MINDIST, _render.viewy)));
    }

    [TestCase(32, 32, 80 + 32)]     // eyes half a story up: the floor half a story's pixels below the horizon
    [TestCase(10, 32, 80 + 10)]
    [TestCase(32, 0, 80)]           // eyes on the floor: the floor is the horizon
    public void The_Floor_Row_Goes_With_Eye_Height(int half, int eyez, int row)
    {
        _render.vieweyez = eyez;
        Assert.That(_posts.FloorRow(half), Is.EqualTo(row));
    }

    [TestCase(true, 1, 1, "west")]          // heading east, the trace meets a wall's west face
    [TestCase(true, -1, 1, "east")]
    [TestCase(false, 1, 1, "north")]        // heading south, a horizontal wall's north face
    [TestCase(false, 1, -1, "south")]
    public void The_Face_Seen_Is_The_One_Facing_The_Trace(bool vertical, int xstep, int ystep, string face)
    {
        (_render.xtilestep, _render.ytilestep) = ((short)xstep, (short)ystep);
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
        (_render.xtile, _render.ytile, _render.xtilestep, _render.ytilestep) = (12, 14, 1, 1);

        // Act / Assert
        Assert.That((_posts.VertEdge, _posts.HorizEdge), Is.EqualTo((12 * Tile, 14 * Tile)));
        (_render.xtilestep, _render.ytilestep) = (-1, -1);      // heading west and north: their far sides
        Assert.That((_posts.VertEdge, _posts.HorizEdge), Is.EqualTo((13 * Tile, 15 * Tile)));
    }

    [Test]
    public void The_Trace_Goes_On_From_Where_It_Was_Saved()
    {
        // Arrange
        (_render.xtile, _render.ytile, _render.xintercept, _render.yintercept, _render.texdelta) = (5, 6, 700, 800, 9);
        _posts.SaveTrace();
        (_render.xtile, _render.ytile, _render.xintercept, _render.yintercept, _render.texdelta) = (1, 1, 1, 1, 1);

        // Act
        _posts.RestoreTrace();

        // Assert
        Assert.That((_render.xtile, _render.ytile, _render.xintercept, _render.yintercept, _render.texdelta), Is.EqualTo(((short)5, (short)6, 700, 800, (ushort)9)));
        Assert.That(_render.pastwall, Is.True);
    }

    [Test]
    public void A_Queued_Post_Goes_At_The_End_Of_The_Columns_Queue()
    {
        // Arrange
        var brick = new TextureAsset { RawData = new byte[64 * 64] };
        _render.postcount = 1;

        // Act
        _posts.QueuePost(100, 0, 2, brick, 64, brick, 128, light: Lighting.noshade, patchofs: 5, patchstory: 1);

        // Assert
        Assert.That(_render.postcount, Is.EqualTo(2));
        Assert.That((_render.postheights[1], _render.poststories[1], _render.postlowerofs[1], _render.postupperofs[1]), Is.EqualTo(((short)100, (byte)2, 64, 128)));
        Assert.That((_render.postpatchofs[1], _render.postpatchstory[1]), Is.EqualTo((5, (byte)1)));
    }

    [Test]
    public void An_Arch_Is_An_Open_Tile_More_Than_One_Story_Tall()
    {
        _map.storymap[3, 3] = 2;
        _map.storymap[4, 3] = 1;
        Assert.That(_posts.IsArch(3, 3), Is.True);
        Assert.That(_posts.IsArch(4, 3), Is.False);
        _map.tilemap[3, 3] = 5;
        Assert.That(_posts.IsArch(3, 3), Is.False);         // a wall, however tall
    }
}
