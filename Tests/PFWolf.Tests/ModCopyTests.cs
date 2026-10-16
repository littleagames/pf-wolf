using PFWolf.Editor.Definitions;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>The YAML tab's Copy to mod: where a mod keeps its own of pfwolf.pk3's files, and what it starts as</summary>
public class ModCopyTests
{
    [TestCase("gamepacks/wolf3d/colors.yaml", "colors.yaml")]
    [TestCase("gamepacks/spear/game-info.yaml", "game-info.yaml")]
    [TestCase("gamepacks/wolf3d/hud-messages.yaml", "hud-messages.yaml")]
    [TestCase("gamepacks/wolf3d/language/en-us.yaml", "language/en-us.yaml")]
    [TestCase("language/en-us.yaml", "language/en-us.yaml")]
    [TestCase("actordefs/wolf3d/guards.yaml", "actordefs/guards.yaml")]
    [TestCase("actordefs/native.yaml", "actordefs/native.yaml")]
    [TestCase("mapdefs/wolf3d/walls.yaml", "mapdefs/walls.yaml")]
    [TestCase("sounds/sound-seq.yaml", "sounds/sound-seq.yaml")]
    [TestCase("menudefs/main-menu.yaml", "menudefs/main-menu.yaml")]       // merged by id (MENU HOOKS)
    [TestCase("menudefs/blake/options.yaml", "menudefs/options.yaml")]
    public void A_Mod_Keeps_Its_Own_Where_The_Game_Lays_It_Over_Pfwolfs(string basePath, string modPath)
    {
        // Act
        var path = YamlFileTypes.ModPathFor(basePath, out var why);

        // Assert
        Assert.That(path, Is.EqualTo(modPath), why);
        Assert.That(YamlFileTypes.Describe(modPath, isMod: true), Is.EqualTo(YamlFileTypes.Describe(basePath, isMod: false)), "the game reads it as the same");
    }

    [TestCase("gamepacks/gamepack-info.yaml")]
    [TestCase("gamepacks/blake/presenter.yaml")]
    [TestCase("gamepacks/wolf3d-shareware/sound-seq.yaml")]
    public void Files_A_Mod_Cannot_Change_Say_Why(string basePath)
    {
        // Act
        var path = YamlFileTypes.ModPathFor(basePath, out var why);

        // Assert
        Assert.That(path, Is.Null);
        Assert.That(why, Is.Not.Empty);
    }

    [Test]
    public void An_Empty_Copy_Is_A_Note_That_Reads_As_Nothing()
    {
        // Act
        var text = YamlFileTypes.ModCopyText("gamepacks/wolf3d/colors.yaml", "BKGDCOLOR: \"#590000\"\n", whole: false);

        // Assert
        Assert.That(text, Does.StartWith("# Laid over pfwolf.pk3's gamepacks/wolf3d/colors.yaml"));
        Assert.That(YamlTree.IsEmptyDocument(text), Is.True);
        Assert.That(YamlFileTypes.Check("colors.yaml", isMod: true, text), Is.Null);
    }

    [Test]
    public void A_Whole_Copy_Is_Pfwolfs_File_After_The_Note()
    {
        // Arrange
        const string colors = "# Menu frame\r\nBORDCOLOR: \"#890000\"\r\n";

        // Act
        var text = YamlFileTypes.ModCopyText("gamepacks/wolf3d/colors.yaml", colors, whole: true);

        // Assert
        Assert.That(text, Does.EndWith("\r\n\r\n" + colors));
        Assert.That(text.Replace("\r\n", ""), Does.Not.Contain("\n"), "keeps the file's line endings");
        Assert.That(YamlFileTypes.Check("colors.yaml", isMod: true, text), Is.Null);
    }
}
