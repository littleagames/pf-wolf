using PFWolf.Editor.Editing;

namespace PFWolf.Tests;

public class MapDocumentTests
{
    [Test]
    public void Editing_Changes_The_Copy_Not_The_Loaded_Level()
    {
        // Arrange
        var loaded = EditorMaps.Blank();
        var document = new MapDocument("MAP01", loaded);

        // Act
        EditorMaps.Set(document, 0, 5, 6, 1);

        // Assert
        Assert.That(document[0, 5, 6], Is.EqualTo(1));
        Assert.That(loaded.MapData[0][6 * 64 + 5], Is.EqualTo(100));
    }

    [Test]
    public void Undo_And_Redo_Step_Through_Edits()
    {
        // Arrange
        var document = EditorMaps.Document();
        EditorMaps.Set(document, 0, 1, 1, 1);
        EditorMaps.Set(document, 0, 1, 1, 2);

        // Act
        document.Undo();
        var afterOneUndo = document[0, 1, 1];
        document.Undo();
        var afterTwoUndos = document[0, 1, 1];
        document.Redo();

        // Assert
        Assert.That(afterOneUndo, Is.EqualTo(1));
        Assert.That(afterTwoUndos, Is.EqualTo(100));
        Assert.That(document[0, 1, 1], Is.EqualTo(1));
        Assert.That(document.CanRedo, Is.True);
    }

    [Test]
    public void A_Stroke_Over_Its_Own_Path_Undoes_To_The_First_Value()
    {
        // Arrange
        var document = EditorMaps.Document();

        // Act
        using (var edit = document.BeginEdit("Paint"))
        {
            edit.Set(0, 3, 3, 1);
            edit.Set(0, 3, 3, 2);
            edit.Set(0, 3, 3, 5);
        }
        var painted = document[0, 3, 3];
        document.Undo();

        // Assert
        Assert.That(painted, Is.EqualTo(5));
        Assert.That(document[0, 3, 3], Is.EqualTo(100));
        Assert.That(document.CanUndo, Is.False);
    }

    [Test]
    public void An_Edit_That_Changes_Nothing_Leaves_No_Undo_Step()
    {
        // Arrange
        var document = EditorMaps.Document();

        // Act
        using (var edit = document.BeginEdit("Paint"))
        {
            edit.Set(0, 3, 3, 1);
            edit.Set(0, 3, 3, 100);
        }

        // Assert
        Assert.That(document.CanUndo, Is.False);
        Assert.That(document.IsDirty, Is.False);
    }

    [Test]
    public void Revert_Puts_Tiles_Back_But_Keeps_The_Edit_Open()
    {
        // Arrange
        var document = EditorMaps.Document();
        var edit = document.BeginEdit("Line");
        edit.Set(0, 1, 1, 1);

        // Act
        edit.Revert();
        edit.Set(0, 2, 2, 1);
        edit.Commit();
        document.Undo();

        // Assert
        Assert.That(document[0, 1, 1], Is.EqualTo(100));
        Assert.That(document[0, 2, 2], Is.EqualTo(100));
    }

    [Test]
    public void Cancel_Takes_The_Changes_Back_Without_An_Undo_Step()
    {
        // Arrange
        var document = EditorMaps.Document();
        var edit = document.BeginEdit("Paste");
        edit.Set(0, 1, 1, 1);

        // Act
        edit.Cancel();

        // Assert
        Assert.That(document[0, 1, 1], Is.EqualTo(100));
        Assert.That(document.CanUndo, Is.False);
    }

    [Test]
    public void Tiles_Off_The_Level_Are_Ignored()
    {
        // Arrange
        var document = EditorMaps.Document();

        // Act
        using (var edit = document.BeginEdit("Paint"))
        {
            edit.Set(0, -1, 0, 1);
            edit.Set(0, 64, 0, 1);
            edit.Set(9, 0, 0, 1);
        }

        // Assert
        Assert.That(document.CanUndo, Is.False);
    }

    [Test]
    public void Dirty_Follows_The_Save_Point_Through_Undo_And_Redo()
    {
        // Arrange
        var document = EditorMaps.Document();
        EditorMaps.Set(document, 0, 1, 1, 1);
        document.MarkSaved();

        // Act
        var afterSave = document.IsDirty;
        document.Undo();
        var afterUndo = document.IsDirty;
        document.Redo();
        var afterRedo = document.IsDirty;

        // Assert
        Assert.That(afterSave, Is.False);
        Assert.That(afterUndo, Is.True);
        Assert.That(afterRedo, Is.False);
    }

    [Test]
    public void An_Edit_After_Undoing_Past_The_Save_Never_Gets_Back_To_It()
    {
        // Arrange
        var document = EditorMaps.Document();
        EditorMaps.Set(document, 0, 1, 1, 1);
        document.MarkSaved();
        document.Undo();

        // Act
        EditorMaps.Set(document, 0, 2, 2, 1);
        document.Undo();

        // Assert: the level is as it was before the save, not as saved
        Assert.That(document.IsDirty, Is.True);
    }

    [Test]
    public void A_New_Level_Is_Unsaved_Until_Saved()
    {
        // Arrange
        var document = new MapDocument("MAP61", EditorMaps.Blank(), isNew: true);

        // Act
        var before = document.IsDirty;
        document.MarkSaved();

        // Assert
        Assert.That(before, Is.True);
        Assert.That(document.IsDirty, Is.False);
    }
}
