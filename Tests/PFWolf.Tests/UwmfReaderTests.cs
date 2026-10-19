using PFWolf.Loaders.Uwmf;

namespace PFWolf.Tests;

public class UwmfReaderTests
{
    private const string Small = """
        // A 3x2 level
        namespace = "Wolf3D";
        name = "Small \"one\"";
        tilesize = 64;
        width = 3;
        height = 2;
        tile { texturenorth = "GSTONEA1"; texturesouth = "GSTONEA1"; textureeast = "GSTONEA2"; texturewest = "GSTONEA2"; }
        sector { texturefloor = "#707070"; textureceiling = "#383838"; }
        zone { }
        plane { depth = 64; }
        /* the planemap: a wall, then
           open floor */
        planemap
        {
            {0, 0, -1}, {-1, 0, 0}, {-1, 0, 0, 7},
            {0, 0, -1}, {0, -1, -1}, {-1, 0, 0}
        }
        thing { x = 1.5; y = 0.5; type = "Guard"; angle = 90; skill1 = true; ambush = false; }
        trigger { x = 0; y = 1; action = "Pushwall_Move"; arg1 = 8; secret = true; playeruse = true; }
        someotherblock { a = 1; nested { b = 2; } }
        """;

    [Test]
    public void A_Textmap_Reads_Into_Its_Header_Palettes_Planemap_Things_And_Triggers()
    {
        // Act
        var map = UwmfReader.Read(Small);

        // Assert
        Assert.That(map.Namespace, Is.EqualTo("Wolf3D"));
        Assert.That(map.Name, Is.EqualTo("Small \"one\""));
        Assert.That((map.Width, map.Height, map.TileSize), Is.EqualTo((3, 2, 64)));
        Assert.That(map.Tiles, Has.Count.EqualTo(1));
        Assert.That(map.Tiles[0].String("TextureEast"), Is.EqualTo("GSTONEA2"));
        Assert.That(map.Sectors[0].String("texturefloor"), Is.EqualTo("#707070"));
        Assert.That(map.Zones, Has.Count.EqualTo(1));
        Assert.That(map.Planes[0].Int("depth"), Is.EqualTo(64));
        Assert.That(map.PlaneMaps, Has.Count.EqualTo(1));
        Assert.That(map.PlaneMaps[0], Is.EqualTo(new[]
        {
            new UwmfSpot(0, 0, -1, 0), new UwmfSpot(-1, 0, 0, 0), new UwmfSpot(-1, 0, 0, 7),
            new UwmfSpot(0, 0, -1, 0), new UwmfSpot(0, -1, -1, 0), new UwmfSpot(-1, 0, 0, 0),
        }));
        Assert.That(map.Things[0].Double("x"), Is.EqualTo(1.5));
        Assert.That(map.Things[0].Int("angle"), Is.EqualTo(90));
        Assert.That(map.Things[0].Bool("skill1"), Is.True);
        Assert.That(map.Things[0].Bool("skill2"), Is.False);
        Assert.That(map.Triggers[0].String("action"), Is.EqualTo("Pushwall_Move"));
        Assert.That(map.Triggers[0].Bool("secret"), Is.True);
        Assert.That(map.UnknownBlocks, Is.EqualTo(new[] { "someotherblock" }));
    }

    [Test]
    public void Numbers_Can_Be_Negative_Hexadecimal_Or_Decimal()
    {
        var map = UwmfReader.Read("""
            a = -5; b = 0x1F; c = 2.5; d = -0.25; e = 1e2;
            """);

        Assert.That(map.Header.Int("a"), Is.EqualTo(-5));
        Assert.That(map.Header.Int("b"), Is.EqualTo(31));
        Assert.That(map.Header.Double("c"), Is.EqualTo(2.5));
        Assert.That(map.Header.Double("d"), Is.EqualTo(-0.25));
        Assert.That(map.Header.Double("e"), Is.EqualTo(100));
    }

    [TestCase("width = 2; height = 1; planemap { {0, 0, 0} }", "planemap has 1 spots")]
    [TestCase("width = 1; height = 1; planemap { {0, 0, 0}, {0, 0, 0} }", "more than")]
    [TestCase("tile { texturenorth = ; }", "line 1: expected a value")]
    [TestCase("name = \"no end;", "no closing quote")]
    [TestCase("width = 1; height = 1;\nplanemap { {0, 0, 0} }\nwidth = 2;", "line 3")]
    [TestCase("tile { a = 1 }", "expected ';'")]
    public void Mistakes_Are_Refused_With_Their_Line(string text, string message)
    {
        Assert.That(() => UwmfReader.Read(text), Throws.InstanceOf<InvalidDataException>().With.Message.Contains(message));
    }

    [Test]
    public void A_Later_Key_Wins_And_Keys_Match_Whatever_Their_Case()
    {
        var map = UwmfReader.Read("tile { offsetvertical = false; OffsetVertical = true; }");

        Assert.That(map.Tiles[0].Bool("offsetvertical"), Is.True);
    }

    [Test]
    public void Values_Write_Back_As_They_Read()
    {
        Assert.That(new UwmfValue("a \"b\" \\c").ToText(), Is.EqualTo("\"a \\\"b\\\" \\\\c\""));
        Assert.That(new UwmfValue(2.5).ToText(), Is.EqualTo("2.5"));
        Assert.That(new UwmfValue(29.0).ToText(), Is.EqualTo("29.0"));
        Assert.That(new UwmfValue(-3).ToText(), Is.EqualTo("-3"));
        Assert.That(new UwmfValue(true).ToText(), Is.EqualTo("true"));
        Assert.That(UwmfReader.Read("a = " + new UwmfValue("x\"y").ToText() + ";").Header.String("a"), Is.EqualTo("x\"y"));
    }
}
