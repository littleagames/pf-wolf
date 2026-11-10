using PFWolf.Editor.Data;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Tests;

/// <summary>
/// The font window's style editing: a font's fields changed, drawn, undone, saved into a mod as
/// what changed, and new fonts. Needs a built game folder with the data files.
/// </summary>
[Category("GameData")]
public class FontStyleEditingTests
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
        _mod = Path.Combine(Path.GetTempPath(), "pfwolf-fonts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_mod);
    }

    [TearDown]
    public void RemoveMod() => Directory.Delete(_mod, recursive: true);

    private static FieldNode Row(FontBrowserViewModel fonts, params string[] path)
    {
        var row = fonts.StyleRows.Single(row => row.Key == path[0]);
        foreach (var key in path.Skip(1))
        {
            row.EnsureChildren();
            row = row.Children.Single(child => child.Key == key);
        }
        return row;
    }

    [Test]
    public void A_Change_Is_Drawn_Undone_And_Saved_As_Just_What_Changed()
    {
        // Arrange: HudMessage, the small font with shadow: {}
        var fonts = new FontBrowserViewModel { PickMod = _ => Task.FromResult<string?>(_mod) };
        fonts.Load(Load("wolf3d"));
        fonts.Show("HudMessage");
        var before = fonts.PreviewPicture!.Indices.ToArray();

        // Act: the shadow further down
        Row(fonts, "shadow", "y").Text = "3";

        // Assert: drawn so, and a change to save
        Assert.That(fonts.IsStyleChanged, Is.True);
        Assert.That(fonts.UnsavedNames, Is.EqualTo(new[] { "font HudMessage" }));
        Assert.That(fonts.PreviewPicture!.Indices, Is.Not.EqualTo(before));
        Assert.That(fonts.SelectedItem!.HasEdits, Is.True);

        fonts.UndoStyleCommand.Execute(null);
        Assert.That(fonts.IsStyleChanged, Is.False);
        Assert.That(fonts.PreviewPicture!.Indices, Is.EqualTo(before));
        fonts.RedoStyleCommand.Execute(null);
        Assert.That(fonts.IsStyleChanged, Is.True);

        fonts.SaveStyleCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        var text = File.ReadAllText(Path.Combine(_mod, "fonts.yaml"));
        Assert.That(text, Does.Contain("HudMessage").And.Contain("3").And.Not.Contain("source"));
        Assert.That(fonts.UnsavedNames, Is.Empty);

        // The game with the mod has the change, the rest of the font as it was
        var reloaded = Load("wolf3d", _mod);
        var hud = reloaded.Assets.FindInGamePack<PFWolf.Assets.FontDefinitionsAsset>("fonts")!.Fonts["HudMessage"];
        Assert.That((hud.Source, hud.Shadow?.X, hud.Shadow?.Y), Is.EqualTo(("SmallFont", 1, 3)));
    }

    [Test]
    public void A_New_Font_Is_Listed_Until_It_Is_Saved_Whole()
    {
        // Arrange
        var fonts = new FontBrowserViewModel
        {
            PickMod = _ => Task.FromResult<string?>(_mod),
            AskText = (_, _, _, check) => Task.FromResult<string?>(check("Title Font") == null ? "Title Font" : "TitleFont"),
        };
        fonts.Load(Load("wolf3d"));
        fonts.Show("LargeFont");

        // Act: a copy of LargeFont, with an outline
        fonts.NewFontCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.That((fonts.SelectedItem?.Name, fonts.SelectedItem?.IsDraft, fonts.IsStyleChanged), Is.EqualTo(("TitleFont", true, true)),
            "a name with a space isn't taken; the new one is picked, and unsaved");
        Row(fonts, "outline", "color").Text = "Red";
        Assert.That(fonts.PreviewPicture, Is.Not.Null);
        fonts.SaveStyleCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        // Assert: written whole, as LargeFont is plus the outline
        var reloaded = Load("wolf3d", _mod);
        var title = reloaded.Assets.FindInGamePack<PFWolf.Assets.FontDefinitionsAsset>("fonts")!.Fonts["TitleFont"];
        Assert.That((title.Type, title.Outline?.Color), Is.EqualTo(("vga", "Red")));
        Assert.That(FontCatalog.Build(reloaded).Any(entry => entry.Name == "TitleFont"), Is.True);
    }

    [Test]
    public void A_Type_Shows_Only_The_Fields_It_Reads()
    {
        var fonts = new FontBrowserViewModel();
        fonts.Load(Load("wolf3d"));
        fonts.Show("SmallFont");
        Assert.That(fonts.StyleRows.Select(row => row.Key), Does.Contain("source").And.Not.Contain("image").And.Not.Contain("pattern"));

        fonts.Show("IntermissionFont");
        Assert.That(fonts.StyleRows.Select(row => row.Key), Does.Contain("pattern").And.Contain("glyphs").And.Not.Contain("source").And.Not.Contain("image"));

        // Made a sheet: the sheet's fields come, the graphic font's that are set stay, and it's one step back
        Row(fonts, "type").Text = "sheet";
        Assert.That(fonts.StyleRows.Select(row => row.Key), Does.Contain("image").And.Contain("pattern").And.Not.Contain("glyphs"));
        fonts.UndoStyleCommand.Execute(null);
        Assert.That(fonts.StyleRows.Select(row => row.Key), Does.Contain("glyphs").And.Not.Contain("image"));
        Assert.That(fonts.IsStyleChanged, Is.False);
    }
}
