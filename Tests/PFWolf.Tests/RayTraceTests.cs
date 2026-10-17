using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class RayTraceTests
{
    private const int Tile = 1 << MapConstants.TileShift;

    private MapManager _map = null!;
    private RenderState _render = null!;
    private RayTrace _trace = null!;

    [SetUp]
    public void Make()
    {
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession(), null)
        {
            Spotvis = new bool[MapManager.MapSize, MapManager.MapSize],
            Seen = new SeenFlags[MapManager.MapSize, MapManager.MapSize],
        };
        _render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.NoShade);
        _trace = new RayTrace(_map, null!, null!, null!, null!, _render);
    }

    [Test]
    public void Crossing_A_Vertical_Edge_Marks_The_Tile_Seen_And_Steps_On()
    {
        // Arrange: heading east into column 5, the ray at y 7.5 tiles, rising a quarter tile a column
        (_render.XTile, _render.XTileStep, _render.YIntercept) = (5, 1, 7 * Tile + Tile / 2);
        _render.YIntTile = _render.YIntercept >> MapConstants.TileShift;

        // Act
        _trace.PassVert(-Tile / 4);

        // Assert
        Assert.That(_map.Spotvis[5, 7], Is.True);
        Assert.That((_render.XTile, _render.YIntercept, _render.YIntTile), Is.EqualTo(((short)6, 7 * Tile + Tile / 4, 7)));
    }

    [Test]
    public void Crossing_A_Horizontal_Edge_Does_The_Same_The_Other_Way()
    {
        // Arrange: heading north out of row 9, the ray at x 3.9 tiles, moving right a fifth of a tile a row
        (_render.YTile, _render.YTileStep, _render.XIntercept) = (9, -1, 3 * Tile + Tile * 9 / 10);
        _render.XIntTile = _render.XIntercept >> MapConstants.TileShift;

        // Act
        _trace.PassHoriz(Tile / 5);

        // Assert: it has crossed into column 4
        Assert.That(_map.Spotvis[3, 9], Is.True);
        Assert.That((_render.YTile, _render.XIntTile), Is.EqualTo(((short)8, 4)));
    }

    [Test]
    public void Tiles_Passed_While_Tracing_On_Past_A_Wall_Arent_Seen()
    {
        // Arrange
        _render.PastWall = true;
        (_render.XTile, _render.XTileStep, _render.YIntercept, _render.YIntTile) = (5, 1, 7 * Tile, 7);

        // Act
        _trace.PassVert(0);

        // Assert
        Assert.That(_map.Spotvis[5, 7], Is.False);
    }

    [Test]
    public void Faces_Go_On_The_Automap_Only_From_The_Players_Own_View()
    {
        // Act / Assert
        _trace.MarkSeen(4, 4, SeenFlags.WestFace);
        Assert.That(_map.Seen[4, 4], Is.EqualTo(SeenFlags.None));

        _render.MarkSeen = true;
        _trace.MarkSeen(4, 4, SeenFlags.WestFace);
        _trace.MarkSeen(4, 4, SeenFlags.NorthFace);
        Assert.That(_map.Seen[4, 4], Is.EqualTo(SeenFlags.WestFace | SeenFlags.NorthFace));
    }
}
