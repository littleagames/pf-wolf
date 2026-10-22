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

    // With the Multiplayer mod (multiplayer-pk3), which has Wolf3D's arenas
    private static PfWolfPk3Loader LoadWithMultiplayer(string pack, string release) =>
        new([new DirectoryAssetSource(TestPaths.Pk3SourceFolder())], pack, release,
            [new DirectoryAssetSource(Path.Combine(TestPaths.RepoRoot(), "multiplayer-pk3"))]);

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

    [Test]
    public void Wolf3Ds_Death_Cam_Settings_Are_Read()
    {
        // Arrange: the key is death-cam; a misspelt one (deathcam) is skipped without a word
        var path = Path.Combine(TestPaths.Pk3SourceFolder(), "gamepacks", "wolf3d", "intermission.yaml");

        // Act
        var intermission = YamlDataEntryLoader.Deserialize<IntermissionAsset>(File.ReadAllText(path));

        // Assert
        Assert.That(intermission.DeathCam, Is.Not.Null);
        Assert.That(intermission.DeathCam!.Labels, Is.Not.Empty);
        Assert.That(intermission.DeathCam.HoldTics, Is.GreaterThan(0));
    }

    // A game's sprites: its data files' (raw-data-map.yaml's, by chunk) and pfwolf-pk3's sprites/
    // pictures. Planet Strike starts from Aliens of Gold's classes and map objects, so a class one
    // game draws can name frames the other lettered differently, or hasn't got; the shareware and
    // demo data files have fewer sprites than the full games whose classes they use. Classes
    // count when a map places them, a Horde wave sends them, or a class that counts names them
    // (a property, as a drop or an ammo type, or an action's argument, as a projectile).
    [TestCaseSource(nameof(GameCases))]
    public void Every_Class_A_Game_Places_Draws_Frames_It_Has(string pack, string release)
    {
        // Arrange
        var loader = Load(pack, release);
        var metadata = Metadata(loader, pack);
        var placed = Spawned(metadata, loader.Load<MapObjectTranslationAsset>($"{pack}/mapdefs").Things.Values
            .Select(thing => thing.Class)
            .Concat(loader.Load<GameInfoAsset>($"{pack}/game-info").Horde.Waves.SelectMany(wave => wave.Enemies.Keys)));
        var sprites = new[] { pack }.Concat(PfWolfPk3Loader.ReadBasePackIds(new DirectoryAssetSource(TestPaths.Pk3SourceFolder()), release))
            .Select(id => Path.Combine(TestPaths.Pk3SourceFolder(), "gamepacks", id, "raw-data-map.yaml"))
            .Where(File.Exists).SelectMany(File.ReadAllLines)      // a shareware or demo pack's sprites are its base pack's
            .Select(line => line.Split('#')[0].Trim(' ', '-')).Where(name => name.Length == 6)
            .Concat(Directory.GetFiles(Path.Combine(TestPaths.Pk3SourceFolder(), "sprites"), "*", SearchOption.AllDirectories)
                .Select(Path.GetFileNameWithoutExtension).OfType<string>())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.That(placed, Is.Not.Empty);

        // Act: each frame of each state, in any of its views (0 for one alone, 1-8 turning)
        var problems = new List<string>();
        foreach (var className in placed)
        {
            var actor = metadata.CreateActor(className, metadata.Actors[className]);
            var missing = actor.States.Values.SelectMany(entries => entries.OfType<ActorStatesData>())
                .Where(frames => frames.Sprite is { Length: > 0 } && frames.Sprite != "TNT1")
                .SelectMany(frames => frames.Frames.Select(letter => $"{frames.Sprite}{letter}"))
                .Where(frame => !Enumerable.Range(0, 9).Any(view => sprites.Contains($"{frame}{view}")))
                .Distinct().ToList();
            if (missing.Count > 0)
                problems.Add($"{className}: {string.Join(" ", missing)}");
        }

        // Assert
        Assert.That(problems, Is.Empty);
    }

    // The classes among those given, and every class they name in turn: in a property (theirs or
    // a parent's) or as an argument to a state's action or think
    private static List<string> Spawned(ActorMetadata metadata, IEnumerable<string> classes)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(classes.Where(metadata.Actors.ContainsKey));
        while (queue.TryDequeue(out var className))
        {
            if (!found.Add(className))
                continue;
            for (var name = className; !string.IsNullOrEmpty(name) && metadata.Actors.TryGetValue(name, out var data); name = data.Parent)
            {
                var named = data.Properties.Values.SelectMany(value => value is IEnumerable<object> list ? list : [value])
                    .Select(value => value?.ToString() ?? "")
                    .Concat(data.States.Values.SelectMany(frames => frames.OfType<ActorStatesData>())
                        .SelectMany(frames => new[] { frames.Action, frames.Think })
                        .Where(call => !string.IsNullOrWhiteSpace(call))
                        .SelectMany(call => Entities.Actors.ActorActionRegistry.Parse(call).Args.Select(arg => arg.Trim('"'))));
                foreach (var other in named.Where(metadata.Actors.ContainsKey))
                    queue.Enqueue(other);
            }
        }
        return [.. found];
    }

    // The game's actor classes, the shared ones and then its own, as AssetManager.GetActorMetadata builds them
    private static ActorMetadata Metadata(PfWolfPk3Loader loader, string pack)
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
        return metadata;
    }

    // What's wrong with the loaded actordefs' classes: a parent or next-state that isn't there,
    // or a class that can't be created. As AssetManager.GetActorMetadata builds them.
    private static List<string> ActorProblems(PfWolfPk3Loader loader, string pack)
    {
        var metadata = Metadata(loader, pack);

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
            List<string> ownSettings = [.. data.Properties.Keys, .. data.Flags];     // before its parents' are merged in
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

            // A next-state, goto, stop or loop that's never reached (not on its state's last entry)
            Entities.Actors.ActorStateResolver.Resolve(actor.States,
                (state, _, marker) => problems.Add(Entities.Actors.ActorStateResolver.IgnoredMessage(className, state, marker)));

            // A frame modifier the engine doesn't read
            foreach (var (state, entries) in actor.States)
                foreach (var modifier in Entities.Actors.ActorStateResolver.UnknownModifiers(entries))
                    problems.Add(Entities.Actors.ActorStateResolver.UnknownModifierMessage(className, state, modifier));

            // A property or flag only another kind of actor reads (a BlakeMonster's, on a Wolf3D enemy)
            foreach (var (setting, needs) in ActorMetadata.MisplacedSettings(ownSettings, actor))
                problems.Add(ActorMetadata.MisplacedMessage(className, setting, needs));
        }
        return problems;
    }

    [Test]
    public void Every_Example_Mods_States_Are_Read_As_Written()
    {
        // Arrange: each example mod's and the Multiplayer mod's actordefs files, as written
        var root = TestPaths.RepoRoot();
        var files = Directory.GetDirectories(Path.Combine(root, "examples", "mods")).Append(Path.Combine(root, "multiplayer-pk3"))
            .Select(mod => Path.Combine(mod, "actordefs")).Where(Directory.Exists)
            .SelectMany(folder => Directory.GetFiles(folder, "*.yaml", SearchOption.AllDirectories))
            .ToList();
        Assert.That(files, Is.Not.Empty);

        // Act
        var problems = new List<string>();
        foreach (var file in files)
            foreach (var (className, data) in ActorDefs.Parse(File.ReadAllText(file)))
            {
                Entities.Actors.ActorStateResolver.Resolve(data.States, (state, _, marker) =>
                    problems.Add($"{Path.GetRelativePath(root, file)}: {Entities.Actors.ActorStateResolver.IgnoredMessage(className, state, marker)}"));
                foreach (var (state, entries) in data.States)
                    foreach (var modifier in Entities.Actors.ActorStateResolver.UnknownModifiers(entries))
                        problems.Add($"{Path.GetRelativePath(root, file)}: {Entities.Actors.ActorStateResolver.UnknownModifierMessage(className, state, modifier)}");
            }

        // Assert
        Assert.That(problems, Is.Empty);
    }

    [Test]
    public void Every_Wolf3d_Deathmatch_Arena_Has_A_Map_With_Starts()
    {
        // Arrange
        var loader = LoadWithMultiplayer("wolf3d", "wolf3d");
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
            int StartsOf(string team) => objects.Count(o => mapdefs.PlayerStarts.TryGetValue(o, out var s) && s.Team == team);
            int ownStarts = objects.Count(o => mapdefs.PlayerStarts.TryGetValue(o, out var s) && !s.Deathmatch && s.Team.Length == 0);
            if (gameInfo.Maps[name].IsArenaFor("deathmatch") && deathmatchStarts < 4)
                problems.Add($"{name}: {deathmatchStarts} deathmatch starts, fewer than 4 players");
            // An arena for team games only: each team's own starts, for 2 players a side
            if (!gameInfo.Maps[name].IsArenaFor("deathmatch") && (StartsOf("red") < 2 || StartsOf("blue") < 2))
                problems.Add($"{name}: {StartsOf("red")} red and {StartsOf("blue")} blue starts, fewer than 2 a side");
            if (ownStarts != 1)
                problems.Add($"{name}: {ownStarts} player starts of its own, not 1");
            if (gameInfo.Episodes.Values.Any(e => e.StartMap == name))
                problems.Add($"{name}: an episode starts on it");
        }

        // Assert
        Assert.That(problems, Is.Empty);
    }

    [TestCase("wolf3d", "wolf3d", new[] { "DM01", "DM02", "DM03", "DM04", "HE01", "PH01" })]
    [TestCase("wolf3d-shareware", "wolf3d-shareware", new[] { "HE01", "PH01" })]
    [TestCase("wolf3d-apogee", "wolf3d-apogee", new[] { "DM01", "DM02", "DM03", "DM04", "HE01", "PH01" })]
    [TestCase("spear", "spear", new string[0])]
    [TestCase("spear-demo", "spear-demo", new string[0])]
    public void Wolf3ds_Arenas_Come_With_The_Multiplayer_Mod_Only(string pack, string release, string[] withMod)
    {
        // Act
        var plain = Load(pack, release).Load<GameInfoAsset>($"{pack}/game-info");
        var modded = LoadWithMultiplayer(pack, release).Load<GameInfoAsset>($"{pack}/game-info");

        // Assert
        Assert.That(plain.Maps.Where(m => m.Value.Deathmatch), Is.Empty);
        Assert.That(modded.Maps.Where(m => m.Value.Deathmatch).Select(m => m.Key), Is.EquivalentTo(withMod));
    }

    [TestCase("wolf3d", "wolf3d")]
    [TestCase("spear", "spear")]
    public void Multiplayer_Mods_Classes_Are_For_The_Lobby_Only(string pack, string release)
    {
        // Act
        var plain = Load(pack, release).Load<GameInfoAsset>($"{pack}/game-info");
        var modded = LoadWithMultiplayer(pack, release).Load<GameInfoAsset>($"{pack}/game-info");

        // Assert
        Assert.That((plain.PlayerClasses.Count, plain.ClassMenu), Is.EqualTo((0, true)));
        Assert.That(modded.PlayerClasses.Count, Is.GreaterThan(1));
        Assert.That(modded.ClassMenu, Is.False);
    }

    [TestCase("wolf3d", "wolf3d")]
    [TestCase("spear", "spear")]
    [TestCase("wolf3d-shareware", "wolf3d-shareware")]
    [TestCase("wolf3d-apogee", "wolf3d-apogee")]
    public void Multiplayer_Pk3_Loads_As_A_Mod_And_Its_Actors_Hang_Together(string pack, string release)
    {
        // Act
        var loader = LoadWithMultiplayer(pack, release);
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
    public void A_Mod_Leaves_Extended_Actors_As_They_Were()
    {
        // Arrange: a mod that only adds an actor, so the pack's actordefs are rebuilt from YAML
        var modFolder = Path.Combine(Path.GetTempPath(), $"pfwolf-extend-mod-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(modFolder, "actordefs"));
        File.WriteAllText(Path.Combine(modFolder, "actordefs", "probe.yaml"), "ZzProbe:\n  radius: 32\n");
        var serializer = new YamlDotNet.Serialization.SerializerBuilder().DisableAliases().Build();

        try
        {
            // Act: Planet Strike's actors, whose extend: true classes build on Blake's
            var plain = Load("planetstrike", "blake-ps").Load<ActorTranslationAsset>("planetstrike/actordefs").Actors;
            var modded = new PfWolfPk3Loader([new DirectoryAssetSource(TestPaths.Pk3SourceFolder())], "planetstrike", "blake-ps",
                [new DirectoryAssetSource(modFolder)]).Load<ActorTranslationAsset>("planetstrike/actordefs").Actors;

            // Assert
            var changed = plain.Keys
                .Where(name => !modded.TryGetValue(name, out var actor) || serializer.Serialize(actor) != serializer.Serialize(plain[name]))
                .ToList();
            Assert.That(changed, Is.Empty);
            Assert.That(modded.Keys, Does.Contain("ZzProbe"));
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
