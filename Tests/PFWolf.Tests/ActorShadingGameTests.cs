using PFWolf.Editor.Data;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Tests;

/// <summary>
/// The actor window's "seen from the player" preview on real games: Blake Stone's default-map
/// fade with a lamp's partial-bright lighten, a mod level's light zone, and shading set by hand.
/// Needs a built game folder with the data files.
/// </summary>
[Category("GameData")]
public class ActorShadingGameTests
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

    [Test]
    public void A_Blake_Lamp_Fades_Less_By_Its_Partial_Bright_Lighten()
    {
        // Arrange
        var actors = new ActorBrowserViewModel();
        actors.Load(Load("blake"));
        actors.Show("FloorLamp");

        // Act
        actors.SeenFromPlayer = true;
        actors.Distance = 24;

        // Assert: Blake's inverse fade (80%: 25 steps), 6 steps less for partial-bright
        Assert.That(actors.ShadeText, Does.StartWith("24 tiles: fade step 15 of 25 (lighten +6 from step 21)"));
    }

    [Test]
    public void A_Mod_Levels_Zone_Lights_The_Frame()
    {
        // Arrange: the switch demo's MAP01 has zones and no shading of its own
        var mod = Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "switch-demo");
        var actors = new ActorBrowserViewModel();
        actors.Load(Load("wolf3d", mod));
        actors.Show("Guard");
        actors.SeenFromPlayer = true;

        // Act
        actors.SelectedShadingSource = actors.ShadingSources.Single(source => source.MapName == "MAP01");
        var zones = actors.PreviewZones.Select(zone => zone.Id).ToList();
        actors.SelectedPreviewZone = actors.PreviewZones.Single(zone => zone.Id == 1);

        // Assert: the light room's 24, a level of 3, and no fade (the level has none)
        Assert.That(zones, Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(actors.ShadeText, Does.Contain("no fade").And.Contain("light level 3 of 31 (zone 1)"));
    }

    [Test]
    public void Shading_Set_By_Hand_Resolves_As_A_Levels_Own_Would()
    {
        // Arrange: Wolf3D has no shading anywhere
        var actors = new ActorBrowserViewModel();
        actors.Load(Load("wolf3d"));
        actors.Show("Guard");
        actors.SeenFromPlayer = true;
        Assert.That(actors.ShadeText, Does.Contain("no shading here").And.Contain("pick a level"));

        // Act: a light alone takes the game's other defaults (fade to black, 0 to 16 tiles, 100%)
        actors.HandLight = "128";
        actors.Distance = 8.25;

        // Assert
        Assert.That(actors.ShadeText, Does.Contain("fade step 15 of 31").And.Contain("light level 16 of 31").And.Contain("set by hand"));

        // A bad value is left out, and said
        actors.HandMaxFade = "lots";
        Assert.That(actors.ShadeText, Does.Contain("max fade 'lots' isn't 0-100, left out"));
    }
}
