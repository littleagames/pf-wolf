using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class RayTraceTests
{
    private const int Tile = 1 << MapConstants.TILESHIFT;

    private MapManager _map = null!;
    private RenderState _render = null!;
    private RayTrace _trace = null!;

    [SetUp]
    public void Make()
    {
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")))
        {
            spotvis = new bool[MapManager.MAPSIZE, MapManager.MAPSIZE],
            seen = new SeenFlags[MapManager.MAPSIZE, MapManager.MAPSIZE],
        };
        _render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.noshade);
        _trace = new RayTrace(_map, null!, null!, null!, null!, _render);
    }

    [Test]
    public void Crossing_A_Vertical_Edge_Marks_The_Tile_Seen_And_Steps_On()
    {
        // Arrange: heading east into column 5, the ray at y 7.5 tiles, rising a quarter tile a column
        (_render.xtile, _render.xtilestep, _render.yintercept) = (5, 1, 7 * Tile + Tile / 2);
        _render.yinttile = _render.yintercept >> MapConstants.TILESHIFT;

        // Act
        _trace.passvert(-Tile / 4);

        // Assert
        Assert.That(_map.spotvis[5, 7], Is.True);
        Assert.That((_render.xtile, _render.yintercept, _render.yinttile), Is.EqualTo(((short)6, 7 * Tile + Tile / 4, 7)));
    }

    [Test]
    public void Crossing_A_Horizontal_Edge_Does_The_Same_The_Other_Way()
    {
        // Arrange: heading north out of row 9, the ray at x 3.9 tiles, moving right a fifth of a tile a row
        (_render.ytile, _render.ytilestep, _render.xintercept) = (9, -1, 3 * Tile + Tile * 9 / 10);
        _render.xinttile = _render.xintercept >> MapConstants.TILESHIFT;

        // Act
        _trace.passhoriz(Tile / 5);

        // Assert: it has crossed into column 4
        Assert.That(_map.spotvis[3, 9], Is.True);
        Assert.That((_render.ytile, _render.xinttile), Is.EqualTo(((short)8, 4)));
    }

    [Test]
    public void Tiles_Passed_While_Tracing_On_Past_A_Wall_Arent_Seen()
    {
        // Arrange
        _render.pastwall = true;
        (_render.xtile, _render.xtilestep, _render.yintercept, _render.yinttile) = (5, 1, 7 * Tile, 7);

        // Act
        _trace.passvert(0);

        // Assert
        Assert.That(_map.spotvis[5, 7], Is.False);
    }

    [Test]
    public void Faces_Go_On_The_Automap_Only_From_The_Players_Own_View()
    {
        // Act / Assert
        _trace.MarkSeen(4, 4, SeenFlags.WestFace);
        Assert.That(_map.seen[4, 4], Is.EqualTo(SeenFlags.None));

        _render.markseen = true;
        _trace.MarkSeen(4, 4, SeenFlags.WestFace);
        _trace.MarkSeen(4, 4, SeenFlags.NorthFace);
        Assert.That(_map.seen[4, 4], Is.EqualTo(SeenFlags.WestFace | SeenFlags.NorthFace));
    }
}
