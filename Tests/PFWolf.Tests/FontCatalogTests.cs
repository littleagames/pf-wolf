using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Definitions;
using PFWolf.Fonts;

namespace PFWolf.Tests;

/// <summary>The font window's list: what names a font, found by walking the definitions</summary>
public class FontCatalogTests
{
    [Test]
    public void Font_References_Are_Found_With_Their_Yaml_Paths()
    {
        var menu = new MenuAsset
        {
            Font = "MenuItems",
            StatsPanel = new MenuStatsPanel { Font = "Stats" },
            Components = [new ComponentEntry { Params = [new() { ["text"] = "Hello", ["font"] = "LabelFont" }] }],
        };
        var found = FontCatalog.FontReferences(menu).Select(found => (string.Join("/", found.Path), found.Font)).ToList();
        Assert.That(found, Does.Contain(("font", "MenuItems")));
        Assert.That(found, Does.Contain(("stats-panel/font", "Stats")));
        Assert.That(found, Does.Contain(("components/0/params/0/font", "LabelFont")));

        var presenter = new PresenterAsset { Fonts = ["Presenter1", "Presenter2"] };
        Assert.That(FontCatalog.FontReferences(presenter).Select(found => (string.Join("/", found.Path), found.Font)),
            Is.SupersetOf(new[] { ("fonts/0", "Presenter1"), ("fonts/1", "Presenter2") }));
    }

    [Test]
    public void Property_Names_Become_Yaml_Keys() => Assert.That(FontCatalog.Kebab("TitleFont"), Is.EqualTo("title-font"));
}

/// <summary>The font window's list for each game. Needs a built game folder with the data files.</summary>
[Category("GameData")]
public class FontCatalogGameTests
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

    private static FontEntry Entry(List<FontEntry> entries, string name)
        => entries.Single(entry => entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    [Test]
    public void Wolf3d_Fonts_Their_Definitions_And_Uses()
    {
        var content = Load("wolf3d");
        var entries = FontCatalog.Build(content);
        Assert.That(entries.Select(entry => entry.Name), Is.SupersetOf(new[] { "SmallFont", "LargeFont", "HudMessage", "StatusNumbers", "IntermissionFont" }));

        var small = Entry(entries, "SmallFont");
        Assert.That(small.DefinedIn.Select(site => site.File), Is.EqualTo(new[] { "pfwolf.pk3: gamepacks/wolf3d/fonts.yaml" }));
        Assert.That(small.DefinedIn[0].Yaml, Is.EqualTo("SmallFont:\n  type: vga"));
        Assert.That(small.SourceName, Is.EqualTo("SmallFont"));
        Assert.That(small.SourceOrigins, Is.Not.Empty);
        Assert.That(small.Uses.Select(use => use.Label), Has.Some.StartsWith("The engine:"));
        Assert.That(small.Uses.Select(use => use.Label), Has.Some.EqualTo("fonts.yaml: HudMessage is made from it"));
        Assert.That(small.Uses.Where(use => use.Target?.Kind == DefinitionKind.GameInfo), Is.Not.Empty, "game-info prints in it");

        Assert.That(Entry(entries, "StatusNumbers").Uses.Select(use => use.Label), Has.Some.StartsWith("statusbar"));
        Assert.That(Entry(entries, "HudMessage").Uses.Select(use => use.Label), Has.Some.StartsWith("hud-messages"));
        // Wolf3D's menus name no font: they're in LargeFont, the engine's menu font
        Assert.That(Entry(entries, "LargeFont").Uses.Select(use => use.Label), Has.Some.EqualTo(FontNames.EngineUses[FontNames.Large]));

        // Everything in Wolf3D builds and nothing's wrong with it
        var canvas = new IndexedTextCanvas(1, 1, content.Palette, content.Assets.FindInGamePack<ColorThemeAsset>("colors"));
        foreach (var entry in entries)
        {
            var problems = new List<string>();
            Assert.That(FontCatalog.BuildFont(content, entry, canvas, problems), Is.Not.Null, entry.Name);
            Assert.That(problems.Concat(FontCatalog.Notes(entry)), Is.Empty, entry.Name);
        }
    }

    [Test]
    public void A_Mods_Fonts_Lay_Over_The_Games()
    {
        var content = Load("wolf3d", Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "fonts-demo"));
        var entries = FontCatalog.Build(content);

        // HudMessage is the game's, then the mod's: the mod's lines are the ones shown
        var hud = Entry(entries, "HudMessage");
        Assert.That(hud.DefinedIn, Has.Count.EqualTo(2));
        Assert.That(hud.DefinedIn[0].File, Does.StartWith("pfwolf.pk3"));
        Assert.That(hud.DefinedIn[1].File, Does.StartWith("fonts-demo"));

        // Its fonts of every type build with nothing wrong, the sheets and the .fnt file among them
        var canvas = new IndexedTextCanvas(1, 1, content.Palette, content.Assets.FindInGamePack<ColorThemeAsset>("colors"));
        var demo = entries.Where(entry => entry.Name.StartsWith("Fd", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.That(demo.Select(entry => entry.Type), Is.SupersetOf(new[] { "vga", "graphic", "sheet" }));
        foreach (var entry in demo)
        {
            var problems = new List<string>();
            Assert.That(FontCatalog.BuildFont(content, entry, canvas, problems), Is.Not.Null, entry.Name);
            Assert.That(problems, Is.Empty, entry.Name);
        }
        Assert.That(canvas.UnknownColors, Is.Empty);
    }

    [Test]
    public void Spear_Fonts_Come_From_Wolf3ds_File()
    {
        var entries = FontCatalog.Build(Load("spear"));
        Assert.That(Entry(entries, "LargeFont").DefinedIn.Select(site => site.File), Is.EqualTo(new[] { "pfwolf.pk3: gamepacks/wolf3d/fonts.yaml" }));
    }

    [Test]
    public void Blake_Font_Chunks_Are_Listed_With_What_Is_Made_From_Them()
    {
        var content = Load("blake");
        var entries = FontCatalog.Build(content);
        Assert.That(entries.Where(entry => !entry.IsDefined).Select(entry => entry.Name), Is.SupersetOf(new[] { "Font1", "Font2", "Font3", "Font4", "Font5" }));

        Assert.That(FontCatalog.Notes(Entry(entries, "Font1")), Has.One.Contains("31 codes"));
        Assert.That(Entry(entries, "Font3").Uses.Select(use => use.Label), Has.Some.EqualTo("fonts.yaml: BlakeHud is made from it"));

        // Blake's presenter lists Presenter1 as fonts 1 and 2
        var presenter = Entry(entries, "Presenter1").Uses.First(use => use.Label.StartsWith("presenter"));
        Assert.That((presenter.Target?.Kind, presenter.Target?.Path), Is.EqualTo(((DefinitionKind?)DefinitionKind.Presenter, (IReadOnlyList<string>)["fonts", "0"])));
        Assert.That(presenter.Label, Is.EqualTo("presenter: fonts › #1"));
    }
}
