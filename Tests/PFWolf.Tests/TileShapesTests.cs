using PFWolf.Editor.Editing;

namespace PFWolf.Tests;

public class TileShapesTests
{
    [Test]
    public void Line_Includes_Both_Ends_And_Has_No_Gaps()
    {
        // Act
        var line = TileShapes.Line(2, 3, 9, 6).ToList();

        // Assert
        Assert.That(line[0], Is.EqualTo((2, 3)));
        Assert.That(line[^1], Is.EqualTo((9, 6)));
        Assert.That(line, Has.Count.EqualTo(8));
        for (int i = 1; i < line.Count; i++)
            Assert.That(Math.Max(Math.Abs(line[i].X - line[i - 1].X), Math.Abs(line[i].Y - line[i - 1].Y)), Is.EqualTo(1));
    }

    [Test]
    public void Line_To_The_Same_Tile_Is_One_Tile()
    {
        // Act
        var line = TileShapes.Line(4, 4, 4, 4).ToList();

        // Assert
        Assert.That(line, Is.EqualTo(new[] { (4, 4) }));
    }

    [Test]
    public void Rectangle_From_Any_Corner_Covers_The_Same_Tiles()
    {
        // Act
        var forwards = TileShapes.Rectangle(TileRect.FromCorners(1, 1, 4, 3), outline: false).ToHashSet();
        var backwards = TileShapes.Rectangle(TileRect.FromCorners(4, 3, 1, 1), outline: false).ToHashSet();

        // Assert
        Assert.That(forwards, Has.Count.EqualTo(12));
        Assert.That(backwards.SetEquals(forwards), Is.True);
    }

    [Test]
    public void Rectangle_Outline_Is_Only_The_Edge()
    {
        // Act
        var outline = TileShapes.Rectangle(TileRect.FromCorners(0, 0, 4, 4), outline: true).ToList();

        // Assert
        Assert.That(outline, Has.Count.EqualTo(16));
        Assert.That(outline, Does.Not.Contain((2, 2)));
    }

    [Test]
    public void FloodFill_Stops_At_Other_Values()
    {
        // Arrange: a 3x3 room walled off with value 1
        var document = EditorMaps.Document();
        using (var edit = document.BeginEdit("Walls"))
        {
            foreach (var (x, y) in TileShapes.Rectangle(TileRect.FromCorners(10, 10, 14, 14), outline: true))
                edit.Set(0, x, y, 1);
        }

        // Act
        var inside = TileShapes.FloodFill(document, 0, 12, 12);

        // Assert
        Assert.That(inside, Has.Count.EqualTo(9));
        Assert.That(inside.All(tile => tile.X is >= 11 and <= 13 && tile.Y is >= 11 and <= 13), Is.True);
    }

    [Test]
    public void FloodFill_Joins_Through_Edges_Not_Corners()
    {
        // Arrange: two floor tiles touching only at a corner, in a field of walls
        var document = EditorMaps.Document();
        using (var edit = document.BeginEdit("Walls"))
        {
            foreach (var (x, y) in TileShapes.Rectangle(TileRect.FromCorners(0, 0, 63, 63), outline: false))
                edit.Set(0, x, y, 1);
            edit.Set(0, 5, 5, 100);
            edit.Set(0, 6, 6, 100);
        }

        // Act
        var filled = TileShapes.FloodFill(document, 0, 5, 5);

        // Assert
        Assert.That(filled, Is.EqualTo(new[] { (5, 5) }));
    }
}
