using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>translations.yaml: sprite color translations, each built into a table of 256 palette indices</summary>
public class TranslationTests
{
    // A grey ramp (0 black .. 255 white) in the first 256 entries, but for a few pure colors
    private static PaletteColor[] Palette()
    {
        var palette = Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i)).ToArray();
        palette[10] = new PaletteColor(255, 0, 0);
        palette[11] = new PaletteColor(128, 0, 0);
        palette[12] = new PaletteColor(0, 0, 255);
        return palette;
    }

    private static byte[] Build(params string[] ranges)
    {
        var warnings = new List<string>();
        var table = TranslationTable.Build(ranges, Palette(), "test", warnings);
        Assert.That(warnings, Is.Empty);
        return table;
    }

    [Test]
    public void An_Index_Range_Maps_Evenly_Onto_Another()
    {
        // Act
        var table = Build("100:103=200:206");

        // Assert
        Assert.That(table[100..104], Is.EqualTo(new byte[] { 200, 202, 204, 206 }));
        Assert.That(table[99], Is.EqualTo(99), "the rest stay as they are");
        Assert.That(table[104], Is.EqualTo(104));
    }

    [Test]
    public void A_Range_Can_Run_Either_Way_And_Be_One_Index()
    {
        // Act
        var table = Build("100:103=206:200", "50=60");

        // Assert
        Assert.That(table[100..104], Is.EqualTo(new byte[] { 206, 204, 202, 200 }));
        Assert.That(table[50], Is.EqualTo(60));
    }

    [Test]
    public void Each_Range_Starts_From_The_Palettes_Own_Colors()
    {
        // Act: the second range reads 100's own color, not what the first made of it
        var table = Build("100=200", "100:101=[255,0,0]:[255,0,0]");

        // Assert
        Assert.That(table[100], Is.EqualTo(10));
        Assert.That(table[101], Is.EqualTo(10));
    }

    [Test]
    public void A_Color_Gradient_Takes_The_Nearest_Palette_Color_At_Each_Step()
    {
        // Act
        var table = Build("100:102=[255,0,0]:[0,0,255]", "200:201=#FF0000:#800000");

        // Assert: red, then halfway (nearest: a dark red or grey), then blue
        Assert.That(table[100], Is.EqualTo(10));
        Assert.That(table[102], Is.EqualTo(12));
        Assert.That(table[200], Is.EqualTo(10));
        Assert.That(table[201], Is.EqualTo(11));
    }

    [Test]
    public void A_Desaturated_Range_Keeps_Each_Colors_Lightness()
    {
        // Act: greys 0..255 onto black..red by their lightness
        var table = Build("0:255=%[0,0,0]:[2,0,0]");

        // Assert: white is full red (2 x its lightness, capped), black stays black, mid grey goes red too
        Assert.That(table[255], Is.EqualTo(10));
        Assert.That(table[0], Is.EqualTo(0));
        Assert.That(table[128], Is.EqualTo(10), "128 lightness x 2 = full red");
        Assert.That(table[64], Is.EqualTo(11), "64 lightness x 2 = dark red");
    }

    [Test]
    public void A_Range_Below_N_Only_Recolors_From_That_Many_Rows_Down()
    {
        // Arrange
        var warnings = new List<string>();

        // Act: 50 everywhere, 60 from row 10 down, 70 from row 20 down
        var translation = TranslationTable.BuildTranslation(["50=51", "60=61 below 10", "70=71 below 20"], Palette(), "test", warnings);

        // Assert
        Assert.That(warnings, Is.Empty);
        Assert.That(translation.IsBanded, Is.True);
        Assert.That(translation.Starts, Is.EqualTo(new[] { 0, 10, 20 }));
        Assert.That((translation.ForRow(0)[50], translation.ForRow(0)[60], translation.ForRow(0)[70]), Is.EqualTo(((byte)51, (byte)60, (byte)70)));
        Assert.That((translation.ForRow(9)[60], translation.ForRow(10)[60], translation.ForRow(10)[70]), Is.EqualTo(((byte)60, (byte)61, (byte)70)));
        Assert.That((translation.ForRow(25)[50], translation.ForRow(25)[60], translation.ForRow(25)[70]), Is.EqualTo(((byte)51, (byte)61, (byte)71)));
        Assert.That(translation.ForRow(-3)[50], Is.EqualTo(51), "above the top is the first band");
        Assert.That(TranslationTable.Build(["60=61 below 10"], Palette(), "test", warnings)[60], Is.EqualTo(61), "Build is the lowest rows'");
    }

    [Test]
    public void A_Translation_Without_Below_Is_One_Table()
    {
        // Act
        var translation = TranslationTable.BuildTranslation(["50=51"], Palette(), "test", []);

        // Assert
        Assert.That(translation.IsBanded, Is.False);
        Assert.That(translation.ForRow(40)[50], Is.EqualTo(51));
    }

    [Test]
    public void A_Sprites_Top_Is_Its_First_Drawn_Row()
    {
        // Arrange: 4 wide, drawn from row 2
        var mask = new byte[4 * 5];
        mask[2 * 4 + 1] = 1;
        mask[4 * 4 + 3] = 1;

        // Act / Assert
        Assert.That(new SpriteAsset { Width = 4, Height = 5, OpacityMask = mask }.TopRow, Is.EqualTo(2));
        Assert.That(new SpriteAsset { Width = 4, Height = 5, OpacityMask = new byte[20] }.TopRow, Is.Zero, "blank");
    }

    [TestCase("100:103")]
    [TestCase("1=2 below x")]
    [TestCase("1=2 below -3")]
    [TestCase("100:300=1:2")]
    [TestCase("a:b=1:2")]
    [TestCase("1:2=[1,2]:[3,4,5]")]
    [TestCase("1:2=[1,2,3]")]
    public void A_Range_That_Cannot_Be_Read_Is_Left_Out_With_A_Warning(string range)
    {
        // Arrange
        var warnings = new List<string>();

        // Act
        var table = TranslationTable.Build([range, "5=6"], Palette(), "test", warnings);

        // Assert
        Assert.That(warnings, Has.One.Contains(range));
        Assert.That(table[5], Is.EqualTo(6), "the other ranges still apply");
    }

    [Test]
    public void A_Factions_Class_Translations_Go_By_The_Nearest_Class_In_Its_Ancestry()
    {
        // Arrange
        var table = new FactionTable(YamlDataEntryLoader.Deserialize<FactionsAsset>("""
            factions:
              allies:
                translation: allies
                class-translations: { SS: allies-ss, Guard: allies-guard }
            """));

        // Assert
        Assert.That(table.Translation("allies", ["EliteSS", "SS", "Monster"]), Is.EqualTo("allies-ss"));
        Assert.That(table.Translation("allies", ["Dog", "Monster"]), Is.EqualTo("allies"));
        Assert.That(table.Translation("allies"), Is.EqualTo("allies"));
        Assert.That(table.Translation("axis", ["SS"]), Is.Null);
    }

    [Test]
    public void A_Packs_Translations_Replace_Its_Base_Packs_By_Name_And_A_Mods_Too()
    {
        // Arrange
        var pk3 = new MemoryAssetSource("pfwolf.pk3", new()
        {
            ["gamepacks/gamepack-info.yaml"] = "alpha:\n  title: Alpha\n  game-palette: pal\nbeta:\n  title: Beta\n  game-palette: pal\n  base-pack: alpha\n",
            ["gamepacks/alpha/translations.yaml"] = "red: [\"1:2=3:4\"]\nblue: [\"5:6=7:8\"]\n",
            ["gamepacks/beta/translations.yaml"] = "red: [\"9:10=11:12\"]\n",
        });
        var mod = new MemoryAssetSource("mod.pk3", new()
        {
            ["modinfo.yaml"] = "name: Mod\nversion: \"1\"\n",
            ["translations.yaml"] = "blue: !remove\ngreen: [\"1=2\"]\n",
        });

        // Act
        var plain = new PfWolfPk3Loader([pk3], "beta", "beta").Load<TranslationsAsset>("beta/translations");
        var modded = new PfWolfPk3Loader([pk3], "beta", "beta", [mod]).Load<TranslationsAsset>("beta/translations");

        // Assert
        Assert.That(plain.Translations["red"], Is.EqualTo(new[] { "9:10=11:12" }));
        Assert.That(plain.Translations["BLUE"], Is.EqualTo(new[] { "5:6=7:8" }), "names ignore case");
        Assert.That(modded.Translations.Keys, Is.EquivalentTo(new[] { "red", "green" }));
    }
}
