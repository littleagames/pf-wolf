using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.ViewModels;
using PFWolf.Fonts;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>Fonts as sheets of characters: a vga font exported as one, and sheets read back as fonts</summary>
public class FontSheetsTests
{
    // A grey ramp: index i is (i, i, i)
    private static readonly PaletteColor[] Grey = [.. Enumerable.Range(0, 256).Select(i => new PaletteColor { Red = (byte)i, Green = (byte)i, Blue = (byte)i })];

    /// <summary>The pixels a font draws a line of text in (the ink color), as a set</summary>
    internal static HashSet<(int, int)> Ink(Font font, string text, PaletteColor[] palette)
    {
        var canvas = new IndexedTextCanvas(font.Measure(text) + 4, font.Height + 4, palette);
        font.Draw(canvas, 2, 2, text, "200");
        var ink = new HashSet<(int, int)>();
        for (int y = 0; y < canvas.Height; y++)
            for (int x = 0; x < canvas.Width; x++)
                if (canvas.Pixels[y * canvas.Width + x] != 0)
                    ink.Add((x, y));
        return ink;
    }

    /// <summary>The sheet font an export's entry and picture make</summary>
    internal static Font SheetFont(byte[] png, string yaml, PaletteColor[] palette)
    {
        var (name, definition) = YamlDataEntryLoader.DeserializeQuietly<Dictionary<string, FontDefinition>>(yaml).Single();
        var picture = FontSheets.ToPicture(ImageDecoder.Decode(png), palette);
        var warnings = new List<string>();
        var font = new FontBuilder(_ => null, image => image == definition.Image ? picture : null, byte.Parse, warnings.Add).Build(name, definition)!;
        Assert.That(warnings, Is.Empty);
        return font;
    }

    [Test]
    public void A_Vga_Font_Exported_As_A_Sheet_Draws_The_Same()
    {
        var tiny = FontDrawingTests.TinyFont();
        var (png, yaml) = FontSheets.Export(tiny, "Tiny", Grey);

        // 16 cells across, 14 rows (32-255), each as wide as the widest glyph (2) and the font's height
        var image = ImageDecoder.Decode(png);
        Assert.That((image.Width, image.Height), Is.EqualTo((32, 42)));
        Assert.That(yaml, Does.Contain("type: sheet").And.Contain("first-char: 32").And.Contain("colorize: true").And.Contain("\"B\": 1"));

        var sheet = SheetFont(png, yaml, Grey);
        Assert.That(Ink(sheet, "AB A", Grey), Is.EquivalentTo(Ink(new VgaFont(tiny), "AB A", Grey)));
        Assert.That(sheet.Measure("AB A"), Is.EqualTo(new VgaFont(tiny).Measure("AB A")));
    }

    [Test]
    public void A_Sheet_Read_As_A_Vga_Font_Has_Its_Glyphs_Trimmed()
    {
        var (png, _) = FontSheets.Export(FontDrawingTests.TinyFont(), "Tiny", Grey);
        var font = FontSheets.ToVgaFont("Back", ImageDecoder.Decode(png), new SheetCells(2, 3, 32), threshold: 128, spacing: 0);
        Assert.That((font.Height, font.Width('A'), font.Width('B'), font.Width(' '), font.Has('C')), Is.EqualTo((3, 2, 1, 1, false)));
        Assert.That(font.Pixel('A', 1, 1), Is.False);
        Assert.That(font.Pixel('A', 1, 2), Is.True);

        var spaced = FontSheets.ToVgaFont("Back", ImageDecoder.Decode(png), new SheetCells(2, 3, 32), threshold: 128, spacing: 1);
        Assert.That(spaced.Width('B'), Is.EqualTo(2), "a column of gap after it");
    }

    [TestCase(128, 48, 8, 8, 32)]      // 16 x 6: characters 32-127
    [TestCase(160, 192, 10, 12, 0)]    // 16 x 16: all 256
    [TestCase(128, 112, 8, 8, 32)]     // 16 x 14: 32-255
    [TestCase(182, 51, 13, 17, 32)]    // the Fonts Demo's FDGOLD: 14 x 3 of 13 x 17
    public void A_Sheets_Cells_Are_Guessed(int width, int height, int cellWidth, int cellHeight, int firstChar)
        => Assert.That(FontSheets.Guess(width, height), Is.EqualTo(new SheetCells(cellWidth, cellHeight, firstChar)));

    [Test]
    public void A_Sheets_Background_Color_Is_Left_Out()
    {
        // FDGOLD: gold letters on a solid background, no see-through pixels
        var png = File.ReadAllBytes(Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "fonts-demo", "graphics", "FDGOLD.png"));
        var image = ImageDecoder.Decode(png);
        Assert.That(FontSheets.HasTransparency(image), Is.False);
        var corner = FontSheets.CornerColor(image)!.Value;

        var cells = new SheetCells(13, 17, 65);
        var keyed = FontSheets.SheetDefinition("FDGOLD", cells, proportional: true, colorize: false, key: corner);
        Assert.That(((YamlDotNet.RepresentationModel.YamlScalarNode)keyed["transparent"]).Value, Is.EqualTo($"#{corner.R:X2}{corner.G:X2}{corner.B:X2}"));

        // Proportional: with the background left out, A is trimmed to its letter; with it, the whole cell draws
        Font Build(YamlDotNet.RepresentationModel.YamlMappingNode node)
        {
            var definition = YamlDataEntryLoader.DeserializeQuietly<FontDefinition>(YamlTree.ToText(node));
            var picture = FontSheets.ToPicture(image, Grey);
            return new FontBuilder(_ => null, _ => picture, color => new IndexedTextCanvas(1, 1, Grey).ResolveColor(color), _ => { }).Build("FDGOLD", definition)!;
        }
        Assert.That(Build(keyed).Advance('A'), Is.LessThan(13));
        Assert.That(Build(FontSheets.SheetDefinition("FDGOLD", cells, proportional: true, colorize: false)).Advance('A'), Is.EqualTo(14));

        // Its cells aren't in code order: listed, each character is its own cell, as a sheet font and as a vga font
        const string order = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!?.,:-";
        var listed = cells with { Chars = order };
        var font = Build(FontSheets.SheetDefinition("FDGOLD", listed, proportional: true, colorize: false, key: corner));
        Assert.That((font.Has('0'), font.Has('-'), font.Has('a')), Is.EqualTo((true, true, false)));
        var vga = FontSheets.ToVgaFont("GOLD", image, listed, threshold: 64, spacing: 1, key: corner);
        Assert.That((vga.Has('Z'), vga.Has('9'), vga.Has('-'), vga.Has('a')), Is.EqualTo((true, true, true, false)));
    }
}

/// <summary>The games' fonts as sheets, and fonts brought into a mod. Needs a built game folder with the data files.</summary>
[Category("GameData")]
public class FontSheetsGameTests
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

    private string _mod = "";

    [SetUp]
    public void MakeMod()
    {
        _mod = Path.Combine(Path.GetTempPath(), "pfwolf-sheets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_mod);
    }

    [TearDown]
    public void RemoveMod() => Directory.Delete(_mod, recursive: true);

    [TestCase("wolf3d", "SmallFont")]
    [TestCase("wolf3d", "LargeFont")]
    [TestCase("blake", "Font2")]
    public void A_Game_Font_As_A_Sheet_Draws_The_Same(string game, string name)
    {
        var content = Load(game);
        var font = content.Find<FontAsset>(name)!;
        var (png, yaml) = FontSheets.Export(font, name, content.Palette);
        const string text = "The Quick Brown Fox 0123456789 !?.,";
        Assert.That(FontSheetsTests.Ink(FontSheetsTests.SheetFont(png, yaml, content.Palette), text, content.Palette),
            Is.EquivalentTo(FontSheetsTests.Ink(new VgaFont(font), text, content.Palette)));
    }

    [Test]
    public void A_Font_File_Added_To_A_Mod_Is_Printed_In_By_Its_Name()
    {
        var fonts = new FontBrowserViewModel
        {
            PickMod = _ => Task.FromResult<string?>(_mod),
            AskText = (_, _, _, check) => Task.FromResult<string?>(check("MyItalic") == null ? "MyItalic" : null),
        };
        fonts.Load(Load("wolf3d"));
        var file = Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "fonts-demo", "fonts", "FDITALIC.fnt");

        fonts.AddFontFileAsync(file, File.ReadAllBytes(file)).GetAwaiter().GetResult();

        Assert.That(File.Exists(Path.Combine(_mod, "fonts", "MyItalic.fnt")), Is.True, fonts.Status);
        var reloaded = Load("wolf3d", _mod);
        Assert.That(FontCatalog.Build(reloaded).Single(entry => entry.Name.Equals("MyItalic", StringComparison.OrdinalIgnoreCase)).IsDefined, Is.False,
            "a font file is printed in by its own name, with no fonts.yaml entry");
    }

    [Test]
    public void A_Sheet_Brought_In_Becomes_A_Sheet_Font_Or_A_Vga_Font()
    {
        // Arrange: SmallFont as a sheet, brought back in
        var content = Load("wolf3d");
        var (png, _) = FontSheets.Export(content.Find<FontAsset>("SmallFont")!, "SmallFont", content.Palette);
        var fonts = new FontBrowserViewModel { PickMod = _ => Task.FromResult<string?>(_mod) };
        fonts.Load(content);
        SheetImportViewModel? sheet = null;
        fonts.SheetImportRequested += (_, opened) => sheet = opened;
        fonts.ImportSheet("SmallSheet.png", png);
        Assert.That(sheet, Is.Not.Null, fonts.Status);
        Assert.That(sheet!.Cells, Is.EqualTo(new SheetCells(sheet.Cells.CellWidth, 10, 32)), "14 rows of 10-high cells from character 32");
        Assert.That(sheet.HasProblem, Is.False, sheet.Problem);
        Assert.That(sheet.PreviewPicture, Is.Not.Null);

        // As a vga font: to the glyph editor
        PFWolf.Editor.Editing.FontDocument? opened = null;
        var asVga = new SheetImportViewModel("SmallVga", png, ImageDecoder.Decode(png), content.Palette, null, (_, _) => null) { OpenVga = font => opened = font };
        asVga.AsVga = true;
        asVga.DoneCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.That((opened?.Name, opened?.Height, opened?.Has('A')), Is.EqualTo(("SmallVga", 10, (bool?)true)));

        // As a sheet font: its picture and fonts.yaml entry in the mod
        sheet.Colorize = true;
        sheet.DoneCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.That(File.Exists(Path.Combine(_mod, "graphics", "SmallSheet.png")), Is.True, fonts.Status);
        var reloaded = Load("wolf3d", _mod);
        var entry = FontCatalog.Build(reloaded).Single(entry => entry.Name == "SmallSheet");
        var problems = new List<string>();
        var canvas = new IndexedTextCanvas(1, 1, reloaded.Palette, reloaded.Assets.FindInGamePack<ColorThemeAsset>("colors"));
        Assert.That((entry.Type, FontCatalog.BuildFont(reloaded, entry, canvas, problems)?.Height), Is.EqualTo(("sheet", (int?)10)));
        Assert.That(problems, Is.Empty);
    }
}
