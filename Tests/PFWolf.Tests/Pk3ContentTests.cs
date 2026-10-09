using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>
/// Checks over the repository's own pfwolf-pk3 folder: that every game pack's files load, and
/// that its actordefs hang together. Run as the game would load them, pack by pack.
/// </summary>
[Category("Content")]
public class Pk3ContentTests
{
    // GameEngineManager's game types: pack id (the folders' name) and release id (gamepack-info's key)
    private static readonly (string Pack, string Release)[] Games =
    [
        ("wolf3d", "wolf3d"),
        ("spear", "spear"),
        ("blake", "blake-aog"),
        ("planetstrike", "blake-ps"),
        ("wolf3d-shareware", "wolf3d-shareware"),
        ("wolf3d-apogee", "wolf3d-apogee"),
        ("spear-demo", "spear-demo"),
    ];

    private static IEnumerable<TestCaseData> GameCases() =>
        Games.Select(game => new TestCaseData(game.Pack, game.Release).SetArgDisplayNames(game.Pack));

    private static PfWolfPk3Loader Load(string pack, string release) =>
        new([new DirectoryAssetSource(TestPaths.Pk3SourceFolder())], pack, release);

    [TestCaseSource(nameof(GameCases))]
    public void Every_Game_Packs_Files_Load_Without_Warnings(string pack, string release)
    {
        // Act
        var loader = Load(pack, release);

        // Assert
        Assert.That(loader.Warnings, Is.Empty);
    }

    [TestCaseSource(nameof(GameCases))]
    public void Every_Actor_Class_Hangs_Together(string pack, string release)
    {
        // Act
        var problems = ActorProblems(Load(pack, release), pack);

        // Assert
        Assert.That(problems, Is.Empty);
    }

    // What's wrong with the loaded actordefs' classes: a parent or next-state that isn't there,
    // or a class that can't be created. As AssetManager.GetActorMetadata builds them.
    private static List<string> ActorProblems(PfWolfPk3Loader loader, string pack)
    {
        var metadata = new ActorMetadata();
        foreach (var name in new[] { "actordefs", $"{pack}/actordefs" })
        {
            try
            {
                metadata.AddActors(loader.Load<ActorTranslationAsset>(name).Actors);
            }
            catch (KeyNotFoundException)
            {
                // a pack with no actordefs of its own
            }
        }

        Assert.That(metadata.Actors.Keys, Does.Contain("Inventory").And.Contain("Monster"), "the shared actordefs should load");

        var problems = new List<string>();
        foreach (var (className, data) in metadata.Actors.ToList())
        {
            if (!string.IsNullOrWhiteSpace(data.Parent) && !metadata.Actors.ContainsKey(data.Parent))
            {
                problems.Add($"{className}: parent {data.Parent} isn't a class");
                continue;
            }

            Entities.Actors.Actor actor;
            try
            {
                actor = metadata.CreateActor(className, data);
            }
            catch (Exception e)
            {
                problems.Add($"{className}: can't be created: {e.Message}");
                continue;
            }

            foreach (var (state, entries) in actor.States)
            {
                foreach (var entry in entries)
                {
                    var target = entry switch
                    {
                        ActorStatesData frames => frames.NextState,
                        GoToStateData goTo => goTo.NextState,
                        _ => null,
                    };
                    if (!string.IsNullOrEmpty(target) && !actor.States.ContainsKey(target))
                        problems.Add($"{className}: {state} goes to {target}, which it has no state called");
                }
            }
        }
        return problems;
    }

    [Test]
    public void Every_Wolf3d_Deathmatch_Arena_Has_A_Map_With_Starts()
    {
        // Arrange
        var loader = Load("wolf3d", "wolf3d");
        var gameInfo = loader.Load<GameInfoAsset>("wolf3d/game-info");
        var mapdefs = loader.Load<MapObjectTranslationAsset>("wolf3d/mapdefs");
        var arenas = gameInfo.Maps.Where(m => m.Value.Deathmatch).Select(m => m.Key).ToList();
        Assert.That(arenas, Is.Not.Empty, "wolf3d's game-info should list deathmatch arenas");

        // Act
        var problems = new List<string>();
        foreach (var name in arenas)
        {
            var map = loader.Load<MapAsset>(name);
            var objects = map.MapData[1];
            int deathmatchStarts = objects.Count(o => mapdefs.PlayerStarts.TryGetValue(o, out var s) && s.Deathmatch);
            int ownStarts = objects.Count(o => mapdefs.PlayerStarts.TryGetValue(o, out var s) && !s.Deathmatch);
            if (deathmatchStarts < 4)
                problems.Add($"{name}: {deathmatchStarts} deathmatch starts, fewer than 4 players");
            if (ownStarts != 1)
                problems.Add($"{name}: {ownStarts} player starts of its own, not 1");
            if (gameInfo.Episodes.Values.Any(e => e.StartMap == name))
                problems.Add($"{name}: an episode starts on it");
        }

        // Assert
        Assert.That(problems, Is.Empty);
    }

    [TestCase("wolf3d", "wolf3d")]
    [TestCase("spear", "spear")]
    public void Multiplayer_Pk3_Loads_As_A_Mod_And_Its_Actors_Hang_Together(string pack, string release)
    {
        // Act
        var loader = new PfWolfPk3Loader([new DirectoryAssetSource(TestPaths.Pk3SourceFolder())], pack, release,
            [new DirectoryAssetSource(Path.Combine(TestPaths.RepoRoot(), "multiplayer-pk3"))]);
        var problems = ActorProblems(loader, pack);
        var weapons = loader.Load<ActorTranslationAsset>($"{pack}/actordefs").Actors.Keys;

        // Assert
        Assert.That(loader.Warnings, Is.Empty);
        Assert.That(problems, Is.Empty);
        Assert.That(weapons, Does.Contain("RocketLauncher").And.Contain("FlameThrower").And.Contain("PlayerRocket"));
    }

    [Test]
    public void A_Mod_Can_Remove_Actordefs_And_Mapdefs_Keys()
    {
        // Arrange: a mod taking out the Guard's Path state and the puddle's things: entry
        var modFolder = Path.Combine(Path.GetTempPath(), $"pfwolf-remove-mod-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(modFolder, "actordefs"));
        Directory.CreateDirectory(Path.Combine(modFolder, "mapdefs"));
        File.WriteAllText(Path.Combine(modFolder, "actordefs", "guards.yaml"), "Guard:\n  states:\n    Path: !remove\n");
        File.WriteAllText(Path.Combine(modFolder, "mapdefs", "decorations.yaml"), "things:\n  23: !remove\n");

        try
        {
            // Act
            var loader = new PfWolfPk3Loader([new DirectoryAssetSource(TestPaths.Pk3SourceFolder())], "wolf3d", "wolf3d",
                [new DirectoryAssetSource(modFolder)]);
            var guard = loader.Load<ActorTranslationAsset>("wolf3d/actordefs").Actors["Guard"];
            var mapdefs = loader.Load<MapObjectTranslationAsset>("wolf3d/mapdefs");

            // Assert
            Assert.That(loader.Warnings, Is.Empty);
            Assert.That(guard.States.Keys, Does.Contain("Spawn").And.Not.Contain("Path"));
            Assert.That(mapdefs.Things.Keys, Does.Not.Contain(23).And.Contain(24));
        }
        finally
        {
            Directory.Delete(modFolder, recursive: true);
        }
    }

    [Test]
    public void Multiplayer_Pk3_Sprites_Have_The_See_Through_Color_Behind_Them()
    {
        // Arrange: SplitWolf's pictures, BMPs with a background color for see-through (152,0,136)
        var files = Directory.GetFiles(Path.Combine(TestPaths.RepoRoot(), "multiplayer-pk3", "sprites"), "*.bmp",
            new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive });
        Assert.That(files, Is.Not.Empty);

        // Act: the top-left corner of each, which none of them draws on
        var problems = new List<string>();
        foreach (var file in files)
        {
            var image = ImageDecoder.Decode(file);
            var corner = image[0, 0];
            if (corner.R != 152 || corner.G != 0 || corner.B != 136)
                problems.Add($"{Path.GetFileName(file)}: {corner.R},{corner.G},{corner.B} behind it, which is drawn");
        }

        // Assert
        Assert.That(problems, Is.Empty);
    }

    [Test]
    public void Multiplayer_Pk3_Gives_Views_2_To_8_Of_Frames_The_Games_Draw_From_One_Side()
    {
        // Arrange: the VSWAP sprite names Wolf3D and Spear give (raw-data-map.yaml)
        var gameSprites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pack in new[] { "wolf3d", "spear" })
        {
            var map = File.ReadAllLines(Path.Combine(TestPaths.Pk3SourceFolder(), "gamepacks", pack, "raw-data-map.yaml"));
            gameSprites.UnionWith(map.Select(line => line.Trim(' ', '-')).Where(name => name.Length == 6));
        }
        var views = Directory.GetFiles(Path.Combine(TestPaths.RepoRoot(), "multiplayer-pk3", "sprites", "enemies"))
            .Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.That(views, Is.Not.Empty);

        // Act
        var problems = new List<string>();
        foreach (var frame in views.Select(v => v![..5]).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!gameSprites.Contains($"{frame}0"))
                problems.Add($"{frame}: the games have no single view {frame}0 for it to turn");
            foreach (var view in Enumerable.Range(2, 7).Where(r => !views.Contains($"{frame}{r}")))
                problems.Add($"{frame}: no view {view}");
        }

        // Assert
        Assert.That(problems, Is.Empty);
    }
}
