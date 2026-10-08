using PFWolf.Editor.Editing;

namespace PFWolf.Tests;

public class ToolControllerTests
{
    private static (MapDocument Document, ToolController Tools) Setup(EditTool tool, ushort value = 1, int plane = 0)
    {
        var document = EditorMaps.Document();
        var tools = new ToolController
        {
            Document = document,
            Tool = tool,
            Value = value,
            Plane = plane,
            EraseValue = p => p == 0 ? (ushort)100 : (ushort)0,
        };
        return (document, tools);
    }

    [Test]
    public void A_Paint_Stroke_Fills_The_Gaps_And_Undoes_As_One()
    {
        // Arrange
        var (document, tools) = Setup(EditTool.Paint);

        // Act: a quick stroke whose pointer events skip tiles
        tools.Press(2, 2);
        tools.Move(8, 2);
        tools.Release();
        var painted = Enumerable.Range(2, 7).All(x => document[0, x, 2] == 1);
        document.Undo();

        // Assert
        Assert.That(painted, Is.True);
        Assert.That(Enumerable.Range(2, 7).All(x => document[0, x, 2] == 100), Is.True);
        Assert.That(document.CanUndo, Is.False);
    }

    [Test]
    public void A_Rectangle_Is_Redrawn_As_It_Is_Dragged()
    {
        // Arrange
        var (document, tools) = Setup(EditTool.Rectangle);

        // Act: drag out, then back in before letting go
        tools.Press(1, 1);
        tools.Move(10, 10);
        tools.Move(3, 3);
        tools.Release();

        // Assert
        Assert.That(document[0, 3, 3], Is.EqualTo(1));
        Assert.That(document[0, 10, 10], Is.EqualTo(100));
    }

    [Test]
    public void Fill_Changes_The_Joined_Area_On_The_Plane_Being_Edited()
    {
        // Arrange
        var (document, tools) = Setup(EditTool.Fill, value: 7, plane: 4);

        // Act
        tools.Press(0, 0);
        tools.Release();

        // Assert
        Assert.That(document[4, 63, 63], Is.EqualTo(7));
        Assert.That(document[0, 63, 63], Is.EqualTo(100));
    }

    [Test]
    public void Pick_Reports_The_Value_Under_The_Pointer()
    {
        // Arrange
        var (document, tools) = Setup(EditTool.Pick);
        EditorMaps.Set(document, 0, 4, 5, 42);
        ushort? picked = null;
        tools.ValuePicked += (_, value) => picked = value;

        // Act
        tools.Press(4, 5);
        tools.Release();

        // Assert
        Assert.That(picked, Is.EqualTo(42));
    }

    [Test]
    public void Copy_And_Paste_Put_The_Tiles_Where_The_Click_Is()
    {
        // Arrange
        var (document, tools) = Setup(EditTool.Select);
        EditorMaps.Set(document, 0, 2, 2, 1);
        EditorMaps.Set(document, 1, 3, 3, 23);
        tools.Press(2, 2);
        tools.Move(3, 3);
        tools.Release();

        // Act
        tools.Copy();
        tools.StartPaste(20, 20);
        tools.Move(30, 40);
        tools.Press(30, 40);
        tools.Release();

        // Assert
        Assert.That(document[0, 30, 40], Is.EqualTo(1));
        Assert.That(document[1, 31, 41], Is.EqualTo(23));
        Assert.That(document[0, 20, 20], Is.EqualTo(100), "the paste's first position is put back as it moves");
        Assert.That(tools.Selection, Is.EqualTo(new TileRect(30, 40, 31, 41)));
    }

    [Test]
    public void Escape_Drops_A_Paste_Without_Changing_The_Level()
    {
        // Arrange
        var (document, tools) = Setup(EditTool.Select);
        EditorMaps.Set(document, 0, 2, 2, 1);
        tools.Press(2, 2);
        tools.Release();
        tools.Copy();
        var steps = document.UndoDescription;

        // Act
        tools.StartPaste(10, 10);
        tools.Escape();

        // Assert
        Assert.That(document[0, 10, 10], Is.EqualTo(100));
        Assert.That(document.UndoDescription, Is.EqualTo(steps));
    }

    [Test]
    public void Dragging_Inside_The_Selection_Moves_Its_Tiles()
    {
        // Arrange
        var (document, tools) = Setup(EditTool.Select);
        EditorMaps.Set(document, 0, 5, 5, 1);
        tools.Press(5, 5);
        tools.Release();

        // Act
        tools.Press(5, 5);
        tools.Move(8, 9);
        tools.Release();

        // Assert
        Assert.That(document[0, 8, 9], Is.EqualTo(1));
        Assert.That(document[0, 5, 5], Is.EqualTo(100), "where it was is cleared to open floor");
        Assert.That(tools.Selection, Is.EqualTo(new TileRect(8, 9, 8, 9)));
    }

    [Test]
    public void Copying_One_Plane_Leaves_The_Others_Where_It_Is_Pasted()
    {
        // Arrange
        var (document, tools) = Setup(EditTool.Select, plane: 1);
        tools.CopyAllPlanes = false;
        EditorMaps.Set(document, 0, 2, 2, 1);
        EditorMaps.Set(document, 1, 2, 2, 23);
        tools.Press(2, 2);
        tools.Release();

        // Act
        tools.Copy();
        tools.StartPaste(9, 9);
        tools.Press(9, 9);
        tools.Release();

        // Assert
        Assert.That(document[1, 9, 9], Is.EqualTo(23));
        Assert.That(document[0, 9, 9], Is.EqualTo(100));
    }

    [Test]
    public void Delete_Clears_The_Selection_To_Each_Planes_Erase_Value()
    {
        // Arrange
        var (document, tools) = Setup(EditTool.Select);
        EditorMaps.Set(document, 0, 2, 2, 1);
        EditorMaps.Set(document, 1, 2, 2, 23);
        tools.Press(2, 2);
        tools.Release();

        // Act
        tools.Delete();

        // Assert
        Assert.That(document[0, 2, 2], Is.EqualTo(100));
        Assert.That(document[1, 2, 2], Is.EqualTo(0));
    }

    [Test]
    public void A_Paste_Hanging_Off_The_Level_Drops_What_Falls_Off()
    {
        // Arrange
        var (document, tools) = Setup(EditTool.Select);
        tools.SelectAll();
        tools.Copy();
        EditorMaps.Set(document, 0, 63, 63, 1);

        // Act
        tools.StartPaste(62, 62);
        tools.Press(62, 62);
        tools.Release();

        // Assert: tile (0, 0) of the copy lands on (62, 62); the rest is off the level
        Assert.That(document[0, 62, 62], Is.EqualTo(100));
        Assert.That(document[0, 63, 63], Is.EqualTo(100));
    }
}
