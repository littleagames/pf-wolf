using PFWolf.Assets;

namespace PFWolf.Tests;

/// <summary>Font files in a pk3's fonts/: Wolf3D's chunk format, and WDC's .wsf</summary>
public class FontFileTests
{
    [Test]
    public void A_Wdc_Wsf_Font_Reads_As_The_Same_Glyphs()
    {
        // Arrange: "WSF1.0", height 2, widths ('A' 2, 'B' 1), then A's rows then B's
        var wsf = new List<byte>("WSF1.0"u8.ToArray()) { 2 };
        var widths = new byte[256];
        widths['A'] = 2;
        widths['B'] = 1;
        wsf.AddRange(widths);
        wsf.AddRange([0xff, 0x00, 0x00, 0xff]);    // A: a diagonal
        wsf.AddRange([0xff, 0xff]);                // B: a bar

        // Act
        var font = FontAsset.FromFile(wsf.ToArray());

        // Assert
        Assert.That(font.Height, Is.EqualTo(2));
        Assert.That(font.Width['A'], Is.EqualTo(2));
        Assert.That(font.Width['B'], Is.EqualTo(1));
        Assert.That(font.RawData.AsSpan(font.Location['A'], 4).ToArray(), Is.EqualTo(new byte[] { 0xff, 0x00, 0x00, 0xff }));
        Assert.That(font.RawData.AsSpan(font.Location['B'], 2).ToArray(), Is.EqualTo(new byte[] { 0xff, 0xff }));
    }

    [Test]
    public void A_Wsf_Font_Too_Short_For_Its_Glyphs_Is_Refused()
    {
        // Arrange: widths that promise more glyph bytes than there are
        var wsf = new List<byte>("WSF1.0"u8.ToArray()) { 8 };
        var widths = new byte[256];
        widths['A'] = 8;
        wsf.AddRange(widths);
        wsf.AddRange(new byte[10]);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => FontAsset.FromFile(wsf.ToArray()));
    }
}
