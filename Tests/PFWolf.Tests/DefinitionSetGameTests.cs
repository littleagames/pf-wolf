using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Definitions;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Tests;

/// <summary>
/// The definitions editor's merge of pfwolf.pk3's files against what the game loads. Needs a built
/// game folder (pfwolf.pk3 and the data files).
/// </summary>
[Category("GameData")]
public class DefinitionSetGameTests
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

    [TestCase("wolf3d")]
    [TestCase("spear")]
    [TestCase("wolf3d-shareware")]
    public void Merged_Mapdefs_Read_As_The_Games(string game)
    {
        // Arrange
        var content = Load(game);

        // Act
        var set = DefinitionSet.Load(content, DefinitionKind.MapDefs);
        var merged = YamlDataEntryLoader.DeserializeQuietly<MapObjectTranslationAsset>(YamlTree.ToText(set.Merged));

        // Assert
        Assert.That(set.Files.Where(file => file.Error != null), Is.Empty);
        Assert.That(merged.Things.Keys, Is.EquivalentTo(content.MapDefs.Things.Keys));
        Assert.That(merged.Walls.Keys, Is.EquivalentTo(content.MapDefs.Walls.Keys));
        Assert.That(merged.Doors.Keys, Is.EquivalentTo(content.MapDefs.Doors.Keys));
        foreach (var (id, thing) in content.MapDefs.Things)
            Assert.That(merged.Things[id], Is.EqualTo(thing), $"thing {id}");
        foreach (var (id, wall) in content.MapDefs.Walls)
            Assert.That((merged.Walls[id].North, merged.Walls[id].East), Is.EqualTo((wall.North, wall.East)), $"wall {id}");
        Assert.That(merged.Floors, Is.EqualTo(content.MapDefs.Floors).Using<MapFloorsTranslation>((a, b) =>
            a.AreaStart == b.AreaStart && a.AreaCount == b.AreaCount && a.Ambush == b.Ambush && a.SecretExit == b.SecretExit));
    }

    [TestCase("wolf3d")]
    [TestCase("spear")]
    public void Merged_Actors_Read_As_The_Games(string game)
    {
        // Arrange
        var content = Load(game);

        // Act
        var set = DefinitionSet.Load(content, DefinitionKind.Actors);
        var merged = YamlDataEntryLoader.DeserializeQuietly<Dictionary<string, ActorData>>(YamlTree.ToText(set.Merged));

        // Assert
        Assert.That(set.Files.Where(file => file.Error != null), Is.Empty);
        Assert.That(merged.Keys, Is.EquivalentTo(content.Actors.Keys));
        foreach (var (name, actor) in content.Actors)
        {
            Assert.That(merged[name].Parent, Is.EqualTo(actor.Parent), name);
            Assert.That(merged[name].Flags, Is.EquivalentTo(actor.Flags), name);
            Assert.That(merged[name].Properties.Keys, Is.EquivalentTo(actor.Properties.Keys), name);
            Assert.That(merged[name].States.Keys, Is.EquivalentTo(actor.States.Keys), name);
        }
    }

    [TestCase("wolf3d")]
    [TestCase("spear")]
    [TestCase("spear-demo")]
    public void Merged_Game_Info_Reads_As_The_Games(string game)
    {
        // Arrange
        var content = Load(game);

        // Act
        var set = DefinitionSet.Load(content, DefinitionKind.GameInfo);
        var merged = YamlDataEntryLoader.DeserializeQuietly<GameInfoAsset>(YamlTree.ToText(set.Merged));

        // Assert
        var gameInfo = content.GameInfo!;
        Assert.That(merged.Maps.Keys, Is.EqualTo(gameInfo.Maps.Keys));
        Assert.That(merged.Episodes.Keys, Is.EqualTo(gameInfo.Episodes.Keys));
        Assert.That(merged.Skills.Keys, Is.EqualTo(gameInfo.Skills.Keys));
        var zones = new Dictionary<int, ZoneInfo>();
        Assert.That(merged.DefaultMap with { Zones = zones }, Is.EqualTo(gameInfo.DefaultMap with { Zones = zones }));
        foreach (var (name, map) in gameInfo.Maps)
            Assert.That((merged.Maps[name].Next, merged.Maps[name].Music, merged.Maps[name].ParTime), Is.EqualTo((map.Next, map.Music, map.ParTime)), name);
    }

    [TestCase("wolf3d")]
    [TestCase("spear")]
    public void Merged_Factions_Read_As_The_Games(string game)
    {
        // Arrange
        var content = Load(game);

        // Act
        var set = DefinitionSet.Load(content, DefinitionKind.Factions);
        var merged = new FactionTable(YamlDataEntryLoader.DeserializeQuietly<FactionsAsset>(YamlTree.ToText(set.Merged)));

        // Assert: spear has no factions.yaml of its own, so it's Wolf3D's
        var loaded = new FactionTable(content.Assets.FindInGamePackIfAny<FactionsAsset>("factions"));
        Assert.That(set.Files, Is.Not.Empty);
        Assert.That(set.Files.Where(file => file.Error != null), Is.Empty);
        Assert.That(merged.Names, Is.EquivalentTo(loaded.Names));
        Assert.That((merged.PlayerFaction, merged.MonsterFaction), Is.EqualTo((loaded.PlayerFaction, loaded.MonsterFaction)));
        Assert.That(merged.Translation("allies", ["SS"]), Is.EqualTo(loaded.Translation("allies", ["SS"])));
        Assert.That(content.FactionNames, Is.SupersetOf(new[] { "allies", "axis" }));
        Assert.That(content.TranslationNames, Does.Contain("allies"));
    }

    [Test]
    public void Merged_Animations_Are_The_Games_With_The_Switch_Demo()
    {
        // Arrange
        var content = Load("wolf3d", Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "switch-demo"));

        // Act
        var set = DefinitionSet.Load(content, DefinitionKind.Animations);
        var merged = YamlDataEntryLoader.DeserializeQuietly<Dictionary<string, AnimationDefinition>>(YamlTree.ToText(set.Merged));

        // Assert
        Assert.That(set.Files.Where(file => file.Error != null), Is.Empty);
        Assert.That(merged.Keys, Is.EquivalentTo(content.Assets.Animations.Keys.Concat(content.Assets.AnimationProblems.Keys)));
        Assert.That(merged.Keys, Is.SupersetOf(new[] { "STICKYLEVER", "ALARMLEVER" }));
        Assert.That(content.Assets.AnimationProblems, Is.Empty);
        foreach (var (name, definition) in merged)
            Assert.That(content.Assets.Animations[name][0].TotalTics, Is.EqualTo(definition.Read(_ => { }).Frames.Sum(frame => frame.Tics)), name);
    }

    [Test]
    public void Game_Packs_List_Every_Release()
    {
        // Arrange
        var content = Load("wolf3d");

        // Act
        var set = DefinitionSet.Load(content, DefinitionKind.GamePackInfo);

        // Assert
        Assert.That(set.Files, Has.Count.EqualTo(1));
        Assert.That(set.Entries(baseOnly: true).Select(entry => entry.Name), Is.SupersetOf(new[] { "standalone", "wolf3d", "spear", "wolf3d-shareware" }));
    }

    [Test]
    public void Saving_A_Change_Into_A_Mod_Folder_Is_What_The_Game_Then_Loads()
    {
        // Arrange
        var mod = Path.Combine(Path.GetTempPath(), "pfwolf-defs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mod);
        try
        {
            var content = Load("wolf3d");
            var set = DefinitionSet.Load(content, DefinitionKind.Actors);
            var edited = (YamlMappingNode)YamlNodes.Clone(set.ValueOf(["Guard"], baseOnly: false)!);
            YamlNodes.Set(edited, ["properties", "health"], new YamlScalarNode("99"));

            // Act
            var write = set.PlanWrite(["Guard"], edited, mod);
            DefinitionSaver.Save(write);
            var reloaded = Load("wolf3d", mod);

            // Assert
            Assert.That(File.ReadAllText(Path.Combine(mod, "actordefs", "actors.yaml")), Is.EqualTo("Guard:\n  properties:\n    \"health\": 99\n"));
            Assert.That(Convert.ToString(reloaded.Actors["Guard"].Properties["health"]), Is.EqualTo("99"));
            Assert.That(reloaded.Actors["Guard"].Parent, Is.EqualTo(content.Actors["Guard"].Parent));
            Assert.That(DefinitionSet.Load(reloaded, DefinitionKind.Actors).FilesDefining(["Guard"]).Last().ModPath, Is.EqualTo(mod));
        }
        finally
        {
            Directory.Delete(mod, recursive: true);
        }
    }
}
