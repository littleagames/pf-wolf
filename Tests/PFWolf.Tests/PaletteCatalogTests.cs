using System.Text;
using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;

namespace PFWolf.Tests;

/// <summary>The palette browser's colors, palette files and editing</summary>
public class PaletteCatalogTests
{
    private static PaletteColor[] Ramp() => Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)(255 - i), (byte)(i / 2))).ToArray();

    private static string Rgb(PaletteColor color) => PaletteCatalog.Hex(color);

    [Test]
    public void Gradient_Blends_Between_The_Ends_Either_Way_Round()
    {
        // Arrange
        var colors = new PaletteColor[256];
        colors[10] = new PaletteColor(0, 0, 0);
        colors[14] = new PaletteColor(200, 100, 40);

        // Act
        PaletteCatalog.Gradient(colors, 14, 10);

        // Assert
        Assert.That(colors[10..15].Select(Rgb), Is.EqualTo(new[] { "#000000", "#32190A", "#643214", "#964B1E", "#C86428" }));
    }

    [Test]
    public void Closest_Finds_An_Exact_Color_Or_The_Nearest()
    {
        // Arrange
        var colors = new PaletteColor[256];
        colors[7] = new PaletteColor(250, 10, 10);
        colors[9] = new PaletteColor(240, 20, 20);

        // Act / Assert
        Assert.That(PaletteCatalog.Closest(colors, 240, 20, 20), Is.EqualTo(9));
        Assert.That(PaletteCatalog.Closest(colors, 255, 0, 0), Is.EqualTo(7));
        Assert.That(PaletteCatalog.Closest(colors, 5, 5, 5), Is.EqualTo(0));
    }

    [TestCase("#FC0000", true, 252, 0, 0)]
    [TestCase("fc0000", true, 252, 0, 0)]
    [TestCase("#f00", true, 255, 0, 0)]
    [TestCase("#GG0000", false, 0, 0, 0)]
    [TestCase("12", false, 0, 0, 0)]
    public void TryParseHex_Reads_Six_And_Three_Digit_Colors(string text, bool valid, int red, int green, int blue)
    {
        // Act
        var parsed = PaletteCatalog.TryParseHex(text, out var color);

        // Assert
        Assert.That(parsed, Is.EqualTo(valid));
        if (valid)
            Assert.That((color.Red, color.Green, color.Blue), Is.EqualTo(((byte)red, (byte)green, (byte)blue)));
    }

    [TestCase(PaletteCatalog.FileFormat.Raw)]
    [TestCase(PaletteCatalog.FileFormat.Jasc)]
    [TestCase(PaletteCatalog.FileFormat.Gimp)]
    public void Every_Format_Reads_Back_What_It_Wrote(PaletteCatalog.FileFormat format)
    {
        // Arrange
        var colors = Ramp();

        // Act
        var (read, _) = PaletteCatalog.Read(PaletteCatalog.Write(colors, format, "TEST"));

        // Assert
        Assert.That(read.Select(Rgb), Is.EqualTo(colors.Select(Rgb)));
    }

    [Test]
    public void A_Raw_Palette_Of_6_Bit_Values_Is_Scaled_Up()
    {
        // Arrange: VGA DAC values, as id's palettes hold them
        var data = new byte[768];
        data[0] = 63;
        data[4] = 42;

        // Act
        var (colors, how) = PaletteCatalog.Read(data);

        // Assert
        Assert.That(Rgb(colors[0]), Is.EqualTo("#FF0000"));
        Assert.That(colors[1].Green, Is.EqualTo(170));
        Assert.That(how, Does.Contain("6-bit"));
    }

    [Test]
    public void An_Adobe_Act_Palette_Is_Read_And_A_Short_Text_Palette_Fills_The_Start()
    {
        // Arrange
        var act = PaletteCatalog.ToRaw(Ramp()).Concat(new byte[] { 1, 0, 255, 255 }).ToArray();
        var gimp = Encoding.ASCII.GetBytes("GIMP Palette\nName: two\n#\n255 0 0\tred\n0 0 255\tblue\n");

        // Act
        var (fromAct, _) = PaletteCatalog.Read(act);
        var (fromGimp, _) = PaletteCatalog.Read(gimp);

        // Assert
        Assert.That(Rgb(fromAct[200]), Is.EqualTo(Rgb(Ramp()[200])));
        Assert.That(fromGimp.Take(3).Select(Rgb), Is.EqualTo(new[] { "#FF0000", "#0000FF", "#000000" }));
    }

    [Test]
    public void A_File_That_Isnt_A_Palette_Is_Refused()
    {
        Assert.Throws<InvalidDataException>(() => PaletteCatalog.Read(new byte[100]));
        Assert.Throws<InvalidDataException>(() => PaletteCatalog.Read(Encoding.ASCII.GetBytes("JASC-PAL\r\n0100\r\n256\r\n1 2\r\n")));
    }

    [Test]
    public void Document_Undoes_A_Slider_Drag_As_One_Step_And_Knows_When_Its_Saved()
    {
        // Arrange
        var document = new PaletteDocument("TEST", Ramp());
        var folder = Path.Combine(Path.GetTempPath(), "pfwolf-palette-test-" + Guid.NewGuid().ToString("N"));

        try
        {
            // Act: a drag through three values, then a separate edit
            foreach (byte red in new byte[] { 10, 20, 30 })
                document.Edit("Change color 5", colors => colors[5] = new PaletteColor(red, 0, 0), running: "color 5");
            document.EndRunningEdit();
            document.Edit("Change color 6", colors => colors[6] = new PaletteColor(1, 2, 3));

            // Assert
            Assert.That(document.IsDirty, Is.True);
            document.Undo();
            Assert.That(Rgb(document.Colors[6]), Is.EqualTo(Rgb(Ramp()[6])));
            Assert.That(document.Colors[5].Red, Is.EqualTo(30));
            document.Undo();
            Assert.That(Rgb(document.Colors[5]), Is.EqualTo(Rgb(Ramp()[5])), "the whole drag undone at once");
            Assert.That(document.IsDirty, Is.False);
            Assert.That(document.CanUndo, Is.False);

            document.Redo();
            var path = document.Save(folder);
            Assert.That(path, Is.EqualTo(Path.Combine(folder, "palettes", "test.pal")));
            Assert.That(document.IsDirty, Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(PaletteCatalog.ToRaw(document.Colors)));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
