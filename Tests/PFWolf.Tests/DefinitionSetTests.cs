using PFWolf.Editor.Definitions;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Tests;

/// <summary>The definitions editor's file sets: merged as the game merges them, and changes written as overlays</summary>
public class DefinitionSetTests
{
    private const string Mod = @"C:\mods\mymod";

    private static readonly string Guards = """
        # The guards
        Guard:
          parent: Monster
          flags: [SHOOTABLE, COUNTKILL]
          properties:
            health: 25
            speed: 512
        Officer:
          parent: Guard
          properties:
            health: 50
        """.Replace("\r\n", "\n");

    private static readonly string Bosses = """
        Hans:
          parent: Monster
          properties:
            health: 850
        """.Replace("\r\n", "\n");

    private static DefinitionSet Actors(params DefinitionFile[] mods)
        => new(DefinitionKind.Actors, "wolf3d",
        [
            new DefinitionFile(null, "actordefs/native.yaml", DefinitionFile.SharedLayer, "Monster:\n  radius: 32\n"),
            new DefinitionFile(null, "actordefs/wolf3d/guards.yaml", "", Guards),
            new DefinitionFile(null, "actordefs/wolf3d/bosses.yaml", "", Bosses),
            .. mods,
        ]);

    private static YamlMappingNode Edit(DefinitionSet set, string className, Action<YamlMappingNode> change, bool baseOnly = false)
    {
        var edited = (YamlMappingNode)YamlNodes.Clone(set.ValueOf([className], baseOnly)!);
        change(edited);
        return edited;
    }

    [Test]
    public void Merged_Lays_Mods_Over_The_Base_Key_By_Key()
    {
        // Arrange
        var set = Actors(new DefinitionFile(Mod, "actordefs/tweaks.yaml", "", "Guard:\n  properties:\n    health: 40\n"));

        // Act
        var guard = set.ValueOf(["Guard"], baseOnly: false)!;

        // Assert
        Assert.That(((YamlScalarNode)YamlNodes.Get(guard, ["properties", "health"])!).Value, Is.EqualTo("40"));
        Assert.That(((YamlScalarNode)YamlNodes.Get(guard, ["properties", "speed"])!).Value, Is.EqualTo("512"));
        Assert.That(((YamlScalarNode)YamlNodes.Get(set.ValueOf(["Guard"], baseOnly: true), ["properties", "health"])!).Value, Is.EqualTo("25"));
        Assert.That(set.FilesDefining(["Guard"]).Select(file => file.EntryPath), Is.EqualTo(new[] { "actordefs/wolf3d/guards.yaml", "actordefs/tweaks.yaml" }));
    }

    [Test]
    public void Entries_List_Classes_And_Mapdefs_Table_Entries()
    {
        // Arrange
        var mapDefs = new DefinitionSet(DefinitionKind.MapDefs, "wolf3d",
        [
            new DefinitionFile(null, "mapdefs/wolf3d/walls.yaml", "", "walls:\n  '1': { North: A }\n  '2': { North: B }\n"),
            new DefinitionFile(null, "mapdefs/wolf3d/floors.yaml", "", "floors:\n  ambush: 106\n"),
        ]);

        // Act
        var actorNames = Actors().Entries(baseOnly: false).Select(entry => entry.Name);
        var mapEntries = mapDefs.Entries(baseOnly: false).Select(entry => YamlNodes.Describe(entry.Path));

        // Assert
        Assert.That(actorNames, Is.EquivalentTo(new[] { "Monster", "Guard", "Officer", "Hans" }));
        Assert.That(mapEntries, Is.EqualTo(new[] { "walls › 1", "walls › 2", "floors" }));
    }

    [Test]
    public void A_Change_In_A_New_Mod_File_Is_Only_What_Differs()
    {
        // Arrange
        var set = Actors();
        var edited = Edit(set, "Guard", guard =>
        {
            YamlNodes.Set(guard, ["properties", "health"], new YamlScalarNode("40"));
            YamlNodes.Set(guard, ["flags"], new YamlSequenceNode(new YamlScalarNode("SHOOTABLE")));
        });

        // Act
        var write = set.PlanWrite(["Guard"], edited, Mod);
        var after = set.With(write.File.WithText(write.NewText));

        // Assert
        Assert.That(write.File.EntryPath, Is.EqualTo("actordefs/actors.yaml"));
        Assert.That(write.File.IsNew, Is.True);
        Assert.That(write.NewText, Is.EqualTo("Guard:\n  flags: [SHOOTABLE]\n  properties:\n    health: 40\n"));
        Assert.That(YamlNodes.Equal(after.ValueOf(["Guard"], baseOnly: false), edited), Is.True);
    }

    [Test]
    public void Changing_Back_Takes_The_Key_Out_Of_The_Mod()
    {
        // Arrange
        var mod = new DefinitionFile(Mod, "actordefs/tweaks.yaml", "", "# my tweaks\nGuard:\n  properties:\n    health: 40\n    speed: 600\n");
        var set = Actors(mod);
        var edited = Edit(set, "Guard", guard => YamlNodes.Set(guard, ["properties", "health"], new YamlScalarNode("25")));

        // Act
        var write = set.PlanWrite(["Guard"], edited, Mod);

        // Assert
        Assert.That(write.File, Is.SameAs(mod));
        Assert.That(write.NewText, Is.EqualTo("# my tweaks\nGuard:\n  properties:\n    speed: 600\n"));
    }

    [Test]
    public void A_Key_Taken_Out_Is_Written_Remove()
    {
        // Arrange
        var set = Actors();
        var edited = Edit(set, "Guard", guard => YamlNodes.Set(guard, ["properties", "speed"], null));

        // Act
        var write = set.PlanWrite(["Guard"], edited, Mod);
        var after = set.With(write.File.WithText(write.NewText));

        // Assert
        Assert.That(write.NewText, Is.EqualTo("Guard:\n  properties:\n    speed: !remove\n"));
        Assert.That(YamlNodes.Get(after.ValueOf(["Guard"], baseOnly: false), ["properties", "speed"]), Is.Null);
    }

    [Test]
    public void Base_Mode_Changes_The_File_The_Class_Comes_From()
    {
        // Arrange
        var set = Actors();
        var edited = Edit(set, "Hans", hans => YamlNodes.Set(hans, ["properties", "health"], new YamlScalarNode("900")), baseOnly: true);

        // Act
        var write = set.PlanWrite(["Hans"], edited, modPath: null);

        // Assert
        Assert.That(write.File.EntryPath, Is.EqualTo("actordefs/wolf3d/bosses.yaml"));
        Assert.That(write.NewText, Is.EqualTo(Bosses.Replace("850", "900")));
    }

    [Test]
    public void Base_Mode_Puts_A_New_Class_With_Its_Parent()
    {
        // Arrange
        var set = Actors();
        var sergeant = new YamlMappingNode { { "parent", "Officer" } };

        // Act
        var write = set.PlanWrite(["Sergeant"], sergeant, modPath: null);

        // Assert
        Assert.That(write.File.EntryPath, Is.EqualTo("actordefs/wolf3d/guards.yaml"));
        Assert.That(write.NewText, Does.EndWith("    health: 50\nSergeant:\n  parent: Officer\n"));
    }

    [Test]
    public void A_Change_That_Would_Not_Read_Is_Refused()
    {
        // Arrange
        var set = Actors();
        var edited = Edit(set, "Guard", guard => YamlNodes.Set(guard, ["flags"], new YamlMappingNode { { "not", "a list" } }));

        // Act, Assert
        Assert.Throws<InvalidDataException>(() => set.PlanWrite(["Guard"], edited, Mod));
    }

    [Test]
    public void A_Mod_File_That_Does_Not_Read_Is_Left_Out()
    {
        // Arrange
        var broken = new DefinitionFile(Mod, "actordefs/broken.yaml", "", "Guard:\n  properties: [\n");

        // Act
        var set = Actors(broken);

        // Assert
        Assert.That(broken.Error, Is.Not.Null);
        Assert.That(((YamlScalarNode)YamlNodes.Get(set.ValueOf(["Guard"], baseOnly: false), ["properties", "health"])!).Value, Is.EqualTo("25"));
    }

    [Test]
    public void Extended_Classes_Write_Flags_Added_And_Taken_Away()
    {
        // Arrange
        var mod = new DefinitionFile(Mod, "actordefs/tweaks.yaml", "", "Guard:\n  extend: true\n  properties:\n    health: 40\n");
        var set = Actors(mod);
        var edited = Edit(set, "Guard", guard => YamlNodes.Set(guard, ["flags"],
            new YamlSequenceNode(new YamlScalarNode("SHOOTABLE"), new YamlScalarNode("AMBUSH"))));

        // Act
        var write = set.PlanWrite(["Guard"], edited, Mod);
        var after = set.With(write.File.WithText(write.NewText));

        // Assert
        Assert.That(write.NewText, Does.Contain("flags: [AMBUSH, ~COUNTKILL]"));
        Assert.That(write.NewText, Does.Contain("extend: true"));
        Assert.That(YamlNodes.Equal(YamlNodes.Get(after.ValueOf(["Guard"], baseOnly: false), ["flags"]), YamlNodes.Get(edited, ["flags"])), Is.True);
    }

    [Test]
    public void Game_Info_Changes_Go_In_The_Mods_Root_File()
    {
        // Arrange
        var set = new DefinitionSet(DefinitionKind.GameInfo, "wolf3d",
        [
            new DefinitionFile(null, "gamepacks/wolf3d/game-info.yaml", "", "default-map:\n  floor-color: '#717171'\n  ceiling-color: '#383838'\n"),
        ]);
        var edited = (YamlMappingNode)YamlNodes.Clone(set.ValueOf(["default-map"], baseOnly: false)!);
        YamlNodes.Set(edited, ["ceiling-color"], new YamlScalarNode("#000000"));

        // Act
        var write = set.PlanWrite(["default-map"], edited, Mod);

        // Assert
        Assert.That(write.File.EntryPath, Is.EqualTo("game-info.yaml"));
        Assert.That(write.NewText, Is.EqualTo("default-map:\n  ceiling-color: '#000000'\n"));
    }

    [Test]
    public void Base_Mode_Saves_Into_The_Pk3_And_Its_Source_Folder()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "pfwolf-base-" + Guid.NewGuid().ToString("N"));
        var gameFolder = Path.Combine(root, "game");
        var sourceFolder = Path.Combine(root, DefinitionSaver.BaseSourceFolderName);
        Directory.CreateDirectory(gameFolder);
        Directory.CreateDirectory(Path.Combine(sourceFolder, "gamepacks"));
        Directory.CreateDirectory(Path.Combine(sourceFolder, "mapdefs", "wolf3d"));
        File.WriteAllText(Path.Combine(sourceFolder, "gamepacks", "gamepack-info.yaml"), "wolf3d:\n  title: Wolfenstein 3D\n");
        // The source has a comment the built pk3 doesn't yet: the change goes into each as it is
        const string built = "walls:\n  '1': { North: A, South: A }\n";
        File.WriteAllText(Path.Combine(sourceFolder, "mapdefs", "wolf3d", "walls.yaml"), "# Walls\n" + built);
        Editor.Editing.ModFiles.CreateArchive(Path.Combine(gameFolder, "pfwolf.pk3"), [("mapdefs/wolf3d/walls.yaml", System.Text.Encoding.UTF8.GetBytes(built))]);
        try
        {
            var set = new DefinitionSet(DefinitionKind.MapDefs, "wolf3d", [new DefinitionFile(null, "mapdefs/wolf3d/walls.yaml", "", built)]);
            var edited = new YamlMappingNode { { "North", "B" }, { "South", "A" } };
            edited.Style = YamlDotNet.Core.Events.MappingStyle.Flow;

            // Act
            var write = set.PlanWrite(["walls", "1"], edited, modPath: null);
            var places = DefinitionSaver.Save(write, gameFolder, DefinitionSaver.FindBaseSourceFolder(gameFolder));

            // Assert
            Assert.That(places, Has.Count.EqualTo(2));
            Assert.That(Editor.Editing.ModFiles.ReadText(Path.Combine(gameFolder, "pfwolf.pk3"), "mapdefs/wolf3d/walls.yaml"),
                Is.EqualTo("walls:\n  '1': { North: B, South: A }\n"));
            Assert.That(File.ReadAllText(Path.Combine(sourceFolder, "mapdefs", "wolf3d", "walls.yaml")),
                Is.EqualTo("# Walls\nwalls:\n  '1': { North: B, South: A }\n"));
            Assert.That(File.Exists(Path.Combine(gameFolder, "pfwolf.pk3.bak")), Is.True);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Taking_Out_A_Mods_Change_Leaves_The_Rest_Of_Its_File()
    {
        // Arrange
        var mod = new DefinitionFile(Mod, "actordefs/tweaks.yaml", "", "Guard:\n  properties:\n    health: 40\nHans:\n  properties:\n    health: 1\n");
        var set = Actors(mod);

        // Act
        var write = set.PlanTakeOut(["Guard"], Mod);

        // Assert
        Assert.That(write.NewText, Is.EqualTo("Hans:\n  properties:\n    health: 1\n"));
        Assert.That(set.ModChanges(["Hans"], Mod), Is.True);
    }

    [Test]
    public void Mapdefs_Entries_Go_In_A_File_Named_For_Their_Table()
    {
        // Arrange
        var set = new DefinitionSet(DefinitionKind.MapDefs, "wolf3d",
        [
            new DefinitionFile(null, "mapdefs/wolf3d/walls.yaml", "", "walls:\n  '1': { North: A, South: A }\n"),
        ]);

        // Act
        var write = set.PlanWrite(["walls", "70"], new YamlMappingNode { { "North", "X" }, { "South", "X" } }, Mod);

        // Assert
        Assert.That(write.File.EntryPath, Is.EqualTo("mapdefs/walls.yaml"));
        Assert.That(YamlNodes.Equal(YamlNodes.Get(YamlTree.Parse(write.NewText), ["walls", "70"]), new YamlMappingNode { { "North", "X" }, { "South", "X" } }), Is.True);
    }
}
