using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.Editing;

/// <summary>What a state does after its last frame</summary>
public enum StateEnd
{
    /// <summary>Back to its own first frame (what a state with no ending does)</summary>
    Loop,
    /// <summary>Its last frame again, for good (stop)</summary>
    Stop,
    /// <summary>On to another state (next-state, goto)</summary>
    Next,
}

/// <summary>
/// One frame of a state as the actor window edits it: a sprite frame for a number of tics, with
/// what it runs. Light and particles are kept as written (YAML).
/// </summary>
public sealed record FrameSpec(string Sprite, string Letter, string Tics, string? Think = null, string? Action = null,
    IReadOnlyList<string>? Modifiers = null, YamlNode? Light = null, YamlNode? Particles = null)
{
    public IReadOnlyList<string> ModifierList => Modifiers ?? [];

    /// <summary>Everything but the letter the same: written as one entry with both letters</summary>
    public bool GroupsWith(FrameSpec other)
        => Sprite == other.Sprite && Tics == other.Tics && Think == other.Think && Action == other.Action
           && ModifierList.SequenceEqual(other.ModifierList) && SameNode(Light, other.Light) && SameNode(Particles, other.Particles);

    private static bool SameNode(YamlNode? a, YamlNode? b)
        => a == null ? b == null : b != null && Definitions.YamlNodes.Equal(a, b);
}

/// <summary>A state as the actor window edits it: its frames, one a letter, and how it ends</summary>
public sealed class StateSpec
{
    public List<FrameSpec> Frames { get; init; } = [];
    public StateEnd End { get; set; }

    /// <summary>The state it goes on to, when it ends so</summary>
    public string? Next { get; set; }

    public StateSpec Clone() => new() { Frames = [.. Frames], End = End, Next = Next };
}

/// <summary>
/// An actordefs state read into single frames and written back, as the game reads it
/// (StateDataNodeDeserializer, ActorStateResolver): entries of frames, each letter one frame; how
/// it ends is the last entry's next-state, else the first marker after it (stop, loop, goto), else
/// a loop. Frames next to each other alike but for the letter are written as one entry, as the
/// files write them. A next-state or marker that's never reached (on an entry before the last)
/// does nothing in the game, and is left out.
/// </summary>
public static class StateFrames
{
    public static StateSpec Read(YamlNode? node)
    {
        var spec = new StateSpec();
        if (node is not YamlSequenceNode entries)
            return spec;

        // How it ends: settled by the last frame entry's next-state, or the first marker after it
        (StateEnd End, string? Next)? ending = null;
        foreach (var entry in entries.Children)
        {
            if (entry is YamlScalarNode { Value: var marker })
            {
                if (spec.Frames.Count > 0)
                    ending ??= marker?.Trim().ToLowerInvariant() == "stop" ? (StateEnd.Stop, null) : (StateEnd.Loop, null);
                continue;
            }
            if (entry is not YamlMappingNode mapping)
                continue;
            var fields = mapping.Children
                .Where(pair => pair.Key is YamlScalarNode)
                .ToDictionary(pair => Normalize(((YamlScalarNode)pair.Key).Value ?? ""), pair => pair.Value);

            if (fields.Count == 1 && fields.TryGetValue("goto", out var target))
            {
                if (spec.Frames.Count > 0)
                    ending ??= (StateEnd.Next, Text(target));
                continue;
            }

            // A frame entry: what came after an earlier one doesn't count, a later one goes on from it
            ending = null;
            var letters = fields.TryGetValue("frames", out var frames) || fields.TryGetValue("frame", out frames) ? Strings(frames) : [];
            var frame = new FrameSpec(
                Text(fields.GetValueOrDefault("sprite")) ?? "",
                "",
                Text(fields.GetValueOrDefault("ticsperframe")) ?? "0",
                NullIfEmpty(Text(fields.GetValueOrDefault("think"))),
                NullIfEmpty(Text(fields.GetValueOrDefault("action"))),
                fields.TryGetValue("modifiers", out var modifiers) ? Strings(modifiers) : null,
                fields.TryGetValue("light", out var light) ? Definitions.YamlNodes.Clone(light) : null,
                fields.TryGetValue("particles", out var particles) ? Definitions.YamlNodes.Clone(particles) : null);
            foreach (var letter in letters)
                spec.Frames.Add(frame with { Letter = letter });
            if (letters.Count > 0 && NullIfEmpty(Text(fields.GetValueOrDefault("nextstate"))) is { } next)
                ending = (StateEnd.Next, next);
        }

        (spec.End, spec.Next) = ending ?? (StateEnd.Loop, null);
        return spec;
    }

    public static YamlSequenceNode Write(StateSpec spec)
    {
        var list = new YamlSequenceNode();
        if (spec.Frames.Count == 0)
        {
            if (spec.End == StateEnd.Next && !string.IsNullOrEmpty(spec.Next))
                list.Add(new YamlMappingNode { { "goto", spec.Next } });
            else if (spec.End == StateEnd.Stop)
                list.Add(new YamlScalarNode("stop"));
            return list;
        }

        var groups = new List<List<FrameSpec>>();
        foreach (var frame in spec.Frames)
        {
            if (groups.Count > 0 && groups[^1][0].GroupsWith(frame))
                groups[^1].Add(frame);
            else
                groups.Add([frame]);
        }

        for (int i = 0; i < groups.Count; i++)
        {
            var first = groups[i][0];
            var entry = new YamlMappingNode
            {
                { "sprite", first.Sprite },
                { "frames", new YamlSequenceNode(groups[i].Select(frame => new YamlScalarNode(frame.Letter))) { Style = SequenceStyle.Flow } },
                { "tics-per-frame", first.Tics },
            };
            if (first.Think != null)
                entry.Add("think", first.Think);
            if (first.Action != null)
                entry.Add("action", first.Action);
            if (first.Modifiers is { Count: > 0 } modifiers)
                entry.Add("modifiers", new YamlSequenceNode(modifiers.Select(modifier => new YamlScalarNode(modifier))) { Style = SequenceStyle.Flow });
            if (first.Light != null)
                entry.Add("light", Definitions.YamlNodes.Clone(first.Light));
            if (first.Particles != null)
                entry.Add("particles", Definitions.YamlNodes.Clone(first.Particles));
            if (i == groups.Count - 1 && spec.End == StateEnd.Next && !string.IsNullOrEmpty(spec.Next))
                entry.Add("next-state", spec.Next);
            list.Add(entry);
        }
        if (spec.End == StateEnd.Stop)
            list.Add(new YamlScalarNode("stop"));
        return list;
    }

    private static string Normalize(string key) => key.Replace("-", "").ToLowerInvariant();

    private static string? Text(YamlNode? node) => (node as YamlScalarNode)?.Value;

    private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;

    private static List<string> Strings(YamlNode? node) => node switch
    {
        YamlSequenceNode sequence => [.. sequence.Children.Select(item => (item as YamlScalarNode)?.Value ?? "")],
        YamlScalarNode scalar => [scalar.Value ?? ""],
        _ => [],
    };
}
