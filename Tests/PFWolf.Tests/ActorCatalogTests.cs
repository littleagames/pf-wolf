using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Definitions;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

/// <summary>The actor window's catalog over classes written here (no game data)</summary>
public class ActorCatalogTests
{
    [Test]
    public void Call_Arguments_Are_Read_With_Quotes_Taken_Off()
    {
        Assert.That(ActorCatalog.CallName(" A_GiveInventory(\"Clip\", 25) "), Is.EqualTo("A_GiveInventory"));
        Assert.That(ActorCatalog.CallArguments("A_GiveInventory(\"Clip\", 25)"), Is.EqualTo(new[] { "Clip", "25" }));
        Assert.That(ActorCatalog.CallArguments("A_PlaySound(\"a, b\")"), Is.EqualTo(new[] { "a, b" }));
        Assert.That(ActorCatalog.CallArguments("T_Chase"), Is.Empty);
    }

    [Test]
    public void Actions_That_Ran_Are_Written_As_Runs()
    {
        Assert.That(PFWolf.Editor.ViewModels.ActorBrowserViewModel.RunsOf(["A", "A", "B", "A"]), Is.EqualTo("A ×2, B, A"));
        Assert.That(PFWolf.Editor.ViewModels.ActorBrowserViewModel.RunsOf(["1", "2", "3", "4", "5", "6", "7"]), Is.EqualTo("…, 2, 3, 4, 5, 6, 7"));
    }

    [Test]
    public void Numbers_Are_Written_As_Runs()
    {
        Assert.That(ActorCatalog.Ranges([108, 109, 110, 144, 146, 147]), Is.EqualTo("108-110, 144, 146-147"));
        Assert.That(ActorCatalog.Ranges([]), Is.EqualTo(""));
    }

    [Test]
    public void A_States_Frames_Stop_Where_It_Goes_On_To_Another()
    {
        // Arrange
        var states = ActorStateResolver.Resolve(ActorDefs.Parse("""
            Thing:
              states:
                Attack:
                  - { sprite: GARD, frames: [F, G], tics-per-frame: 5, next-state: Chase }
                Chase:
                  - { sprite: GARD, frames: [B], tics-per-frame: 10 }
            """)["Thing"].States);

        // Act
        var frames = ActorCatalog.FramesOf(states["Attack"]);

        // Assert
        Assert.That(frames.Select(frame => frame.FrameLetter), Is.EqualTo(new[] { "F", "G" }));
    }

    [Test]
    public void Engine_Classes_Know_What_Each_Is_Built_On()
    {
        Assert.That(ActorClasses.EngineClass("Guard", ["Monster", "Actor"]), Is.EqualTo("Monster"));
        Assert.That(ActorClasses.EngineClass("Thing", ["Inventory"]), Is.EqualTo("CustomInventory"));
        Assert.That(ActorClasses.EngineClass("Lamp", []), Is.EqualTo("Actor"));
        Assert.That(ActorClasses.IsA("Informant", "Monster"), Is.True);
        Assert.That(ActorClasses.IsA("Weapon", "Inventory"), Is.True);
        Assert.That(ActorClasses.IsA("Monster", "BlakeMonster"), Is.False);
    }

    [Test]
    public void Engine_Classes_Are_The_Engines_Own()
    {
        // Every actor type the engine has (PFWolf.Entities.Actors), with what it's built on
        var types = typeof(Entities.Actors.Actor).Assembly.GetTypes()
            .Where(type => type.Namespace == "PFWolf.Entities.Actors" && typeof(Entities.Actors.Actor).IsAssignableFrom(type) && !type.IsNested)
            .ToDictionary(type => type.Name, type => type == typeof(Entities.Actors.Actor) ? null : type.BaseType?.Name);

        Assert.That(ActorClasses.Engine, Is.EquivalentTo(types));
    }
}

/// <summary>The actor window's catalog over the games' own classes</summary>
[Category("GameData")]
public class ActorCatalogGameTests
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

    private static ActorEntry Entry(List<ActorEntry> entries, string name)
        => entries.Single(entry => entry.Name == name);

    [TestCase("wolf3d")]
    [TestCase("spear")]
    [TestCase("blake")]
    [TestCase("planetstrike")]
    public void Every_Class_Is_Built_With_Nothing_Wrong(string game)
    {
        // Act
        var entries = ActorCatalog.Build(Load(game));

        // Assert: the games' own classes all hang together (Pk3ContentTests checks the same files)
        var problems = entries.SelectMany(entry => entry.Problems.Select(problem => $"{entry.Name}: {problem}")).ToList();
        Assert.That(entries, Has.Count.GreaterThan(100));
        Assert.That(problems, Is.Empty);
    }

    [Test]
    public void A_Guard_Is_An_Enemy_With_Its_States_File_And_Uses()
    {
        // Act
        var entries = ActorCatalog.Build(Load("wolf3d"));

        // Assert
        var guard = Entry(entries, "Guard");
        Assert.That(guard.Kind, Is.EqualTo(ActorKind.Enemy));
        Assert.That(guard.EngineClass, Is.EqualTo("Monster"));
        Assert.That(guard.Ancestors, Is.EqualTo(new[] { "Monster" }));
        Assert.That(guard.States.Keys, Is.SupersetOf(new[] { "Spawn", "Path", "Chase", "Attack", "Death", ActorStateResolver.RaiseState }));
        Assert.That(ActorCatalog.FramesOf(guard.States["Chase"]), Has.Count.EqualTo(6));
        Assert.That(guard.DefinedIn.Select(site => site.File), Is.EqualTo(new[] { "pfwolf.pk3: actordefs/wolf3d/guards.yaml" }));
        Assert.That(guard.DefinedIn[0].Yaml, Does.StartWith("Guard:\n  parent: Monster"));

        var uses = guard.Uses.Select(use => use.Label).ToList();
        Assert.That(uses, Has.Some.StartsWith("mapdefs: things 108-115, "));
        Assert.That(guard.Uses.First(use => use.Label.StartsWith("mapdefs: things")).Target,
            Is.EqualTo(new ActorUseTarget(Kind: DefinitionKind.MapDefs, EntryPath: ["things", "108"], FieldPath: ["class"])).Using<ActorUseTarget>(SameTarget));
        Assert.That(uses, Has.Some.Matches("^level MAP01.*placed$"), "E1M1 has guards");
        Assert.That(uses, Has.None.Contains("property tag"), "a tag that's the class's name doesn't name it");

        var clip = Entry(entries, "DroppedClip");
        Assert.That(clip.Kind, Is.EqualTo(ActorKind.Item));
        Assert.That(clip.Uses.Select(use => use.Label), Has.Some.EqualTo("Guard: property dropitem"));

        Assert.That(Entry(entries, "Monster").Uses.Select(use => use.Label), Has.Some.EqualTo("Guard is built on it (parent)"));
        Assert.That(Entry(entries, "Rocket").Kind, Is.EqualTo(ActorKind.Projectile));
        Assert.That(Entry(entries, "Pistol").Kind, Is.EqualTo(ActorKind.Weapon));
        Assert.That(Entry(entries, "DeathCam").Uses.Select(use => use.Label), Has.Some.StartsWith("the engine:"));
    }

    // Records with lists compare the lists by reference; this compares them by what's in them
    private static bool SameTarget(ActorUseTarget a, ActorUseTarget b)
        => a.Kind == b.Kind && a.Actor == b.Actor && a.Map == b.Map
           && (a.EntryPath ?? []).SequenceEqual(b.EntryPath ?? []) && (a.FieldPath ?? []).SequenceEqual(b.FieldPath ?? []);

    [Test]
    public void The_Window_Plays_A_Guard_From_Chase_And_Follows_An_Attack_Into_Chase()
    {
        // Arrange
        var browser = new PFWolf.Editor.ViewModels.ActorBrowserViewModel();
        browser.Load(Load("wolf3d"));

        // Act: a guard stands still for good, so the preview starts it chasing
        browser.Show("Guard");

        // Assert
        Assert.That(browser.SelectedState, Is.EqualTo("Chase"));
        Assert.That(browser.Timeline.Select(frame => frame.Letter), Is.EqualTo(new[] { "B", "B", "C", "D", "D", "E" }));
        Assert.That(browser.Timeline[0].IsCurrent, Is.True);
        Assert.That(browser.PictureText, Is.EqualTo("GARDB1"));
        Assert.That(browser.TimelineEnd, Is.EqualTo("↺ loops"));
        Assert.That(browser.KeyNumbers, Does.StartWith("health 25"));
        Assert.That(browser.Properties, Is.Empty, "the guard's own properties are its fields; Monster gives it none");

        // Its attack: T_Shoot told as the second frame ends, then on into Chase
        browser.SelectedState = "Attack";
        browser.Tick(20);
        browser.Tick(20);
        Assert.That(browser.EventText, Is.EqualTo("Ran: T_Shoot"));
        browser.Tick(20);
        Assert.That(browser.SelectedState, Is.EqualTo("Chase"), "it follows next-state");
        Assert.That(browser.CurrentFrame!.StateName, Is.EqualTo("Chase"));

        // A view of its side, and a frame picked on the timeline
        browser.SelectedView = 3;
        browser.PickFrame(browser.Timeline[2]);
        Assert.That(browser.Playing, Is.False);
        Assert.That(browser.PictureText, Is.EqualTo("GARDC3"));

        // A weapon starts on Ready; its 1-tic frame loops on itself, and Next frame goes round it once
        browser.Show("Pistol");
        Assert.That(browser.SelectedState, Is.EqualTo("Ready"));
        browser.StepFrameCommand.Execute(null);
        Assert.That(browser.EventText, Is.EqualTo("Ran: A_WeaponReady"));

        // A use that's another class shows it here
        browser.Show("DroppedClip");
        browser.SelectedUse = browser.Uses.First(use => use.Label == "Guard: property dropitem");
        browser.GoToUseCommand.Execute(null);
        Assert.That(browser.SelectedItem!.Name, Is.EqualTo("Guard"));
    }

    [Test]
    public void A_Sprites_Uses_Name_The_Classes_For_Show_In_Actors()
    {
        var uses = ArtCatalog.SpriteUses(Load("wolf3d"));

        Assert.That(uses["GARDA"].Select(use => use.ActorClass), Does.Contain("Guard"));
        Assert.That(uses["GARDA"].Single(use => use.ActorClass == "Guard").Target, Is.Not.Null, "and it's still put in the palette");
    }

    [Test]
    public void A_Frame_Shows_Its_Sprite_From_Each_View()
    {
        // Arrange
        var content = Load("wolf3d");
        var guard = Entry(ActorCatalog.Build(content, levels: false), "Guard");
        var standing = guard.States["Spawn"];
        var dying = ActorCatalog.FramesOf(guard.States["Death"])[^1];

        // Assert: a guard turns; its corpse looks the same from every side
        Assert.That(ActorCatalog.Turns(content, standing), Is.True);
        Assert.That(ActorCatalog.PictureName(content, standing, 3, out var found), Is.EqualTo("GARDA3"));
        Assert.That(found, Is.True);
        Assert.That(ActorCatalog.PictureName(content, dying, 3, out found), Is.EqualTo("GARDN0"));
        Assert.That(found, Is.True);
    }
}
