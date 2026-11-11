using PFWolf.Editor.Data;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Tests;

/// <summary>
/// The actor window's field editing: a class's fields changed, played, undone, saved into a mod as
/// what changed, and new classes built on or copied from one. Needs a built game folder with the
/// data files.
/// </summary>
[Category("GameData")]
public class ActorEditingTests
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
        _mod = Path.Combine(Path.GetTempPath(), "pfwolf-actors-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_mod);
    }

    [TearDown]
    public void RemoveMod() => Directory.Delete(_mod, recursive: true);

    private static FieldNode Row(ActorBrowserViewModel actors, params string[] path)
    {
        var row = actors.FieldRows.Single(row => row.Key == path[0]);
        foreach (var key in path.Skip(1))
        {
            row.EnsureChildren();
            row = row.Children.Single(child => child.Key == key);
        }
        return row;
    }

    [Test]
    public void A_Change_Is_Played_Undone_And_Saved_As_Just_What_Changed()
    {
        // Arrange
        var actors = new ActorBrowserViewModel { PickMod = _ => Task.FromResult<string?>(_mod) };
        actors.Load(Load("wolf3d"));
        actors.Show("Guard");

        // Its own properties are fields; states aren't (the timeline's)
        Assert.That(actors.FieldRows.Select(row => row.Key), Is.SupersetOf(new[] { "parent", "radius", "flags", "properties" }));
        Assert.That(actors.FieldRows.Select(row => row.Key), Has.None.EqualTo("states"));
        Assert.That(Row(actors, "properties").Children.Select(row => row.Key), Does.Contain("health").And.Not.Contain("monster.ammo"),
            "only the properties it sets");

        // Act
        Row(actors, "properties", "health").Text = "50";

        // Assert: shown as edited, and a change to save
        Assert.That(actors.IsChanged, Is.True);
        Assert.That(actors.KeyNumbers, Does.StartWith("health 50"));
        Assert.That(actors.UnsavedNames, Is.EqualTo(new[] { "actor Guard" }));
        Assert.That(actors.SelectedItem!.HasEdits, Is.True);
        Assert.That(actors.SelectedState, Is.EqualTo("Chase"), "it goes on playing");

        actors.UndoClassCommand.Execute(null);
        Assert.That(actors.IsChanged, Is.False);
        Assert.That(actors.KeyNumbers, Does.StartWith("health 25"));
        actors.RedoClassCommand.Execute(null);
        Assert.That(actors.KeyNumbers, Does.StartWith("health 50"));

        actors.SaveClassCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        var text = File.ReadAllText(Path.Combine(_mod, "actordefs", "actors.yaml"));
        Assert.That(text, Does.Contain("Guard:").And.Contain("50").And.Not.Contain("states").And.Not.Contain("dropitem"));
        Assert.That(actors.UnsavedNames, Is.Empty);

        // The game with the mod has the change, the rest of the class as it was
        var reloaded = ActorCatalog.Build(Load("wolf3d", _mod), levels: false).Single(entry => entry.Name == "Guard");
        Assert.That(reloaded.Merged!.Properties["health"].ToString(), Is.EqualTo("50"));
        Assert.That(reloaded.Merged.Properties["dropitem"], Is.EqualTo("DroppedClip"));
        Assert.That(reloaded.States.Keys, Does.Contain("Chase"));
    }

    [Test]
    public void A_Property_Added_Comes_Up_Empty_To_Fill_In()
    {
        var actors = new ActorBrowserViewModel();
        actors.Load(Load("wolf3d"));
        actors.Show("Guard");
        Assert.That(actors.AddableProperties.Select(choice => choice.Name), Does.Contain("monster.chasespeed").And.Not.Contain("health"),
            "a monster's, and not one it sets");
        Assert.That(actors.AddableProperties.Select(choice => choice.Name), Has.None.EqualTo("monster.ammo"), "only a BlakeMonster's");

        actors.PropertyToAdd = actors.AddableProperties.Single(choice => choice.Name == "monster.chasespeed");

        var row = Row(actors, "properties", "monster.chasespeed");
        Assert.That(row.Text, Is.Empty);
        Assert.That(actors.IsChanged, Is.False, "nothing's changed until it's filled in");
        row.Text = "2048";
        Assert.That(actors.IsChanged, Is.True);
        Assert.That(actors.AddableProperties.Select(choice => choice.Name), Has.None.EqualTo("monster.chasespeed"));
    }

    [Test]
    public void A_Parent_That_Is_No_Class_Is_A_Problem()
    {
        var actors = new ActorBrowserViewModel();
        actors.Load(Load("wolf3d"));
        actors.Show("Guard");

        Row(actors, "parent").Text = "NoSuchClass";

        Assert.That(actors.Problems, Has.Some.Contains("Parent actor NoSuchClass not found"));
        Assert.That(actors.Timeline, Is.Empty);
    }

    [Test]
    public void A_New_Class_Built_On_One_Is_Saved_With_Its_Map_Things()
    {
        // Arrange: named MyGuard, placed by things 2000-2007
        var answers = new Queue<string>(["My Guard", "MyGuard", "2000"]);
        var actors = new ActorBrowserViewModel
        {
            PickMod = _ => Task.FromResult<string?>(_mod),
            AskText = (_, _, _, check) =>
            {
                // A name with a space is refused, as the window refuses it
                while (answers.Count > 0 && check(answers.Peek()) != null)
                    answers.Dequeue();
                return Task.FromResult<string?>(answers.Dequeue());
            },
        };
        actors.Load(Load("wolf3d"));
        actors.Show("Guard");

        // Act
        actors.NewChildCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        // Assert: listed, built on the Guard, playing as one
        Assert.That(actors.SelectedItem!.Name, Is.EqualTo("MyGuard"));
        Assert.That(actors.SelectedItem.IsDraft, Is.True);
        Assert.That(actors.Lineage, Is.EqualTo("Monster › Guard › MyGuard"));
        Assert.That(actors.SelectedState, Is.EqualTo("Chase"));
        Assert.That(actors.UnsavedNames, Is.EqualTo(new[] { "actor MyGuard" }));

        actors.SaveClassCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.That(File.ReadAllText(Path.Combine(_mod, "actordefs", "actors.yaml")), Does.Contain("MyGuard:").And.Contain("parent: Guard"));

        var reloaded = Load("wolf3d", _mod);
        Assert.That(reloaded.Actors["MyGuard"].Parent, Is.EqualTo("Guard"));
        Assert.That(Enumerable.Range(2000, 8).Select(id => reloaded.MapDefs.Things[id].Class), Is.All.EqualTo("MyGuard"));
        Assert.That(reloaded.MapDefs.Things[2001].Angles, Is.EqualTo(90));
        Assert.That(reloaded.MapDefs.Things[2004].Patrol, Is.EqualTo(1));
    }

    [Test]
    public void A_Copy_Is_The_Class_As_Written_Under_A_New_Name()
    {
        var answers = new Queue<string>(["GuardCopy", ""]);
        var actors = new ActorBrowserViewModel
        {
            PickMod = _ => Task.FromResult<string?>(_mod),
            AskText = (_, _, _, _) => Task.FromResult<string?>(answers.Dequeue()),
        };
        actors.Load(Load("wolf3d"));
        actors.Show("Guard");

        actors.NewCopyCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        actors.SaveClassCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        var copy = Load("wolf3d", _mod).Actors["GuardCopy"];
        Assert.That(copy.Parent, Is.EqualTo("Monster"));
        Assert.That(copy.States.Keys, Is.SupersetOf(new[] { "Spawn", "Chase", "Death" }));
        Assert.That(copy.Properties["dropitem"], Is.EqualTo("DroppedClip"));
        Assert.That(File.Exists(Path.Combine(_mod, "mapdefs", "things.yaml")), Is.False, "no map things asked for");
    }
}
