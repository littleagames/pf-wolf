using System.Text;
using PFWolf.Assets;
using PFWolf.Entities.Actors;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>
/// Every actor class of every game, the Multiplayer mod and each example mod, built as the game
/// builds them (ActorMetadata.CreateActor: parents merged, states resolved), written out frame by
/// frame with the warnings given on the way. Kept so a change to how classes are put together (the
/// actor editor's move of the state rules into PFWolf.Common) can be checked to change nothing:
/// set PFWOLF_ACTOR_SNAPSHOT to a file path and the snapshot is written there, to compare before
/// and after.
/// </summary>
[Category("Content")]
public class ActorStateSnapshotTests
{
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

    [Test]
    public void Every_Class_Of_Every_Game_And_Mod_Is_Built()
    {
        // Arrange
        var root = TestPaths.RepoRoot();
        List<(string Title, string Pack, string Release, string? Mod)> runs =
        [
            .. Games.Select(game => (game.Pack, game.Pack, game.Release, (string?)null)),
            ("wolf3d + multiplayer-pk3", "wolf3d", "wolf3d", Path.Combine(root, "multiplayer-pk3")),
            .. Directory.GetDirectories(Path.Combine(root, "examples", "mods")).Order(StringComparer.Ordinal)
                .Where(mod => Directory.Exists(Path.Combine(mod, "actordefs")))
                .Select(mod => (Path.GetFileName(mod), ModPack(mod), ModPack(mod), (string?)mod)),
        ];

        // Act
        var snapshot = new StringBuilder();
        int classes = 0;
        foreach (var run in runs)
        {
            snapshot.AppendLine($"=== {run.Title}");
            classes += Write(snapshot, run.Pack, run.Release, run.Mod);
        }

        if (Environment.GetEnvironmentVariable("PFWOLF_ACTOR_SNAPSHOT") is { Length: > 0 } path)
            File.WriteAllText(path, snapshot.ToString());

        // Assert: every game has its classes (a few hundred each)
        Assert.That(classes, Is.GreaterThan(1000));
        Assert.That(snapshot.ToString(), Does.Contain("Guard").And.Contain("Chase"));
    }

    // The pack an example mod is for: the first of its modinfo game-packs, else Wolf3D
    private static string ModPack(string mod)
    {
        var info = Path.Combine(mod, "modinfo.yaml");
        var line = File.Exists(info) ? File.ReadLines(info).FirstOrDefault(l => l.StartsWith("game-packs:")) : null;
        var first = line?.Split(':', 2)[1].Trim(' ', '[', ']').Split(',')[0].Trim();
        return string.IsNullOrEmpty(first) ? "wolf3d" : first;
    }

    // Writes each class of the game (with the mod, if any) and returns how many there are
    private static int Write(StringBuilder snapshot, string pack, string release, string? mod)
    {
        var source = new DirectoryAssetSource(TestPaths.Pk3SourceFolder());
        PfWolfPk3Loader loader;
        try
        {
            loader = new PfWolfPk3Loader([source], pack, release, mod != null ? [new DirectoryAssetSource(mod)] : null);
        }
        catch (Exception e)
        {
            snapshot.AppendLine($"  doesn't load: {e.GetType().Name}");
            return 0;
        }

        // The shared classes, then the pack's own (or the nearest base pack's, for one with none)
        var metadata = new ActorMetadata();
        if (TryLoad(loader, "actordefs") is { } shared)
            metadata.AddActors(shared.Actors);
        if (new[] { pack }.Concat(PfWolfPk3Loader.ReadBasePackIds(source, release))
                .Select(id => TryLoad(loader, $"{id}/actordefs")).FirstOrDefault(actors => actors != null) is { } own)
            metadata.AddActors(own.Actors);

        var warnings = new List<string>();
        var sink = WarningLog.Sink;
        WarningLog.Sink = warnings.Add;
        try
        {
            foreach (var (className, data) in metadata.Actors.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToList())
            {
                snapshot.AppendLine($"{className} : {data.Parent}");
                Entities.Actors.Actor actor;
                try
                {
                    actor = metadata.CreateActor(className, data);
                }
                catch (Exception e)
                {
                    snapshot.AppendLine($"  can't be created: {e.Message}");
                    continue;
                }

                snapshot.AppendLine($"  type {actor.GetType().Name}, radius {actor.Radius}, flags {string.Join(" ", actor.Flags.Order(StringComparer.Ordinal))}");
                foreach (var (key, value) in actor.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    snapshot.AppendLine($"  {key} = {Text(value)}");
                foreach (var (state, first) in actor.ResolvedStates.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    WriteState(snapshot, state, first);
                foreach (var warning in warnings)
                    snapshot.AppendLine($"  ! {warning}");
                warnings.Clear();
            }
        }
        finally
        {
            WarningLog.Sink = sink;
        }
        return metadata.Actors.Count;
    }

    // A state's frames from its first, each with where it goes next, until one comes round again
    private static void WriteState(StringBuilder snapshot, string state, ActorStateFrame first)
    {
        snapshot.AppendLine($"  {state}:");
        var seen = new Dictionary<ActorStateFrame, int>(ReferenceEqualityComparer.Instance);
        var frames = new List<ActorStateFrame>();
        for (var frame = first; frame != null && !seen.ContainsKey(frame); frame = frame.Next)
        {
            seen[frame] = frames.Count;
            frames.Add(frame);
        }

        foreach (var frame in frames)
        {
            var next = frame.Next == null ? "nothing"
                : seen.TryGetValue(frame.Next, out var index) ? $"#{index}"
                : $"{frame.Next.StateName} {frame.Next.Sprite}{frame.Next.FrameLetter}";
            snapshot.AppendLine($"    #{seen[frame]} [{frame.StateName}] {frame.Sprite}{frame.FrameLetter} {frame.TicTime}"
                + $" think={frame.Think} action={frame.Action} bright={frame.Bright} lighten={frame.LightenLevel}"
                + $" mods={string.Join(",", frame.Modifiers)} light={(frame.Light == null ? "" : "set")}"
                + $" particles={(frame.Particles == null ? "-" : string.Join(",", frame.Particles))} -> {next}");
        }
    }

    private static ActorTranslationAsset? TryLoad(PfWolfPk3Loader loader, string name)
    {
        try
        {
            return loader.Load<ActorTranslationAsset>(name);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static string Text(object? value) => value switch
    {
        null => "null",
        string text => text,
        IEnumerable<object> list => "[" + string.Join(", ", list.Select(Text)) + "]",
        IDictionary<object, object> map => "{" + string.Join(", ", map.Select(pair => $"{pair.Key}: {Text(pair.Value)}")) + "}",
        _ => value.ToString() ?? "",
    };
}
