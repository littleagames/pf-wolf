using PFWolf.Constants;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class DiagonalsTests
{
    private const long Tile = MapConstants.TILEGLOBAL;

    // Tile (1, 1)'s top-left corner, in global units
    private const long Ox = Tile, Oy = Tile;

    [TestCase(WallShape.SolidNW, 0.1f, 0.1f)]
    [TestCase(WallShape.SolidNE, 0.9f, 0.1f)]
    [TestCase(WallShape.SolidSW, 0.1f, 0.9f)]
    [TestCase(WallShape.SolidSE, 0.9f, 0.9f)]
    public void The_Named_Corner_Is_Solid(WallShape shape, float u, float v)
    {
        Assert.That(Diagonals.InDiagonalSolid(shape, u, v), Is.True);
    }

    [TestCase(WallShape.SolidNW, 0.9f, 0.9f)]
    [TestCase(WallShape.SolidNE, 0.1f, 0.9f)]
    [TestCase(WallShape.SolidSW, 0.9f, 0.1f)]
    [TestCase(WallShape.SolidSE, 0.1f, 0.1f)]
    public void The_Opposite_Corner_Is_Open(WallShape shape, float u, float v)
    {
        Assert.That(Diagonals.InDiagonalSolid(shape, u, v), Is.False);
    }

    [TestCase(WallShape.SolidNW)]
    [TestCase(WallShape.SolidNE)]
    [TestCase(WallShape.SolidSW)]
    [TestCase(WallShape.SolidSE)]
    public void The_Middle_Is_On_The_Face(WallShape shape)
    {
        Assert.That(Diagonals.DiagonalDepth(shape, Tile / 2, Tile / 2), Is.EqualTo(0));
    }

    [TestCase(WallShape.SolidNW, SeenFlags.NorthFace | SeenFlags.WestFace)]
    [TestCase(WallShape.SolidNE, SeenFlags.NorthFace | SeenFlags.EastFace)]
    [TestCase(WallShape.SolidSW, SeenFlags.SouthFace | SeenFlags.WestFace)]
    [TestCase(WallShape.SolidSE, SeenFlags.SouthFace | SeenFlags.EastFace)]
    [TestCase(WallShape.Square, SeenFlags.NorthFace | SeenFlags.SouthFace | SeenFlags.WestFace | SeenFlags.EastFace)]
    public void Solid_Edges_Are_The_Corner_Ones(WallShape shape, object expected)
    {
        Assert.That(Diagonals.DiagonalSolidEdges(shape), Is.EqualTo((SeenFlags)expected));
    }

    [TestCase(WallShape.SolidNE, 0, 0, 1, 1)]
    [TestCase(WallShape.SolidSW, 0, 0, 1, 1)]
    [TestCase(WallShape.SolidNW, 1, 0, 0, 1)]
    [TestCase(WallShape.SolidSE, 1, 0, 0, 1)]
    public void The_Face_Runs_Between_The_Other_Corners(WallShape shape, int u0, int v0, int u1, int v1)
    {
        Assert.That(Diagonals.DiagonalFaceEnds(shape), Is.EqualTo((u0, v0, u1, v1)));
    }

    [Test]
    public void A_Box_In_The_Solid_Corner_Hits()
    {
        Assert.That(Diagonals.BoxHitsDiagonal(WallShape.SolidNE, 1, 1, Ox + 0xE000, Oy + 0x2000, 0x1000), Is.True);
    }

    [Test]
    public void A_Box_In_The_Open_Corner_Misses()
    {
        Assert.That(Diagonals.BoxHitsDiagonal(WallShape.SolidNE, 1, 1, Ox + 0x2000, Oy + 0xE000, 0x1000), Is.False);
    }

    [Test]
    public void A_Box_Reaching_Over_The_Face_Hits()
    {
        // Centred on the open side, but big enough to cross the face
        Assert.That(Diagonals.BoxHitsDiagonal(WallShape.SolidNE, 1, 1, Ox + 0x6000, Oy + 0xA000, 0x5800), Is.True);
    }

    [Test]
    public void A_Box_Off_The_Tile_Misses()
    {
        Assert.That(Diagonals.BoxHitsDiagonal(WallShape.SolidNE, 1, 1, Ox + 3 * Tile, Oy, 0x1000), Is.False);
    }

    [Test]
    public void A_Line_Across_The_Tile_Hits()
    {
        Assert.That(Diagonals.LineHitsDiagonal(WallShape.SolidNE, 1, 1, Ox - 1, Oy + Tile / 2, Ox + Tile + 1, Oy + Tile / 2), Is.True);
    }

    [Test]
    public void A_Line_In_The_Open_Half_Misses()
    {
        Assert.That(Diagonals.LineHitsDiagonal(WallShape.SolidNE, 1, 1, Ox + 0x1000, Oy + 0xE000, Ox + 0x2000, Oy + 0xF000), Is.False);
    }

    [Test]
    public void A_Line_Along_The_Face_Misses()
    {
        Assert.That(Diagonals.LineHitsDiagonal(WallShape.SolidNE, 1, 1, Ox, Oy, Ox + Tile, Oy + Tile), Is.False);
    }

    [Test]
    public void A_Line_Off_The_Tile_Misses()
    {
        Assert.That(Diagonals.LineHitsDiagonal(WallShape.SolidNE, 1, 1, 0, 0, 0x8000, 0x8000), Is.False);
    }
}
