using PFWolf.Assets;
using PFWolf.Fonts;

namespace PFWolf.Tests;

/// <summary>Fonts built and drawn by the shared code (PFWolf.Common/Fonts) onto an editor picture</summary>
public class FontDrawingTests
{
    // A grey ramp: index i is (i, i, i), so nearest colors are easy to reason about
    private static readonly PaletteColor[] Grey = [.. Enumerable.Range(0, 256).Select(i => new PaletteColor { Red = (byte)i, Green = (byte)i, Blue = (byte)i })];

    /// <summary>A vga font 3 high: 'A' a 2-wide block with a hole, 'B' one column, space 2 wide and blank</summary>
    internal static FontAsset TinyFont()
    {
        var glyphs = new Dictionary<char, byte[]>
        {
            ['A'] = [1, 1, 1, 0, 1, 1],   // rows of 2: ##, #., ##
            ['B'] = [1, 1, 1],            // rows of 1
            [' '] = [0, 0, 0, 0, 0, 0],
        };
        var widths = new Dictionary<char, byte> { ['A'] = 2, ['B'] = 1, [' '] = 2 };
        var data = new List<byte>(new byte[2 + 512 + 256]);
        data[0] = 3;   // the height, 16-bit
        foreach (var (ch, pixels) in glyphs)
        {
            int at = data.Count;
            data[2 + ch * 2] = (byte)at;
            data[2 + ch * 2 + 1] = (byte)(at >> 8);
            data[2 + 512 + ch] = widths[ch];
            data.AddRange(pixels);
        }
        return FontAsset.FromFile([.. data]);
    }

    private static string Picture(IndexedTextCanvas canvas, byte ink)
        => string.Join("|", Enumerable.Range(0, canvas.Height).Select(y =>
            new string([.. Enumerable.Range(0, canvas.Width).Select(x => canvas.Pixels[y * canvas.Width + x] == ink ? '#' : '.')])));

    [Test]
    public void A_Vga_Font_Draws_Its_Glyphs_In_The_Text_Color()
    {
        var canvas = new IndexedTextCanvas(6, 3, Grey);
        new VgaFont(TinyFont()).Draw(canvas, 0, 0, "A B", "200");
        Assert.That(Picture(canvas, 200), Is.EqualTo("##..#.|#...#.|##..#."));
        Assert.That(new VgaFont(TinyFont()).Measure("A B"), Is.EqualTo(5));
    }

    [Test]
    public void Shadows_And_Outlines_Go_Behind_The_Text()
    {
        var font = new VgaFont(TinyFont()) { Shadow = new FontShadow(1, 1, "50") };
        var canvas = new IndexedTextCanvas(4, 4, Grey);
        font.Draw(canvas, 0, 0, "B", "200");
        Assert.That(Picture(canvas, 200), Is.EqualTo("#...|#...|#...|...."));
        Assert.That(Picture(canvas, 50), Is.EqualTo("....|.#..|.#..|.#.."), "the shadow shows only where the text doesn't cover it");

        var outlined = new VgaFont(TinyFont()) { Outline = new FontOutline("50", 1, Diagonals: false) };
        canvas = new IndexedTextCanvas(3, 5, Grey);
        outlined.Draw(canvas, 1, 1, "B", "200");
        Assert.That(Picture(canvas, 50), Is.EqualTo(".#.|#.#|#.#|#.#|.#."));
    }

    [Test]
    public void Gradients_And_Glows_Pick_Palette_Colors()
    {
        var canvas = new IndexedTextCanvas(1, 1, Grey);
        // 100 grey: 50% toward white at the top is 177.5, 50% toward black at the bottom 50
        Assert.That(canvas.GetGradient("100", 3, 50, -50), Is.EqualTo(new byte[] { 177, 100, 50 }));
        Assert.That(canvas.GetGlowColors("200", "0", 2, 50), Is.EqualTo(new[] { "100", "50" }));
    }

    [Test]
    public void Colors_Resolve_As_The_Game_Does()
    {
        var theme = new ColorThemeAsset(new Dictionary<string, string> { ["MIDGREY"] = "#808080" });
        var canvas = new IndexedTextCanvas(1, 1, Grey, theme);
        Assert.That(canvas.ResolveColor("MIDGREY"), Is.EqualTo(128));
        Assert.That(canvas.ResolveColor("#0A0A0A"), Is.EqualTo(10));
        Assert.That(canvas.ResolveColor("42"), Is.EqualTo(42));
        Assert.That(canvas.ResolveColor("Nonsense"), Is.Zero);
        Assert.That(canvas.UnknownColors, Is.EquivalentTo(new[] { "Nonsense" }));
    }

    private static GraphicAsset Pic(int width, int height, byte[] pixels) => new(pixels, (short)width, (short)height);

    [Test]
    public void The_Builder_Makes_Graphic_Sheet_And_Vga_Fonts()
    {
        var pictures = new Dictionary<string, GraphicAsset>(StringComparer.OrdinalIgnoreCase)
        {
            ["DIG048"] = Pic(2, 2, [9, 9, 9, 9]),
            // A sheet of two 4x2 cells; the second has pixels only in its middle two columns
            ["SHEET"] = Pic(8, 2, [9, 9, 9, 9, 0, 9, 9, 0, 9, 9, 9, 9, 0, 9, 9, 0]),
        };
        var warnings = new List<string>();
        var builder = new FontBuilder(name => name == "Tiny" ? TinyFont() : null, name => pictures.GetValueOrDefault(name), color => byte.Parse(color), warnings.Add);

        var digits = builder.Build("Digits", new FontDefinition { Type = "graphic", Pattern = "DIG{code:000}", Chars = "01", CellWidth = 3 });
        Assert.That(digits!.Advance('0'), Is.EqualTo(3));
        Assert.That(warnings, Has.One.Contains("DIG049"), "a missing picture is reported, the rest of the font still made");

        var sheet = builder.Build("Sheet", new FontDefinition { Type = "sheet", Image = "SHEET", CellWidth = 4, CellHeight = 2, Chars = "ab", Proportional = true, Transparent = "0" });
        Assert.That((sheet!.Advance('a'), sheet.Advance('b')), Is.EqualTo((5, 3)), "proportional: the columns drawn plus spacing 1");

        var shouting = builder.Build("Shouting", new FontDefinition { Type = "vga", Source = "Tiny", UpperCase = true, Shadow = new ShadowDefinition() });
        Assert.That(shouting!.Advance('a'), Is.EqualTo(2), "lower case uses the capitals");
        Assert.That(shouting.Shadow, Is.EqualTo(FontShadow.Default));

        Assert.That(builder.Build("Tiny", null), Is.TypeOf<VgaFont>(), "no definition: the font asset of the name");
        Assert.That(builder.Build("Odd", new FontDefinition { Type = "ttf" }), Is.Null);
        Assert.That(warnings, Has.Some.Contains("unknown type 'ttf'"));
    }

    [Test]
    public void Graphic_Glyph_Pictures_Come_From_The_Pattern_Then_The_List()
    {
        var definition = new FontDefinition { Pattern = "FONTN{code:000}", Chars = " 0", Glyphs = new() { ["0"] = "ZERO", ["xy"] = "BAD" } };
        var warnings = new List<string>();
        var pictures = FontBuilder.GlyphPictures(definition, warnings.Add, "Numbers");
        Assert.That(pictures, Is.EquivalentTo(new Dictionary<char, string> { [' '] = "FONTN032", ['0'] = "ZERO" }));
        Assert.That(warnings, Has.One.Contains("'xy'"));
    }
}

/// <summary>Every font each game's fonts.yaml names builds with no problem and draws. Needs a built game folder with the data files.</summary>
[Category("GameData")]
public class FontGameTests
{
    private static PFWolf.Editor.Data.GameContent Load(string game)
    {
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        var folder = new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");

        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            return PFWolf.Editor.Data.GameContent.Load(folder!, game, []);
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
    [TestCase("wolf3d-shareware")]
    [TestCase("blake")]
    [TestCase("planetstrike")]
    public void Every_Defined_Font_Builds_And_Draws(string game)
    {
        var content = Load(game);
        var definitions = content.Assets.FindInGamePack<FontDefinitionsAsset>("fonts")!;
        var warnings = new List<string>();
        var canvas = new IndexedTextCanvas(320, 40, content.Palette, content.Assets.FindInGamePack<ColorThemeAsset>("colors"));
        var builder = new FontBuilder(content.Find<FontAsset>, name => content.Assets.Exists<GraphicAsset>(name) ? content.Find<GraphicAsset>(name) : null,
            canvas.ResolveColor, warnings.Add);

        Assert.That(definitions.Fonts, Is.Not.Empty);
        foreach (var (name, definition) in definitions.Fonts)
        {
            var font = builder.Build(name, definition);
            Assert.That(font, Is.Not.Null, $"{game} {name}");
            Array.Clear(canvas.Pixels);
            font!.Draw(canvas, 2, 2, "0123 ABC", "15");
            Assert.That(canvas.Pixels.Any(pixel => pixel != 0), Is.True, $"{game} {name} drew nothing");
        }
        Assert.That(warnings, Is.Empty);
        Assert.That(canvas.UnknownColors, Is.Empty);
    }
}
