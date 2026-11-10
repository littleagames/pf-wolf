using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Tests;

/// <summary>The glyph editor's font: drawing, the glyph and font changes, undo, writing font files</summary>
public class FontDocumentTests
{
    // TinyFont: 3 high; 'A' 2 wide (##, #., ##), 'B' 1 wide (all on), space 2 wide
    private static FontDocument Tiny() => FontDocument.From("TINY", FontDrawingTests.TinyFont());

    private static string Rows(FontDocument font, int ch)
        => string.Join("|", Enumerable.Range(0, font.Height).Select(y =>
            new string([.. Enumerable.Range(0, font.Width(ch)).Select(x => font.Pixel(ch, x, y) ? '#' : '.')])));

    [Test]
    public void A_Font_File_Written_Reads_Back_The_Same()
    {
        var font = Tiny();
        var asset = FontAsset.FromFile(font.ToFile());
        Assert.That((asset.Height, asset.Width['A'], asset.Width['B'], asset.Width[' '], asset.Width['C']), Is.EqualTo(((short)3, (byte)2, (byte)1, (byte)2, (byte)0)));
        Assert.That(asset.Glyph('A'), Is.EqualTo(new byte[] { 1, 1, 1, 0, 1, 1 }));
        Assert.That(font.IsDirty, Is.False);
    }

    [Test]
    public void A_Stroke_Is_One_Step()
    {
        var font = Tiny();
        font.BeginEdit("Drew");
        font.SetPixel('A', 1, 1, true);
        font.Line('B', 0, 0, 0, 2, false);
        font.EndEdit();
        Assert.That((Rows(font, 'A'), Rows(font, 'B')), Is.EqualTo(("##|##|##", ".|.|.")));
        Assert.That(font.IsDirty, Is.True);
        font.Undo();
        Assert.That((Rows(font, 'A'), Rows(font, 'B'), font.IsDirty), Is.EqualTo(("##|#.|##", "#|#|#", false)));
        font.Redo();
        Assert.That(Rows(font, 'A'), Is.EqualTo("##|##|##"));
    }

    [Test]
    public void Glyphs_Widen_Move_Flip_And_Go()
    {
        var font = Tiny();
        font.SetWidth('A', 3);
        Assert.That(Rows(font, 'A'), Is.EqualTo("##.|#..|##."), "a column at the right");
        font.SetWidth('A', 4, atLeft: true);
        Assert.That(Rows(font, 'A'), Is.EqualTo(".##.|.#..|.##."), "a column at the left");
        font.Shift('A', -1, 0);
        Assert.That(Rows(font, 'A'), Is.EqualTo("##..|#...|##.."));
        font.Shift('A', 0, 1);
        Assert.That(Rows(font, 'A'), Is.EqualTo("....|##..|#..."), "the bottom row's pixels are lost");
        font.Flip('A', vertical: false);
        Assert.That(Rows(font, 'A'), Is.EqualTo("....|..##|...#"));
        font.Remove('A');
        Assert.That(font.Has('A'), Is.False);
        Assert.That(FontAsset.FromFile(font.ToFile()).Width['A'], Is.Zero);
        for (int i = 0; i < 6; i++)
            font.Undo();
        Assert.That((Rows(font, 'A'), font.IsDirty), Is.EqualTo(("##|#.|##", false)));
    }

    [Test]
    public void Glyphs_Paste_Between_Fonts_And_Missing_Ones_Come_From_Another()
    {
        var tall = FontDocument.Blank("TALL", 5, 3);
        tall.Paste('A', Tiny().Copy('A'));
        Assert.That(Rows(tall, 'A'), Is.EqualTo("##|#.|##|..|.."), "rows from the top, the rest empty");

        var copied = tall.CopyMissingFrom(FontDrawingTests.TinyFont());
        Assert.That(copied, Is.EqualTo(1), "B: A is there already, and so is the space");
        Assert.That(Rows(tall, 'B'), Is.EqualTo("#|#|#|.|."));
    }

    [Test]
    public void The_Font_Gets_Taller_Or_Shorter_At_The_Top_Or_Bottom()
    {
        var font = Tiny();
        font.SetHeight(4, atTop: true);
        Assert.That(Rows(font, 'A'), Is.EqualTo("..|##|#.|##"));
        font.SetHeight(2);
        Assert.That((font.Height, Rows(font, 'A')), Is.EqualTo((2, "..|##")));
        Assert.That(FontAsset.FromFile(font.ToFile()).Height, Is.EqualTo(2));
    }

    [Test]
    public void A_Font_Too_Big_For_Its_Offsets_Is_Not_Written()
    {
        var font = FontDocument.Blank("HUGE", 100, 4);
        var wide = new GlyphData(255, 100, new byte[255 * 100]);
        for (int ch = 'A'; ch <= 'Z'; ch++)
            font.Paste(ch, wide);
        var problem = Assert.Throws<InvalidDataException>(() => font.ToFile())!;
        Assert.That(problem.Message, Does.Contain("bytes too many"));
        Assert.That(font.ToAsset(), Is.Null);
    }
}

/// <summary>The games' fonts in the glyph editor, and one saved into a mod. Needs a built game folder with the data files.</summary>
[Category("GameData")]
public class FontDocumentGameTests
{
    private static GameContent Load(string game, params string[] mods)
    {
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        var folder = new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");

        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            return GameContent.Load(folder!, game, mods);
        }
        catch (PFWolf.Exceptions.DataFilesException)
        {
            Assert.Ignore($"{game}'s data files aren't in {folder}");
            throw;
        }
        finally
        {
            Directory.SetCurrentDirectory(workingFolder);
        }
    }

    [TestCase("wolf3d")]
    [TestCase("spear")]
    [TestCase("blake")]
    [TestCase("planetstrike")]
    public void Every_Font_Chunk_Comes_Back_The_Same_Through_The_Glyph_Editor(string game)
    {
        var content = Load(game);
        foreach (var name in FontCatalog.FontAssetNames(content))
        {
            var asset = content.Find<FontAsset>(name)!;
            var written = FontAsset.FromFile(FontDocument.From(name, asset).ToFile());
            Assert.That(written.Height, Is.EqualTo(asset.Height), name);
            for (int ch = 0; ch < 256; ch++)
            {
                Assert.That(written.Width[ch], Is.EqualTo(asset.Width[ch]), $"{name} {ch}");
                Assert.That(written.Glyph(ch), Is.EqualTo(asset.Glyph(ch)), $"{name} {ch}");
            }
        }
    }

    [Test]
    public void A_Picture_Fonts_Glyph_Opens_As_Its_Picture()
    {
        var fonts = new FontBrowserViewModel();
        fonts.Load(Load("wolf3d"));
        string? opened = null;
        fonts.PictureEditRequested += (_, picture) => opened = picture;

        // IntermissionFont: FONTL{code:000}, upper case, so a picks A's picture
        fonts.Show("IntermissionFont");
        Assert.That(fonts.EditGlyphsCommand.CanExecute(null), Is.False, "pictures, not a vga font's glyphs");
        Assert.That(fonts.EditGlyphPictureCommand.CanExecute(null), Is.False, "no character picked");
        fonts.PickedCharacter = 'a';
        fonts.EditGlyphPictureCommand.Execute(null);
        Assert.That(opened, Is.EqualTo("FONTL065"));
    }

    [Test]
    public void A_Font_Chunk_Drawn_And_Saved_Replaces_The_Games()
    {
        var mod = Path.Combine(Path.GetTempPath(), "pfwolf-glyphs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mod);
        try
        {
            // Arrange: SmallFont in the glyph editor, from the font window
            var content = Load("wolf3d");
            var fonts = new FontBrowserViewModel { PickMod = _ => Task.FromResult<string?>(mod) };
            fonts.Load(content);
            fonts.Show("SmallFont");
            GlyphEditorViewModel? editor = null;
            fonts.GlyphEditRequested += (_, opened) => editor = opened;
            fonts.EditGlyphsCommand.Execute(null);
            Assert.That(editor?.Name, Is.EqualTo("SmallFont"));
            editor!.TestText = "A A A";
            var before = editor.TestPicture!.Indices.ToArray();

            // Act: a pixel of A's top left corner on, a stroke; then saved
            editor.SelectedCode = 'A';
            var on = !editor.Document.Pixel('A', 0, 0);
            editor.Press(0, 0, erase: !on);
            editor.Release();
            Assert.That(editor.Document.IsDirty, Is.True);
            Assert.That(editor.TestPicture!.Indices, Is.Not.EqualTo(before), "the line of text is drawn with it");
            editor.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();

            // Assert: fonts/SmallFont.fnt, which the game reads in place of the chunk
            Assert.That(File.Exists(Path.Combine(mod, "fonts", "SmallFont.fnt")), Is.True);
            Assert.That(editor.Document.IsDirty, Is.False);
            var reloaded = Load("wolf3d", mod);
            var small = reloaded.Find<FontAsset>("SmallFont")!;
            Assert.That(small.Glyph('A')[0] != 0, Is.EqualTo(on));
            Assert.That(small.Glyph('B'), Is.EqualTo(content.Find<FontAsset>("SmallFont")!.Glyph('B')));
        }
        finally
        {
            Directory.Delete(mod, recursive: true);
        }
    }
}
