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

    [TestCase("blake", "FloorLamp", false, 6)]      // partial-bright
    [TestCase("wolf3d", "FloorLamp", true, 0)]      // bright
    [TestCase("wolf3d", "Guard", false, 0)]
    public void The_3D_Views_Things_Are_Lit_As_The_Game_Lights_Their_Spawn_Frame(string game, string thing, bool bright, int lighten)
    {
        Assert.That(Load(game).ThingShading(thing), Is.EqualTo((bright, lighten)));
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
    public void A_Thing_Is_Lit_By_Its_Own_Light_And_Its_Pulse_Plays()
    {
        // Arrange: a guard glowing green and pulsing (a mod's), on moody-lights' dim default map
        // (light 64). The content's lit decorations are all drawn bright, so unshaded anyway.
        var root = TestPaths.RepoRoot();
        var mod = Path.Combine(Path.GetTempPath(), "pfwolf-glow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(mod, "actordefs"));
        File.WriteAllText(Path.Combine(mod, "modinfo.yaml"), "name: Glow test\ngame-packs: [wolf3d]\n");
        File.WriteAllText(Path.Combine(mod, "actordefs", "glow.yaml"),
            "GlowGuard:\n  parent: Guard\n  properties:\n    \"light.intensity\": 180\n    \"light.radius\": 3\n"
            + "    \"light.color\": \"#40FF40\"\n    \"light.effect\": pulse\n    \"light.low\": 40\n    \"light.tics\": 70\n");
        try
        {
            var actors = new ActorBrowserViewModel();
            actors.Load(Load("wolf3d", Path.Combine(root, "examples", "mods", "moody-lights"), mod));
            actors.Show("GlowGuard");
            actors.SeenFromPlayer = true;
            actors.Distance = 4;
            var first = actors.ShadeText;

            // Act: half a second of play, as the window's timer gives it
            var seen = new HashSet<string> { first };
            for (int i = 0; i < 18; i++)
            {
                actors.Tick(2);
                seen.Add(actors.ShadeText);
            }

            // Assert: its light adds to the level's 8 and tints it green; the pulse moves it
            Assert.That(first, Does.Contain("its light +").And.Contain("tinted #"), first);
            Assert.That(seen, Has.Count.GreaterThan(1), "the pulse changes how much it adds");
        }
        finally
        {
            Directory.Delete(mod, recursive: true);
        }
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
