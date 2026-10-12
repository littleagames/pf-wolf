using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;

namespace PFWolf.Tests;

/// <summary>The pixel editor's picture: drawing, lines, fills, see-through pixels and undo</summary>
public class PixelDocumentTests
{
    // Rows of digits as palette indices, '.' see-through
    private static PixelDocument Make(ArtKind kind, params string[] rows)
    {
        int width = rows[0].Length, height = rows.Length;
        var indices = new byte[width * height];
        var opaque = new bool[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var c = rows[y][x];
                opaque[y * width + x] = c != '.';
                indices[y * width + x] = c == '.' ? (byte)0 : (byte)(c - '0');
            }
        }
        return new PixelDocument(kind, "test", new IndexedPicture(width, height, indices, opaque.All(o => o) ? null : opaque));
    }

    private static string[] Rows(PixelDocument document)
        => Enumerable.Range(0, document.Height)
            .Select(y => new string(Enumerable.Range(0, document.Width)
                .Select(x => document.ValueAt(x, y) is var value && value == PixelDocument.Clear ? '.' : (char)('0' + value)).ToArray()))
            .ToArray();

    [Test]
    public void A_Stroke_Is_One_Undo_Step_And_Redo_Puts_It_Back()
    {
        // Arrange
        var document = Make(ArtKind.Sprite, "000", "000", "000");

        // Act: a line corner to corner, then another pixel, as one stroke
        document.BeginEdit("Draw");
        document.Line(0, 0, 2, 2, 5);
        document.SetPixel(2, 0, 7);
        document.EndEdit();
        var drawn = Rows(document);
        document.Undo();
        var undone = Rows(document);
        document.Redo();

        // Assert
        Assert.That(drawn, Is.EqualTo(new[] { "507", "050", "005" }));
        Assert.That(undone, Is.EqualTo(new[] { "000", "000", "000" }));
        Assert.That(Rows(document), Is.EqualTo(drawn));
        Assert.That(document.IsDirty, Is.True);
    }

    [Test]
    public void An_Edit_That_Changes_Nothing_Leaves_No_Undo_Step()
    {
        // Arrange
        var document = Make(ArtKind.Sprite, "11");

        // Act
        document.BeginEdit("Draw");
        document.SetPixel(0, 0, 1);
        document.SetPixel(5, 5, 2);   // off the picture
        document.EndEdit();

        // Assert
        Assert.That(document.CanUndo, Is.False);
        Assert.That(document.IsDirty, Is.False);
    }

    [Test]
    public void Fill_Takes_The_Joined_Pixels_Of_A_Color_And_Ctrl_Fill_Every_One()
    {
        // Arrange
        var joined = Make(ArtKind.Sprite, "1101", "1001", "0111");
        var everywhere = Make(ArtKind.Sprite, "1101", "1001", "0111");

        // Act
        joined.BeginEdit("Fill");
        joined.Fill(0, 0, 4);
        joined.EndEdit();
        everywhere.BeginEdit("Fill");
        everywhere.Fill(0, 0, 4, everywhere: true);
        everywhere.EndEdit();

        // Assert: across and down only, not corner to corner
        Assert.That(Rows(joined), Is.EqualTo(new[] { "4401", "4001", "0111" }));
        Assert.That(Rows(everywhere), Is.EqualTo(new[] { "4404", "4004", "0444" }));
    }

    [Test]
    public void See_Through_Pixels_Are_Drawn_On_Filled_And_Erased_Where_The_Kind_Allows()
    {
        // Arrange
        var sprite = Make(ArtKind.Sprite, "1.", "..");
        var texture = Make(ArtKind.Texture, "12", "34");

        // Act
        sprite.BeginEdit("Fill");
        sprite.Fill(1, 1, 2);
        sprite.SetPixel(0, 0, PixelDocument.Clear);
        sprite.EndEdit();
        texture.BeginEdit("Erase");
        texture.SetPixel(0, 0, PixelDocument.Clear);
        texture.EndEdit();

        // Assert: a wall texture has no see-through pixels, so "clear" draws color 0
        Assert.That(Rows(sprite), Is.EqualTo(new[] { ".2", "22" }));
        Assert.That(texture.Opaque, Is.Null);
        Assert.That(Rows(texture), Is.EqualTo(new[] { "02", "34" }));
    }

    [Test]
    public void Saving_Marks_It_Clean_And_A_Blank_Is_New_Until_Then()
    {
        // Arrange
        var document = PixelDocument.Blank(ArtKind.Sprite, "new", 2, 2, PixelDocument.Clear);

        // Act
        var before = (document.IsNew, document.IsDirty);
        document.BeginEdit("Draw");
        document.SetPixel(1, 1, 3);
        document.EndEdit();
        var dirty = document.IsDirty;
        document.MarkSaved();

        // Assert
        Assert.That(before, Is.EqualTo((true, false)));
        Assert.That(dirty, Is.True);
        Assert.That((document.IsNew, document.IsDirty), Is.EqualTo((false, false)));
        Assert.That(Rows(document), Is.EqualTo(new[] { "..", ".3" }));
        Assert.That(document.Offset, Is.EqualTo((0, 0)));
    }
}
