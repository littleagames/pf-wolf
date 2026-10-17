using PFWolf.Editor.Editing;

namespace PFWolf.Tests;

/// <summary>The editor's brushes and the shape, room and stamp tools</summary>
public class BrushToolTests
{
    private const ushort Floor = 100, Wall = 1, OtherWall = 2;

    private static (MapDocument Document, ToolController Tools) Setup(EditTool tool, ushort value = Wall)
    {
        var document = EditorMaps.Document();
        var tools = new ToolController
        {
            Document = document,
            Tool = tool,
            Value = value,
            EraseValue = p => p == 0 ? Floor : (ushort)0,
            IsBlank = (p, v) => p == 0 ? v == Floor : v == 0,
        };
        return (document, tools);
    }

    private static int Count(MapDocument document, ushort value)
        => Enumerable.Range(0, 64 * 64).Count(i => document[0, i % 64, i / 64] == value);

    [TestCase(1, false, 1)]
    [TestCase(3, false, 9)]
    [TestCase(3, true, 5)]
    [TestCase(5, true, 21)]
    public void A_Brush_Covers_Its_Size_And_Shape(int size, bool round, int tiles)
        => Assert.That(TileShapes.Brush(10, 10, size, round).Count(), Is.EqualTo(tiles));

    [Test]
    public void A_Wide_Brush_Paints_A_Wide_Stroke_As_One_Undo_Step()
    {
        // Arrange
        var (document, tools) = Setup(EditTool.Paint);
        tools.BrushSize = 3;

        // Act: a stroke along row 10 from x 5 to 15
        tools.Press(5, 10);
        tools.Move(15, 10);
        tools.Release();

        // Assert: three rows, 4 to 16 across
        Assert.That(Count(document, Wall), Is.EqualTo(13 * 3));
        Assert.That(document[0, 4, 9], Is.EqualTo(Wall));
        Assert.That(document[0, 16, 11], Is.EqualTo(Wall));
        document.Undo();
        Assert.That(Count(document, Wall), Is.Zero);
    }

    [Test]
    public void The_Eraser_Clears_To_Open_Floor()
    {
        // Arrange
        var (document, tools) = Setup(EditTool.Erase);
        EditorMaps.Set(document, 0, 3, 3, Wall);

        // Act
        tools.Press(3, 3);
        tools.Release();

        // Assert
        Assert.That(document[0, 3, 3], Is.EqualTo(Floor));
    }

    [Test]
    public void Scatter_Puts_The_Sets_Values_On_Some_Of_The_Tiles()
    {
        // Arrange
        var (document, tools) = Setup(EditTool.Scatter);
        tools.BrushSize = 9;
        tools.ScatterDensity = 50;
        tools.ScatterValues = [Wall, OtherWall];
        tools.Random = new Random(1);

        // Act: back and forth over the same tiles, which are only tried once
        tools.Press(20, 20);
        tools.Move(21, 20);
        tools.Move(20, 20);
        tools.Release();
        int walls = Count(document, Wall), others = Count(document, OtherWall);

        // Assert: about half of the 90 tiles passed over, some of each
        Assert.That(walls + others, Is.InRange(25, 65));
        Assert.That(walls, Is.GreaterThan(0));
        Assert.That(others, Is.GreaterThan(0));
    }

    [Test]
    public void An_Ellipse_Outline_Is_Round()
    {
        // Act: a circle 7 across
        var tiles = TileShapes.Ellipse(new TileRect(0, 0, 6, 6), outline: true).ToHashSet();

        // Assert: the middle of each side, not the corners or the centre
        Assert.That(tiles, Does.Contain((3, 0)).And.Contain((0, 3)).And.Contain((6, 3)).And.Contain((3, 6)));
        Assert.That(tiles, Does.Not.Contain((0, 0)).And.Not.Contain((3, 3)));
    }

    [Test]
    public void Shift_Keeps_A_Line_Straight_And_A_Box_Square()
    {
        Assert.That(TileShapes.Straighten(0, 0, 10, 2), Is.EqualTo((10, 0)));
        Assert.That(TileShapes.Straighten(0, 0, 7, 6), Is.EqualTo((7, 7)));
        Assert.That(TileShapes.Square(5, 5, 2, 9), Is.EqualTo((1, 9)));
    }

    [Test]
    public void A_Room_Has_Walls_Round_Open_Floor()
    {
        // Arrange: a wall in the way inside where the room goes
        var (document, tools) = Setup(EditTool.Room);
        EditorMaps.Set(document, 0, 12, 12, OtherWall);

        // Act
        tools.Press(10, 10);
        tools.Move(14, 14);
        tools.Release();

        // Assert
        Assert.That(document[0, 10, 10], Is.EqualTo(Wall));
        Assert.That(document[0, 14, 12], Is.EqualTo(Wall));
        Assert.That(document[0, 12, 12], Is.EqualTo(Floor));
        Assert.That(Count(document, Wall), Is.EqualTo(16));
    }

    [Test]
    public void A_Stamp_Drag_Lays_Copies_Side_By_Side()
    {
        // Arrange: copy a 2 × 1 piece: a wall and open floor
        var (document, tools) = Setup(EditTool.Select);
        EditorMaps.Set(document, 0, 0, 0, Wall);
        tools.Press(0, 0);
        tools.Move(1, 0);
        tools.Release();
        tools.Copy();
        tools.Tool = EditTool.Stamp;

        // Act: drag 5 tiles right from (20, 20)
        tools.Press(20, 20);
        for (int x = 21; x <= 25; x++)
            tools.Move(x, 20);
        tools.Release();

        // Assert: copies at 20, 22, 24 (each around the pointer: the wall a tile left of it)
        Assert.That(document[0, 19, 20], Is.EqualTo(Wall));
        Assert.That(document[0, 21, 20], Is.EqualTo(Wall));
        Assert.That(document[0, 23, 20], Is.EqualTo(Wall));
        Assert.That(document[0, 20, 20], Is.EqualTo(Floor));
        Assert.That(document.UndoDescription, Is.EqualTo("Stamp"));
    }

    [Test]
    public void Turning_With_The_Stamp_Turns_The_Stamp()
    {
        // Arrange
        var (document, tools) = Setup(EditTool.Select);
        tools.Press(0, 0);
        tools.Move(2, 0);
        tools.Release();
        tools.Copy();
        tools.Tool = EditTool.Stamp;

        // Act
        tools.Transform(TileTransform.RotateClockwise);

        // Assert: 1 wide, 3 high now; the level's untouched
        Assert.That(tools.ClipboardSize, Is.EqualTo((1, 3)));
        Assert.That(document.CanUndo, Is.False);
    }
}
