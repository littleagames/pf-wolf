using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using Wolf3D.Assets;

namespace Wolf3D.Loaders;

/// <summary>
/// Resolves the abstract <see cref="StateData"/> type when deserializing an actor's
/// `states:` lists. Real actordefs content is always a frame entry (sprite/frames/etc.,
/// with an optional inline `next-state:` field), but this also recognizes the shorthand
/// `stop` / `loop` scalar markers and a `{ goto: <state> }` mapping, matching the
/// StopStateData/LoopStateData/GoToStateData types authors can opt into later.
/// </summary>
internal class StateDataNodeDeserializer : INodeDeserializer
{
    public bool Deserialize(IParser reader, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer, out object? value, ObjectDeserializer rootDeserializer)
    {
        if (expectedType != typeof(StateData))
        {
            value = null;
            return false;
        }

        // Shorthand markers: a bare "stop" / "loop" scalar in the list.
        if (reader.Accept<Scalar>(out var scalar))
        {
            var marker = scalar.Value.Trim();
            reader.MoveNext();
            value = marker.ToLowerInvariant() switch
            {
                "stop" => new StopStateData(),
                "loop" => new LoopStateData(),
                _ => throw new YamlException(scalar.Start, scalar.End,
                    $"Unrecognized actor state marker '{marker}'. Expected 'stop', 'loop', a 'goto: <state>' mapping, or a frame entry.")
            };
            return true;
        }

        // Everything else is a mapping: either { goto: <state> } or a real frame entry.
        var raw = (Dictionary<object, object>)nestedObjectDeserializer(reader, typeof(Dictionary<object, object>))!;
        var normalized = raw.ToDictionary(kv => Normalize(kv.Key.ToString() ?? ""), kv => kv.Value);

        if (normalized.Count == 1 && normalized.TryGetValue("goto", out var target))
        {
            value = new GoToStateData(target?.ToString() ?? "");
            return true;
        }

        value = BuildActorStatesData(normalized);
        return true;
    }

    private static ActorStatesData BuildActorStatesData(Dictionary<string, object> fields)
    {
        var data = new ActorStatesData();

        if (fields.TryGetValue("sprite", out var sprite))
            data.Sprite = sprite?.ToString() ?? "";

        // "frames" is correct; "frame" shows up as a typo in some existing content and is
        // accepted the same way rather than silently dropping the frame list.
        if (fields.TryGetValue("frames", out var frames) || fields.TryGetValue("frame", out frames))
            data.Frames = ToStringList(frames);

        if (fields.TryGetValue("ticsperframe", out var tics))
            data.TicsPerFrame = Convert.ToSingle(tics);

        if (fields.TryGetValue("modifiers", out var modifiers))
            data.Modifiers = ToStringList(modifiers);

        if (fields.TryGetValue("action", out var action))
            data.Action = action?.ToString() ?? "";

        if (fields.TryGetValue("think", out var think))
            data.Think = think?.ToString() ?? "";

        if (fields.TryGetValue("nextstate", out var nextState))
            data.NextState = nextState?.ToString();

        return data;
    }

    private static List<string> ToStringList(object value)
    {
        if (value is List<object> list)
            return list.Select(v => v?.ToString() ?? "").ToList();

        return [value?.ToString() ?? ""];
    }

    private static string Normalize(string key) => key.Replace("-", "").ToLowerInvariant();
}
