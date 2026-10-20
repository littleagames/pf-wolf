using PFWolf.Assets;
using PFWolf.Loaders;
using PFWolf.Managers;

namespace PFWolf.Tests;

/// <summary>graphicdefs: pictures cut out of sprites, walls and other pictures, found by name like any picture</summary>
public class CroppedGraphicTests
{
    // 4x4, each pixel's color its row-major index; the left column doesn't show
    private static SpriteAsset Sprite() => new()
    {
        Width = 4,
        Height = 4,
        RawData = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray(),
        OpacityMask = Enumerable.Range(0, 16).Select(i => i % 4 == 0 ? (byte)0 : (byte)1).ToArray(),
    };

    // 3 wide, 2 high, stored column by column: column x is (10x, 10x + 1)
    private static TextureAsset Wall(bool masked = false) => new()
    {
        Width = 3,
        Height = 2,
        RawData = [0, 1, 10, 11, 20, masked ? (byte)0xFF : (byte)21],
        Masked = masked,
    };

    // 2x2, every pixel showing
    private static GraphicAsset Picture(byte first = 100) => new([first, (byte)(first + 1), (byte)(first + 2), (byte)(first + 3)], 2, 2);

    private static GraphicDefinitionsAsset Definitions(string yaml)
        => new(YamlDataEntryLoader.Deserialize<Dictionary<string, GraphicDefinition>>(yaml));

    private static AssetManager Assets(string yaml, string? packYaml = null, params (string, Asset)[] more)
    {
        var assets = new AssetManager();
        var list = new List<(string, Asset)>
        {
            ("SPR", Sprite()), ("WALL", Wall()), ("MASKED", Wall(masked: true)), ("PIC", Picture()),
            ("graphicdefs", Definitions(yaml)),
        };
        if (packYaml != null)
            list.Add(("alpha/graphicdefs", Definitions(packYaml)));
        list.AddRange(more);
        assets.UseAssets("alpha", list);
        return assets;
    }

    [Test]
    public void A_Sprite_Crop_Keeps_Its_Colors_And_What_Doesnt_Show()
    {
        // Act
        var assets = Assets("HEAD:\n  source: SPR\n  x: 0\n  y: 1\n  width: 2\n  height: 2\n");
        var head = assets.Find<GraphicAsset>("HEAD")!;

        // Assert: rows 1-2, columns 0-1; column 0 is see-through
        Assert.That((head.Width, head.Height), Is.EqualTo(((short)2, (short)2)));
        Assert.That(head.RawData[1], Is.EqualTo(5));
        Assert.That(head.RawData[3], Is.EqualTo(9));
        Assert.That(head.OpacityMask, Is.EqualTo(new byte[] { 0, 1, 0, 1 }));
        Assert.That(assets.CropProblems, Is.Empty);
    }

    [Test]
    public void A_Wall_Crop_Is_Read_Column_By_Column_Into_Rows()
    {
        // Act
        var assets = Assets("STRIP:\n  source: WALL\n  x: 1\n  y: 0\n  width: 2\n  height: 2\n");
        var strip = assets.Find<GraphicAsset>("STRIP")!;

        // Assert: row 0 is (10, 20), row 1 is (11, 21), and a wall shows everywhere
        Assert.That(strip.RawData, Is.EqualTo(new byte[] { 10, 20, 11, 21 }));
        Assert.That(strip.OpacityMask, Is.Null);
    }

    [Test]
    public void A_Masked_Walls_Clear_Pixels_Dont_Show()
    {
        // Act
        var strip = Assets("STRIP:\n  source: MASKED\n  x: 2\n  y: 0\n  width: 1\n  height: 2\n").Find<GraphicAsset>("STRIP")!;

        // Assert
        Assert.That(strip.OpacityMask, Is.EqualTo(new byte[] { 1, 0 }));
    }

    [Test]
    public void A_Background_Fills_What_Doesnt_Show()
    {
        // Act
        var head = Assets("HEAD:\n  source: SPR\n  x: 0\n  y: 0\n  width: 2\n  height: 1\n  background: 42\n").Find<GraphicAsset>("HEAD")!;

        // Assert
        Assert.That(head.RawData, Is.EqualTo(new byte[] { 42, 1 }));
        Assert.That(head.OpacityMask, Is.Null);
    }

    [Test]
    public void A_Crop_Past_The_Edge_Is_See_Through_There_With_A_Problem()
    {
        // Act
        var assets = Assets("BIG:\n  source: PIC\n  x: 1\n  y: 1\n  width: 2\n  height: 2\n");
        var big = assets.Find<GraphicAsset>("BIG")!;

        // Assert: only the source's bottom right pixel is in it
        Assert.That(big.RawData[0], Is.EqualTo(103));
        Assert.That(big.OpacityMask, Is.EqualTo(new byte[] { 1, 0, 0, 0 }));
        Assert.That(assets.CropProblems["BIG"], Does.Contain("runs past"));
    }

    [TestCase("source: NOPE\n  width: 1\n  height: 1", "no sprite, texture or picture named NOPE")]
    [TestCase("source: PIC\n  kind: sprite\n  width: 1\n  height: 1", "no sprite named PIC")]
    [TestCase("source: PIC\n  kind: flat\n  width: 1\n  height: 1", "isn't sprite, texture or graphic")]
    [TestCase("source: PIC\n  width: 0\n  height: 1", "more than 0")]
    [TestCase("source: PIC\n  x: 5\n  width: 1\n  height: 1", "outside PIC")]
    [TestCase("width: 1\n  height: 1", "no source")]
    public void A_Crop_That_Cant_Be_Made_Is_Left_Out(string fields, string problem)
    {
        // Act
        var assets = Assets($"BAD:\n  {fields}\n");

        // Assert
        Assert.That(assets.Exists<GraphicAsset>("BAD"), Is.False);
        Assert.That(assets.CropProblems["BAD"], Does.Contain(problem));
    }

    [Test]
    public void Without_A_Kind_A_Sprite_Is_Taken_Before_A_Picture_Of_The_Same_Name()
    {
        // Arrange: SPR is a picture too
        var yaml = "A:\n  source: SPR\n  x: 1\n  width: 1\n  height: 1\nB:\n  source: SPR\n  kind: graphic\n  x: 1\n  width: 1\n  height: 1\n";

        // Act
        var assets = Assets(yaml, more: ("SPR", Picture(200)));

        // Assert
        Assert.That(assets.Find<GraphicAsset>("A")!.RawData[0], Is.EqualTo(1));
        Assert.That(assets.Find<GraphicAsset>("B")!.RawData[0], Is.EqualTo(201));
    }

    [Test]
    public void The_Running_Packs_Crops_Replace_The_Shared_Ones()
    {
        // Act
        var assets = Assets("HEAD:\n  source: PIC\n  width: 1\n  height: 1\n",
            packYaml: "HEAD:\n  source: PIC\n  x: 1\n  width: 1\n  height: 1\n");

        // Assert
        Assert.That(assets.Find<GraphicAsset>("HEAD")!.RawData[0], Is.EqualTo(101));
    }

    [Test]
    public void A_Crop_Can_Take_The_Name_Of_Its_Own_Source()
    {
        // Act
        var assets = Assets("PIC:\n  source: PIC\n  x: 1\n  y: 1\n  width: 1\n  height: 1\n");

        // Assert
        Assert.That(assets.Find<GraphicAsset>("PIC")!.RawData, Is.EqualTo(new byte[] { 103 }));
        Assert.That(assets.FindAssetOrigins("PIC").Single().Origins[^1].Action, Is.EqualTo("cropped from PIC"));
    }

    [Test]
    public void An_Animation_Can_Use_Crops_As_Frames()
    {
        // Act
        var assets = Assets("LEFT:\n  source: PIC\n  width: 1\n  height: 1\nRIGHT:\n  source: PIC\n  x: 1\n  width: 1\n  height: 1\n",
            more: ("animdefs", new AnimationDefinitionsAsset(
                YamlDataEntryLoader.Deserialize<Dictionary<string, AnimationDefinition>>("LOOK:\n  tics: 10\n  frames: [LEFT, RIGHT]\n"))));

        // Assert
        Assert.That(assets.Find<GraphicAsset>("LOOK", 0)!.RawData[0], Is.EqualTo(100));
        Assert.That(assets.Find<GraphicAsset>("LOOK", 10)!.RawData[0], Is.EqualTo(101));
    }

    [Test]
    public void Mods_Graphicdefs_Merge_Into_The_Packs_And_Can_Remove_One()
    {
        // Arrange
        var basePk3 = new MemoryAssetSource("pfwolf.pk3", new()
        {
            ["gamepacks/gamepack-info.yaml"] = "alpha:\n  title: Alpha\n  game-palette: pal\n",
            ["graphicdefs/alpha/graphicdefs.yaml"] = "HEAD:\n  source: A\nFACE:\n  source: B\n",
        });
        var mod = new MemoryAssetSource("mod", new()
        {
            ["graphicdefs/mine.yaml"] = "HEAD: !remove\nFACE:\n  x: 4\nNEW:\n  source: C\n",
        });

        // Act
        var graphics = new PfWolfPk3Loader([basePk3], "alpha", "alpha", [mod]).Load<GraphicDefinitionsAsset>("alpha/graphicdefs").Graphics;

        // Assert: FACE keeps its source and takes the mod's x
        Assert.That(graphics.Keys, Is.EquivalentTo(new[] { "FACE", "NEW" }));
        Assert.That((graphics["FACE"].Source, graphics["FACE"].X), Is.EqualTo(("B", 4)));
    }

    [Test]
    public void A_Palettes_Closest_Index_Is_The_Nearest_Color()
    {
        // Arrange
        var palette = new Palette { Colors = [new(0, 0, 0), new(255, 0, 0), new(0, 0, 250)] };

        // Act / Assert
        Assert.That(palette.ClosestIndex(200, 10, 10), Is.EqualTo(1));
        Assert.That(palette.ClosestIndex(0, 0, 255), Is.EqualTo(2));
        Assert.That(palette.ClosestIndex(5, 5, 5), Is.EqualTo(0));
    }
}
