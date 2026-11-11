using PFWolf.Assets;
using PFWolf.Editor.Definitions;
using PFWolf.Editor.Editing;
using PFWolf.Entities.Actors;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Tests;

/// <summary>The actor window's states read into frames and written back, as the game reads them</summary>
public class StateFramesTests
{
    private static YamlNode States(string yaml) => YamlNodes.Get(YamlText.ParseRoot(yaml), ["Thing", "states", "Attack"])!;

    [Test]
    public void Frames_Are_One_A_Letter_And_Written_Back_Grouped()
    {
        // Arrange
        var spec = StateFrames.Read(States("""
            Thing:
              states:
                Attack:
                  - { sprite: GARD, frames: [F, G], tics-per-frame: 20 }
                  - { sprite: GARD, frames: [H], tics-per-frame: 20, action: T_Shoot, next-state: Chase }
            """));

        // Assert
        Assert.That(spec.Frames.Select(frame => frame.Letter), Is.EqualTo(new[] { "F", "G", "H" }));
        Assert.That((spec.End, spec.Next), Is.EqualTo((StateEnd.Next, "Chase")));

        // F and H alike but for H's action: G joins F
        var written = YamlText.Inline(StateFrames.Write(spec));
        Assert.That(written, Does.Contain("frames: [F, G]").And.Contain("frames: [H]").And.Contain("next-state: Chase"));
    }

    [TestCase("- stop", StateEnd.Stop)]
    [TestCase("- loop", StateEnd.Loop)]
    [TestCase("- { goto: Spawn }", StateEnd.Next)]
    public void A_Marker_After_The_Last_Frame_Ends_It(string marker, StateEnd end)
    {
        var spec = StateFrames.Read(States($"""
            Thing:
              states:
                Attack:
                  - {"{"} sprite: GARD, frames: [F], tics-per-frame: 5 {"}"}
                  {marker}
            """));

        Assert.That(spec.End, Is.EqualTo(end));
    }

    // Each state's frames as the game plays them: sprite, letter, tics, think, action, modifiers,
    // particles, light, and where each goes
    private static List<string> Played(Dictionary<string, List<StateData>> states)
    {
        var resolved = ActorStateResolver.Resolve(states);
        var lines = new List<string>();
        foreach (var (name, first) in resolved.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var seen = new List<ActorStateFrame>();
            for (var frame = first; frame != null && !seen.Contains(frame); frame = frame.Next)
                seen.Add(frame);
            foreach (var frame in seen)
            {
                var next = frame.Next == null ? "-" : seen.Contains(frame.Next) ? $"#{seen.IndexOf(frame.Next)}" : $"{frame.Next.StateName}:{frame.Next.Sprite}{frame.Next.FrameLetter}";
                lines.Add($"{name} {frame.StateName} {frame.Sprite}{frame.FrameLetter} {frame.TicTime} {frame.Think} {frame.Action} "
                          + $"{string.Join(",", frame.Modifiers)} {string.Join(",", frame.Particles ?? [])} {frame.Light?.ToString()} -> {next}");
            }
        }
        return lines;
    }

    [Test]
    public void Every_State_Of_Every_Class_Plays_The_Same_Written_Back()
    {
        // Arrange: every actordefs file in pfwolf-pk3, the Multiplayer mod and the example mods
        var root = TestPaths.RepoRoot();
        var files = new[] { TestPaths.Pk3SourceFolder(), Path.Combine(root, "multiplayer-pk3") }
            .Concat(Directory.GetDirectories(Path.Combine(root, "examples", "mods")))
            .Select(folder => Path.Combine(folder, "actordefs")).Where(Directory.Exists)
            .SelectMany(folder => Directory.GetFiles(folder, "*.yaml", SearchOption.AllDirectories))
            .ToList();
        Assert.That(files, Is.Not.Empty);

        // Act
        var differences = new List<string>();
        int states = 0;
        foreach (var file in files)
        {
            var tree = YamlText.ParseRoot(File.ReadAllText(file));
            foreach (var (key, value) in tree.Children)
            {
                if (key is not YamlScalarNode { Value: { } className } || YamlNodes.Get(value, ["states"]) is not YamlMappingNode classStates)
                    continue;
                var rewritten = new YamlMappingNode();
                foreach (var (stateKey, stateValue) in classStates.Children)
                {
                    rewritten.Add(stateKey, StateFrames.Write(StateFrames.Read(stateValue)));
                    states++;
                }

                var before = Played(Parse(className, classStates));
                var after = Played(Parse(className, rewritten));
                if (!before.SequenceEqual(after))
                    differences.Add($"{Path.GetFileName(file)} {className}: {string.Join(" | ", before.Except(after).Take(2))} became {string.Join(" | ", after.Except(before).Take(2))}");
            }
        }

        // Assert
        Assert.That(states, Is.GreaterThan(800));
        Assert.That(differences, Is.Empty);
    }

    private static Dictionary<string, List<StateData>> Parse(string className, YamlMappingNode states)
    {
        var yaml = YamlTree.ToText(new YamlMappingNode { { className, new YamlMappingNode { { "states", states } } } });
        return YamlDataEntryLoader.Deserialize<Dictionary<string, ActorData>>(yaml)[className].States;
    }
}
