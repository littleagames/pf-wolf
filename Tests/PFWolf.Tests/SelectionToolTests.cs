using PFWolf.Editor.Editing;

namespace PFWolf.Tests;

/// <summary>The editor's selections of any shape, the magic wand, nudging, duplicating, pasting over and Replace</summary>
public class SelectionToolTests
{
    private const ushort Floor = 100, Wall = 1, OtherWall = 2;

    private static (MapDocument Document, ToolController Tools) Setup(EditTool tool = EditTool.Select)
    {
        var document = EditorMaps.Document();
        var tools = new ToolController
        {
            Document = document,
            Tool = tool,
            EraseValue = p => p == 0 ? Floor : (ushort)0,
            IsBlank = (p, value) => p == 0 ? value == Floor : value == 0,
        };
        return (document, tools);
    }

    private static void Drag(ToolController tools, int x0, int y0, int x1, int y1, SelectMode mode = SelectMode.Replace)
    {
        tools.SelectMode = mode;
        tools.Press(x0, y0);
        tools.Move(x1, y1);
        tools.Release();
        tools.SelectMode = SelectMode.Replace;
    }

    [Test]
    public void Shift_Adds_A_Box_To_The_Selection_And_Ctrl_Takes_One_Away()
    {
        // Arrange
        var (_, tools) = Setup();

        // Act: two boxes side by side, then the middle of the first taken out
        Drag(tools, 0, 0, 2, 2);
        Drag(tools, 5, 0, 6, 0, SelectMode.Add);
        Drag(tools, 1, 1, 1, 1, SelectMode.Subtract);
        var selection = tools.Selection!;

        // Assert
        Assert.That(selection.Bounds, Is.EqualTo(new TileRect(0, 0, 6, 2)));
        Assert.That(selection.Contains(0, 0), Is.True);
        Assert.That(selection.Contains(1, 1), Is.False);
        Assert.That(selection.Contains(3, 0), Is.False);
        Assert.That(selection.Contains(6, 0), Is.True);
        Assert.That(selection.Tiles.Count(), Is.EqualTo(10));
    }

    [Test]
    public void Moving_A_Shaped_Selection_Moves_Only_Its_Tiles()
    {
        // Arrange: walls at (0, 0) and (1, 0); only (0, 0) and (0, 1) selected
        var (document, tools) = Setup();
        EditorMaps.Set(document, 0, 0, 0, Wall);
        EditorMaps.Set(document, 0, 1, 0, OtherWall);
        Drag(tools, 0, 0, 0, 1);

        // Act: drag it 5 to the right
        Drag(tools, 0, 0, 5, 0);

        // Assert: the WallId went, its neighbour stayed
        Assert.That(document[0, 5, 0], Is.EqualTo(Wall));
        Assert.That(document[0, 0, 0], Is.EqualTo(Floor));
        Assert.That(document[0, 1, 0], Is.EqualTo(OtherWall));
        Assert.That(document[0, 6, 0], Is.EqualTo(Floor));
    }

    [Test]
    public void The_Magic_Wand_Selects_The_Joined_Tiles_Like_The_One_Clicked()
    {
        // Arrange: a 3-tile run of walls, and one more apart from it
        var (document, tools) = Setup(EditTool.MagicWand);
        foreach (var x in new[] { 10, 11, 12, 20 })
            EditorMaps.Set(document, 0, x, 5, Wall);

        // Act
        tools.Press(11, 5);
        tools.Release();
        var joined = tools.Selection!.Tiles.ToList();
        tools.WandJoined = false;
        tools.Press(11, 5);
        tools.Release();
        var everywhere = tools.Selection!.Tiles.ToList();

        // Assert
        Assert.That(joined, Is.EquivalentTo(new[] { (10, 5), (11, 5), (12, 5) }));
        Assert.That(everywhere, Has.Count.EqualTo(4));
        Assert.That(everywhere, Does.Contain((20, 5)));
    }

    [Test]
    public void The_Arrow_Keys_Move_The_Selection_A_Tile_At_A_Time()
    {
        // Arrange
        var (document, tools) = Setup();
        EditorMaps.Set(document, 0, 3, 3, Wall);
        Drag(tools, 3, 3, 3, 3);

        // Act
        tools.Nudge(1, 0);
        tools.Nudge(0, 1);

        // Assert
        Assert.That(document[0, 4, 4], Is.EqualTo(Wall));
        Assert.That(document[0, 3, 3], Is.EqualTo(Floor));
        Assert.That(tools.Selection!.Bounds, Is.EqualTo(new TileRect(4, 4, 4, 4)));
    }

    [Test]
    public void Duplicate_Picks_Up_A_Copy_And_Leaves_The_Original()
    {
        // Arrange
        var (document, tools) = Setup();
        EditorMaps.Set(document, 0, 3, 3, Wall);
        Drag(tools, 3, 3, 3, 3);

        // Act
        tools.Duplicate();
        tools.Move(9, 9);
        tools.Press(9, 9);
        tools.Release();

        // Assert
        Assert.That(document[0, 3, 3], Is.EqualTo(Wall));
        Assert.That(document[0, 9, 9], Is.EqualTo(Wall));
    }

    [Test]
    public void Pasting_Over_Leaves_What_Is_Under_The_Pastes_Open_Floor()
    {
        // Arrange: copy a WallId with open floor beside it, onto a row of other walls
        var (document, tools) = Setup();
        EditorMaps.Set(document, 0, 0, 0, Wall);
        foreach (var x in new[] { 10, 11 })
            EditorMaps.Set(document, 0, x, 10, OtherWall);
        Drag(tools, 0, 0, 1, 0);
        tools.Copy();
        tools.PasteSkipsBlank = true;

        // Act
        tools.StartPaste(10, 10);
        tools.Press(10, 10);
        tools.Release();

        // Assert
        Assert.That(document[0, 10, 10], Is.EqualTo(Wall));
        Assert.That(document[0, 11, 10], Is.EqualTo(OtherWall));
    }

    [Test]
    public void Replace_Swaps_A_Value_In_The_Selection_Only_When_Asked()
    {
        // Arrange
        var (document, tools) = Setup();
        foreach (var x in new[] { 1, 2, 30 })
            EditorMaps.Set(document, 0, x, 1, Wall);
        Drag(tools, 0, 0, 5, 5);

        // Act
        int count = tools.Replace(Wall, OtherWall, selectionOnly: true);

        // Assert
        Assert.That(count, Is.EqualTo(2));
        Assert.That(document[0, 1, 1], Is.EqualTo(OtherWall));
        Assert.That(document[0, 30, 1], Is.EqualTo(Wall));
    }

    [Test]
    public void Rotating_A_Shaped_Selection_Turns_Its_Shape()
    {
        // Arrange: an L of three tiles in a 2 × 2 box: top left, bottom left, bottom right
        var (document, tools) = Setup();
        EditorMaps.Set(document, 0, 10, 10, Wall);
        Drag(tools, 10, 10, 10, 11);
        Drag(tools, 11, 11, 11, 11, SelectMode.Add);

        // Act
        tools.Transform(TileTransform.RotateClockwise);
        var selection = tools.Selection!;

        // Assert: the top left corner went top right, the rest round after it, leaving the bottom right out
        Assert.That(document[0, 11, 10], Is.EqualTo(Wall));
        Assert.That(selection.Contains(11, 10), Is.True);
        Assert.That(selection.Contains(10, 10), Is.True);
        Assert.That(selection.Contains(10, 11), Is.True);
        Assert.That(selection.Contains(11, 11), Is.False);
    }

    [Test]
    public void Invert_Selects_Everything_Else()
    {
        // Arrange
        var (_, tools) = Setup();
        Drag(tools, 0, 0, 0, 0);

        // Act
        tools.InvertSelection();

        // Assert
        Assert.That(tools.Selection!.Contains(0, 0), Is.False);
        Assert.That(tools.Selection.Tiles.Count(), Is.EqualTo(64 * 64 - 1));
    }
}
