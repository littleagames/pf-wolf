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
        new([new DirectoryAssetSource(ActorDefs.Pk3SourceFolder())], pack, release);

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
        // Arrange: as AssetManager.GetActorMetadata builds it
        var loader = Load(pack, release);
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

        // Act
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

        // Assert
        Assert.That(problems, Is.Empty);
    }
}
