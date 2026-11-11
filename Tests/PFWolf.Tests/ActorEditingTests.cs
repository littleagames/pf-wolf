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

    // The Guard picked, its Attack state paused on its first frame
    private ActorBrowserViewModel GuardAttack(Func<string, string?>? answer = null)
    {
        var actors = new ActorBrowserViewModel
        {
            PickMod = _ => Task.FromResult<string?>(_mod),
            AskText = (_, _, _, check) => Task.FromResult(answer?.Invoke("")),
        };
        actors.Load(Load("wolf3d"));
        actors.Show("Guard");
        actors.SelectedState = "Attack";
        actors.Playing = false;
        return actors;
    }

    [Test]
    public void A_Frames_Tics_Changed_Play_And_Save_As_The_State()
    {
        // Arrange
        var actors = GuardAttack();
        Assert.That(actors.CanEditFrame, Is.True);
        Assert.That(actors.FrameIndex, Is.EqualTo(0));
        Assert.That((actors.FrameSprite, actors.FrameLetter?.Letter, actors.FrameTics), Is.EqualTo(("GARD", "F", 20m)));
        Assert.That(actors.LetterChoices.Select(choice => choice.Letter), Is.SupersetOf(new[] { "A", "F", "N" }));

        // Act
        actors.FrameTics = 30;

        // Assert: the timeline and the preview are the state as changed, still on the frame
        Assert.That(actors.IsChanged, Is.True);
        Assert.That(actors.SelectedState, Is.EqualTo("Attack"));
        Assert.That(actors.Timeline.Select(frame => frame.Tics), Is.EqualTo(new[] { "30", "20", "20" }));
        Assert.That(actors.FrameIndex, Is.EqualTo(0));
        actors.UndoClassCommand.Execute(null);
        Assert.That(actors.Timeline[0].Tics, Is.EqualTo("20"));
        actors.RedoClassCommand.Execute(null);

        actors.SaveClassCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        var text = File.ReadAllText(Path.Combine(_mod, "actordefs", "actors.yaml"));
        Assert.That(text, Does.Contain("Attack:").And.Contain("30").And.Not.Contain("Chase:").And.Not.Contain("Death:"));

        var guard = ActorCatalog.Build(Load("wolf3d", _mod), levels: false).Single(entry => entry.Name == "Guard");
        Assert.That(ActorCatalog.FramesOf(guard.States["Attack"]).Select(frame => frame.TicTime), Is.EqualTo(new short[] { 30, 20, 20 }));
        Assert.That(ActorCatalog.FramesOf(guard.States["Attack"])[1].Action, Is.EqualTo("T_Shoot"));
        Assert.That(ActorCatalog.FramesOf(guard.States["Chase"]), Has.Count.EqualTo(6), "the rest of its states as they were");
        Assert.That(guard.States["Attack"].Next!.Next!.Next!.StateName, Is.EqualTo("Chase"), "it still goes on into Chase");
    }

    [Test]
    public void Frames_Are_Added_Moved_And_Taken_Out()
    {
        var actors = GuardAttack();

        actors.AddFrameCommand.Execute(null);
        Assert.That(actors.Timeline.Select(frame => frame.Letter), Is.EqualTo(new[] { "F", "G", "G", "H" }), "the next letter, after F");
        Assert.That(actors.FrameIndex, Is.EqualTo(1));
        Assert.That(actors.Timeline[1].HasAction, Is.False);

        actors.MoveFrameRightCommand.Execute(null);
        Assert.That(actors.Timeline[2].HasAction, Is.False, "the new frame after the shooting one");
        Assert.That(actors.FrameIndex, Is.EqualTo(2));

        actors.MoveFrame(2, 0);
        Assert.That(actors.Timeline[0].HasAction, Is.False);

        actors.DeleteFrameCommand.Execute(null);
        Assert.That(actors.Timeline.Select(frame => frame.Letter), Is.EqualTo(new[] { "F", "G", "H" }));
        Assert.That(actors.Timeline[1].HasAction, Is.True);
    }

    [Test]
    public void A_State_Ends_As_Picked()
    {
        var actors = GuardAttack();
        Assert.That(actors.SelectedEnd!.Label, Is.EqualTo("goes on to Chase"));

        actors.SelectedEnd = actors.EndChoices.Single(choice => choice.Label == "loops back to its first frame");
        Assert.That(actors.TimelineEnd, Is.EqualTo("↺ loops"));

        actors.SelectedEnd = actors.EndChoices.Single(choice => choice.Label == "stops on its last frame");
        Assert.That(actors.TimelineEnd, Is.EqualTo("↺ its last frame again (stop)"));
    }

    [Test]
    public void States_Are_Added_Renamed_And_Taken_Out()
    {
        // Arrange: answers to New state, then Rename
        var answers = new Queue<string>(["Melee", "Bite"]);
        var actors = GuardAttack(_ => answers.Dequeue());

        // A new state: the frame shown, looping
        actors.AddStateCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.That(actors.SelectedState, Is.EqualTo("Melee"));
        Assert.That(actors.Timeline.Select(frame => frame.Letter), Is.EqualTo(new[] { "F" }));
        Assert.That(actors.StateIsOwn, Is.True);

        // Attack going on to it follows a rename
        actors.SelectedState = "Attack";
        actors.Playing = false;
        actors.SelectedEnd = actors.EndChoices.Single(choice => choice.Label == "goes on to Melee");
        actors.SelectedState = "Melee";
        actors.RenameStateCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.That(actors.States, Does.Contain("Bite").And.Not.Contain("Melee"));
        actors.SelectedState = "Attack";
        actors.Playing = false;
        Assert.That(actors.SelectedEnd!.Label, Is.EqualTo("goes on to Bite"));

        // Taken out
        actors.SelectedState = "Bite";
        actors.DeleteStateCommand.Execute(null);
        Assert.That(actors.States, Has.None.EqualTo("Bite"));
    }

    [Test]
    public void A_Parents_State_Changed_Becomes_The_Class_Own_And_A_Made_Raise_Is_Only_Shown()
    {
        // Arrange: a class built on the Guard, with no states of its own
        var answers = new Queue<string>(["MyGuard", ""]);
        var actors = new ActorBrowserViewModel { AskText = (_, _, _, _) => Task.FromResult<string?>(answers.Dequeue()) };
        actors.Load(Load("wolf3d"));
        actors.Show("Guard");
        actors.NewChildCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        actors.SelectedState = "Chase";
        actors.Playing = false;
        Assert.That(actors.StateNote, Does.Contain("Guard's"));
        Assert.That(actors.StateIsOwn, Is.False);

        // Act
        actors.FrameTics = 4;

        // Assert
        Assert.That(actors.StateIsOwn, Is.True);
        Assert.That(actors.StateNote, Is.Empty);
        Assert.That(actors.Timeline[0].Tics, Is.EqualTo("4"));

        // The Raise the engine makes from its Death can't be changed here
        actors.SelectedState = "Raise";
        actors.Playing = false;
        Assert.That(actors.StateEditable, Is.False);
        Assert.That(actors.StateNote, Does.Contain("made by the engine"));
    }

    [Test]
    public void The_Preview_Says_The_Sounds_Its_Actions_Make_Quietly_Unless_Asked()
    {
        // Arrange: sounds off (the default): nothing plays, the line still says what would
        var actors = GuardAttack();
        Assert.That(actors.PlaySounds, Is.False);
        actors.Playing = true;

        // Act: its shot (T_Shoot as the second frame ends)
        actors.Tick(40);

        // Assert
        Assert.That(actors.SoundText, Is.EqualTo("Sound: guard/attack"));

        actors.SelectedState = "Death";
        actors.Tick(20);
        Assert.That(actors.SoundText, Is.EqualTo("Sound: guard/death"));
    }

    [Test]
    public void Sounds_Are_The_Actions_Own_Or_The_Class_Property_The_Engine_Plays()
    {
        var guard = ActorCatalog.Build(Load("wolf3d"), levels: false).Single(entry => entry.Name == "Guard").Merged!;

        Assert.That(ActorSoundPreview.SoundOf("A_PlaySound(\"misc/yeah\", \"positional\")", guard), Is.EqualTo("misc/yeah"));
        Assert.That(ActorSoundPreview.SoundOf("A_DeathScream", guard), Is.EqualTo("guard/death"));
        Assert.That(ActorSoundPreview.SoundOf("T_Shoot", guard), Is.EqualTo("guard/attack"));
        Assert.That(ActorSoundPreview.SoundOf("A_Look", guard), Is.Null);
        Assert.That(ActorSoundPreview.SoundOfGoing("Spawn", "Chase", guard), Is.EqualTo("guard/sight"));
        Assert.That(ActorSoundPreview.SoundOfGoing("Attack", "Chase", guard), Is.Null);
    }

    [Test]
    public void Edit_Picture_Asks_For_The_Frames_Sprite_Or_A_View_It_Lacks()
    {
        var actors = GuardAttack();
        var asked = new List<SpritePictureRequest>();
        actors.PictureEditRequested += (_, request) => asked.Add(request);

        // The frame shown: a guard shooting faces the player, a sprite with one view
        actors.SelectedView = 3;
        actors.EditPictureCommand.Execute(null);
        Assert.That(asked[^1], Is.EqualTo(new SpritePictureRequest("GARDF0", true)));

        // A sprite the game hasn't got: started blank
        actors.FrameSprite = "ZZZZ";
        actors.EditPictureCommand.Execute(null);
        Assert.That(asked[^1], Is.EqualTo(new SpritePictureRequest("ZZZZF3", false)));
    }

    [Test]
    public void A_Class_A_Mapdefs_Thing_Places_Can_Be_Put_On_The_Map()
    {
        var actors = new ActorBrowserViewModel();
        actors.Load(Load("wolf3d"));
        var placed = new List<string>();
        actors.PlaceRequested += (_, name) => placed.Add(name);

        actors.Show("Guard");
        actors.PlaceOnMapCommand.Execute(null);
        actors.Show("Monster");
        actors.PlaceOnMapCommand.Execute(null);

        Assert.That(placed, Is.EqualTo(new[] { "Guard" }));
        Assert.That(actors.Status, Does.Contain("No mapdefs thing is a Monster"));
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
