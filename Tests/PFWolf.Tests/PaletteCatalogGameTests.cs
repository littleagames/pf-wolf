using PFWolf.Assets;
using PFWolf.Editor.Data;

namespace PFWolf.Tests;

/// <summary>The palette browser's catalog over the real games. Needs a built game folder (pfwolf.pk3 and the data files).</summary>
[Category("GameData")]
public class PaletteCatalogGameTests
{
    private static string? GameFolder()
    {
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        return new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
    }

    private static GameContent Load(string game, params string[] mods)
    {
        var folder = GameFolder();
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

    [TestCase("wolf3d", "WOLFPAL")]
    [TestCase("spear", "SPEARPAL")]
    public void The_Game_Palette_Comes_First_And_Matches_What_The_Editor_Draws_With(string game, string gamePalette)
    {
        // Arrange
        var content = Load(game);

        // Act
        var entries = PaletteCatalog.Build(content);
        TestContext.Out.WriteLine(string.Join(", ", entries.Select(entry => $"{entry.Name} ({entry.Source}, {entry.Uses.Count} uses)")));

        // Assert
        Assert.That(entries[0].Name, Is.EqualTo(gamePalette));
        Assert.That(entries[0].IsGamePalette, Is.True);
        Assert.That(entries[0].Uses, Is.Not.Empty);
        Assert.That(entries[0].Colors.Select(PaletteCatalog.Hex), Is.EqualTo(content.Palette.Select(PaletteCatalog.Hex)));
        Assert.That(entries.Count(entry => entry.IsGamePalette), Is.EqualTo(1));
    }

    [Test]
    public void Spears_Title_And_End_Palettes_Are_Used_By_Their_Screens()
    {
        // Arrange
        var content = Load("spear");

        // Act
        var entries = PaletteCatalog.Build(content).ToDictionary(entry => entry.Name);

        // Assert: the data files' palettes are listed, each with the picture drawn in it
        Assert.That(entries, Does.ContainKey("TITLEPAL"));
        Assert.That(entries["TITLEPAL"].Uses.Select(use => use.Label), Has.Some.EqualTo("The title screen"));
        Assert.That(entries["TITLEPAL"].Uses.Select(use => use.Picture), Has.Some.Not.Null);
        Assert.That(entries.Values.Where(entry => entry.Name.StartsWith("END")).SelectMany(entry => entry.Uses), Has.Some.Matches<PaletteUse>(use => use.Picture != null));
    }

    [Test]
    public void A_Saved_Palette_Replaces_The_Games_Own_In_A_Mod()
    {
        // Arrange: the game palette with its first color changed, saved into a new mod
        var content = Load("wolf3d");
        var colors = (PaletteColor[])PaletteCatalog.Build(content)[0].Colors.Clone();
        colors[0] = new PaletteColor(12, 34, 56);
        var mod = Path.Combine(Path.GetTempPath(), "pfwolf-palette-mod-" + Guid.NewGuid().ToString("N"));

        try
        {
            var document = new PFWolf.Editor.Editing.PaletteDocument("WOLFPAL", colors);
            document.Save(mod);

            // Act
            var modded = Load("wolf3d", mod);

            // Assert: it's what the editor (and the game) draws with now, and it says where it came from
            Assert.That(PaletteCatalog.Hex(modded.Palette[0]), Is.EqualTo("#0C2238"));
            var entry = PaletteCatalog.Build(modded)[0];
            Assert.That(entry.Origins.Count(origin => origin.Action != PFWolf.Loaders.AssetOrigin.LeftOut), Is.GreaterThan(1));
            Assert.That(modded.ModFolderOf("wolfpal", nameof(Palette)), Is.EqualTo(Path.GetFullPath(mod)).IgnoreCase.Or.EqualTo(mod).IgnoreCase);
        }
        finally
        {
            if (Directory.Exists(mod))
                Directory.Delete(mod, recursive: true);
        }
    }
}
