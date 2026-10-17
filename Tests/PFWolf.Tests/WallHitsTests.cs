using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class WallHitsTests
{
    private const long Tile = MapConstants.TileGlobal;

    private RenderState _render = null!;
    private WallHits _hits = null!;

    [SetUp]
    public void Make()
    {
        _render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.NoShade);
        _hits = new WallHits(null!, null!, null!, null!, null!, _render, new GameSession());
    }

    [TestCase("SolidNE", "East", true)]
    [TestCase("SolidNE", "North", true)]
    [TestCase("SolidNE", "West", false)]
    [TestCase("SolidNE", "South", false)]
    [TestCase("SolidSW", "West", true)]
    [TestCase("SolidSW", "South", true)]
    [TestCase("SolidNW", "East", false)]
    public void A_Diagonals_Solid_Edges_Are_The_Two_On_Its_Solid_Side(string shape, string edge, bool solid)
    {
        Assert.That(_hits.IsSolidEdge(Enum.Parse<WallShape>(shape), Enum.Parse<ControlDir>(edge)), Is.EqualTo(solid));
    }

    [Test]
    public void A_Trace_Meets_The_Face_Where_It_Crosses_It()
    {
        // Arrange: a NW-to-SE face (u == v) in tile (3, 4); from the tile's west edge, halfway
        // down, heading east
        long eu = 0, ev = Tile / 2;

        // Act
        bool hit = _hits.TraceDiagonal(WallShape.SolidNE, 3, 4, eu, ev, Tile, 0);

        // Assert: it meets the face at its middle
        Assert.That(hit, Is.True);
        Assert.That((_render.XIntercept, _render.YIntercept, _render.DiagHitU), Is.EqualTo(((int)(3 * Tile + Tile / 2), (int)(4 * Tile + Tile / 2), (int)(Tile / 2))));
    }

    [Test]
    public void The_Other_Diagonal_Runs_Corner_To_Corner_The_Other_Way()
    {
        // A NE-to-SW face (u + v == TILE), from the west edge a quarter of the way down, heading east
        bool hit = _hits.TraceDiagonal(WallShape.SolidNW, 0, 0, 0, Tile / 4, Tile, 0);

        Assert.That(hit, Is.True);
        Assert.That((_render.XIntercept, _render.YIntercept), Is.EqualTo(((int)(Tile * 3 / 4), (int)(Tile / 4))));
    }

    [Test]
    public void A_Trace_Along_The_Face_Or_Away_From_It_Misses()
    {
        // along the u == v face
        Assert.That(_hits.TraceDiagonal(WallShape.SolidNE, 0, 0, 0, Tile / 2, Tile, Tile), Is.False);
        // heading west, away from it
        Assert.That(_hits.TraceDiagonal(WallShape.SolidNE, 0, 0, 0, Tile / 2, -Tile, 0), Is.False);
    }

    [Test]
    public void One_That_Would_Meet_The_Face_Outside_The_Tile_Misses()
    {
        // From the north edge near its east corner, heading south-east: the face's line is met
        // beyond the tile
        Assert.That(_hits.TraceDiagonal(WallShape.SolidNE, 0, 0, Tile - 10, 0, Tile, Tile / 8), Is.False);
    }

    [TestCase("west", 5 * 64, 58 * 64)]     // flipped: mirrored
    [TestCase("east", 5 * 64, 5 * 64)]
    public void A_Flipped_Face_Shows_Its_Columns_Mirrored(string face, int column, int shown)
    {
        var wall = new MapTextureTranslation { Flip = ["West"] };
        Assert.That(_hits.FlipColumn(wall, face, column), Is.EqualTo(shown));
        Assert.That(_hits.FlipColumn(null, face, column), Is.EqualTo(column));
    }
}
