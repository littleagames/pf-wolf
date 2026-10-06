using PFWolf.Entities;

namespace PFWolf.Tests;

public class ColorTests
{
    [Test]
    public void FromByteRGB_Reads_Three_Bytes_And_Is_Opaque()
    {
        // Act
        var color = Color.FromByteRGB("255 128 0");

        // Assert
        Assert.That(color, Is.EqualTo(new Color { Red = 255, Green = 128, Blue = 0, Alpha = 255 }));
    }

    [TestCase("")]
    [TestCase("1 2")]
    [TestCase("1 2 3 4")]
    public void FromByteRGB_Rejects_The_Wrong_Shape(string value)
    {
        Assert.Throws<ArgumentException>(() => Color.FromByteRGB(value));
    }

    [Test]
    public void FromByteRGB_Rejects_Values_Over_255()
    {
        Assert.Throws<OverflowException>(() => Color.FromByteRGB("256 0 0"));
    }

    [TestCase("#FFD080", 0xFF, 0xD0, 0x80, 0xFF)]
    [TestCase("ffd080", 0xFF, 0xD0, 0x80, 0xFF)]
    [TestCase("#10203040", 0x10, 0x20, 0x30, 0x40)]
    public void FromHexRGBA_Reads_Six_Or_Eight_Digits(string value, int r, int g, int b, int a)
    {
        // Act
        var color = Color.FromHexRGBA(value);

        // Assert
        Assert.That(color, Is.EqualTo(new Color { Red = (byte)r, Green = (byte)g, Blue = (byte)b, Alpha = (byte)a }));
    }

    [TestCase("")]
    [TestCase("#FFF")]
    [TestCase("#1234567")]
    public void FromHexRGBA_Rejects_Other_Lengths(string value)
    {
        Assert.Throws<ArgumentException>(() => Color.FromHexRGBA(value));
    }
}
