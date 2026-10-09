using System.Globalization;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.Editing;

/// <summary>
/// A mod's game-info.yaml, which merges over the game pack's: the editor writes a level's
/// properties under maps: NAME:. Only that level's block is rewritten, and in it only the keys
/// the editor changed; everything else in the file, comments included, is left as it was.
/// </summary>
public static class GameInfoFile
{
    public const string FileName = "game-info.yaml";

    /// <summary>The game-info keys the editor writes, in the order it writes them</summary>
    public static readonly IReadOnlyList<string> Keys =
    [
        "name", "music", "next", "secret-next", "par-time", "floor-color", "ceiling-color", "wall-height",
        "sky", "default-floor", "default-ceiling", "shading", "zones",
    ];

    /// <summary>
    /// The mod's game-info.yaml (in a folder or a pk3) with <paramref name="keys"/> of the
    /// level's properties written into its block, ready to write back
    /// </summary>
    public static (string EntryPath, byte[] Data) Updated(string modPath, string mapName, MapProperties properties, IReadOnlyCollection<string> keys)
    {
        var updated = Apply(ModFiles.ReadText(modPath, FileName), mapName, properties, keys);
        return (FileName, new UTF8Encoding(false).GetBytes(updated));
    }

    /// <summary>
    /// The file's text with the level's block changed: each key in <paramref name="keys"/> set to
    /// the properties' value, or taken out where that's null
    /// </summary>
    public static string Apply(string? text, string mapName, MapProperties properties, IReadOnlyCollection<string> keys)
    {
        text ??= "";
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";

        YamlMappingNode? root = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(text));
            if (stream.Documents.Count > 0)
            {
                root = stream.Documents[0].RootNode as YamlMappingNode
                       ?? throw new InvalidDataException($"{FileName} isn't a mapping of keys to values");
            }
        }

        var mapsEntry = root?.Children.FirstOrDefault(pair => pair.Key is YamlScalarNode { Value: "maps" });
        if (root == null || mapsEntry?.Key == null)
        {
            // No maps: yet: add it at the end
            var block = new StringBuilder();
            block.Append("maps:").Append(newline);
            if (!AppendBlock(block, "  ", mapName, newline, [], properties, keys))
                return text;
            return AppendAtEnd(text, block.ToString(), newline);
        }

        var (mapsKey, mapsValue) = (mapsEntry.Value.Key, mapsEntry.Value.Value);
        if (IsEmpty(mapsValue))
        {
            // "maps:" with nothing under it
            var block = new StringBuilder();
            if (!AppendBlock(block, Indent(mapsKey) + "  ", mapName, newline, [], properties, keys))
                return text;
            return InsertAfter(text, LineEnd(text, mapsKey.End.Index), newline + block.ToString().TrimEnd('\r', '\n'));
        }
        if (mapsValue is not YamlMappingNode maps || maps.Style == MappingStyle.Flow)
            throw new InvalidDataException($"{FileName}'s maps isn't a block of levels, so the editor can't add to it");

        var levelIndent = maps.Children.Count > 0 ? Indent(maps.Children.First().Key) : Indent(mapsKey) + "  ";
        var entry = maps.Children.FirstOrDefault(pair => pair.Key is YamlScalarNode scalar
                                                         && string.Equals(scalar.Value, mapName, StringComparison.OrdinalIgnoreCase));
        if (entry.Key == null)
        {
            // A level the file doesn't have yet: after the last one
            var block = new StringBuilder();
            if (!AppendBlock(block, levelIndent, mapName, newline, [], properties, keys))
                return text;
            return InsertAfter(text, LineEnd(text, NodeEnd(text, maps)), newline + block.ToString().TrimEnd('\r', '\n'));
        }

        // The level's block: keep what the editor doesn't write, as it's written
        var (levelKey, levelValue) = (entry.Key, entry.Value);
        var kept = new List<string>();
        if (levelValue is YamlMappingNode level)
        {
            int chunkStart = LineEnd(text, levelKey.End.Index);
            foreach (var (key, value) in level.Children)
            {
                int end = level.Style == MappingStyle.Flow ? NodeEnd(text, value) : LineEnd(text, NodeEnd(text, value));
                if (key is YamlScalarNode { Value: { } name } && Keys.Contains(name) && keys.Contains(name))
                {
                    chunkStart = end;
                    continue;
                }

                kept.Add(level.Style == MappingStyle.Flow
                    ? $"{levelIndent}  {text[(int)key.Start.Index..end].Trim()}"
                    : text[chunkStart..end].TrimStart('\r', '\n'));
                chunkStart = end;
            }
        }

        var rewritten = new StringBuilder();
        var hasValues = AppendBlock(rewritten, levelIndent, ((YamlScalarNode)levelKey).Value!, newline, kept, properties, keys,
            childIndent: levelValue is YamlMappingNode { Style: not MappingStyle.Flow, Children.Count: > 0 } existing
                ? Indent(existing.Children.First().Key)
                : levelIndent + "  ");

        int blockStart = LineStart(text, levelKey.Start.Index);
        int blockEnd = IsEmpty(levelValue) ? LineEnd(text, levelKey.End.Index) : LineEnd(text, NodeEnd(text, levelValue));

        // An empty "MAP01:" would merge over the game's entry as nothing at all: leave the level out instead
        if (!hasValues)
            return text[..blockStart] + text[SkipNewline(text, blockEnd)..];
        return text[..blockStart] + rewritten.ToString().TrimEnd('\r', '\n') + text[blockEnd..];
    }

    /// <summary>A level's block: its key, what's kept, then the keys written. False when that's nothing but the key.</summary>
    private static bool AppendBlock(StringBuilder block, string indent, string mapName, string newline, List<string> kept,
        MapProperties properties, IReadOnlyCollection<string> keys, string? childIndent = null)
    {
        childIndent ??= indent + "  ";
        block.Append(indent).Append(mapName).Append(':').Append(newline);
        int headerEnd = block.Length;
        foreach (var chunk in kept)
            block.Append(chunk.TrimEnd('\r', '\n')).Append(newline);

        foreach (var key in Keys.Where(keys.Contains))
        {
            switch (key)
            {
                case "shading":
                    if (!properties.HasShading)
                        break;
                    block.Append(childIndent).Append("shading:").Append(newline);
                    var shading = childIndent + "  ";
                    AppendValue(block, shading, "fade-color", Quote(properties.FadeColor), newline);
                    AppendValue(block, shading, "fade-start", Number(properties.FadeStart), newline);
                    AppendValue(block, shading, "fade-end", Number(properties.FadeEnd), newline);
                    AppendValue(block, shading, "max-fade", Number(properties.MaxFade), newline);
                    AppendValue(block, shading, "light", Number(properties.Light), newline);
                    break;

                case "zones":
                    if (properties.Zones.Count == 0)
                        break;
                    block.Append(childIndent).Append("zones:").Append(newline);
                    foreach (var zone in properties.Zones.OrderBy(zone => zone.Id))
                    {
                        var fields = new List<string>();
                        void Field(string name, string? value)
                        {
                            if (value != null)
                                fields.Add($"{name}: {value}");
                        }
                        Field("light", Number(zone.Light));
                        Field("color", Quote(zone.Color));
                        Field("effect", Quote(zone.Effect));
                        Field("low", Number(zone.Low));
                        Field("tics", Number(zone.Tics));
                        Field("bright-tics", Number(zone.BrightTics));
                        block.Append(childIndent).Append("  ").Append(zone.Id.ToString(CultureInfo.InvariantCulture))
                            .Append(": { ").Append(string.Join(", ", fields)).Append(fields.Count > 0 ? " }" : "}").Append(newline);
                    }
                    break;

                default:
                    AppendValue(block, childIndent, key, ValueOf(properties, key), newline);
                    break;
            }
        }

        return block.Length > headerEnd;
    }

    private static int SkipNewline(string text, int index)
    {
        if (index < text.Length && text[index] == '\r')
            index++;
        if (index < text.Length && text[index] == '\n')
            index++;
        return index;
    }

    private static string? ValueOf(MapProperties properties, string key) => key switch
    {
        "name" => Quote(properties.Name),
        "music" => Quote(properties.Music),
        "next" => Quote(properties.Next),
        "secret-next" => Quote(properties.SecretNext),
        "par-time" => Number(properties.ParTime),
        "floor-color" => Quote(properties.FloorColor),
        "ceiling-color" => Quote(properties.CeilingColor),
        "wall-height" => Number(properties.WallHeight),
        "sky" => Quote(properties.Sky),
        "default-floor" => Quote(properties.DefaultFloor),
        "default-ceiling" => Quote(properties.DefaultCeiling),
        _ => null,
    };

    private static void AppendValue(StringBuilder block, string indent, string key, string? value, string newline)
    {
        if (value != null)
            block.Append(indent).Append(key).Append(": ").Append(value).Append(newline);
    }

    private static string? Quote(string? value)
        => string.IsNullOrEmpty(value) ? null : "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string? Number(double? value) => value?.ToString(CultureInfo.InvariantCulture);

    private static string Indent(YamlNode key) => new(' ', (int)key.Start.Column - 1);

    // "key:" with nothing after it
    private static bool IsEmpty(YamlNode node) => node is YamlScalarNode { Value: null or "" or "~" } scalar && scalar.Style == ScalarStyle.Plain;

    private static int LineStart(string text, long index)
    {
        int i = (int)Math.Min(index, text.Length);
        while (i > 0 && text[i - 1] != '\n')
            i--;
        return i;
    }

    private static int LineEnd(string text, long index)
    {
        int i = (int)Math.Min(index, text.Length);
        while (i < text.Length && text[i] != '\n' && text[i] != '\r')
            i++;
        return i;
    }

    /// <summary>
    /// Where a node's own text ends. YamlDotNet gives a block mapping or sequence the same end as
    /// its start, so it's where its last child ends (and, written as { } or [ ], its bracket)
    /// </summary>
    private static int NodeEnd(string text, YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
            {
                int end = (int)mapping.Start.Index;
                foreach (var (key, value) in mapping.Children)
                    end = Math.Max(end, Math.Max(NodeEnd(text, key), NodeEnd(text, value)));
                return mapping.Style == MappingStyle.Flow ? PastBracket(text, end, '}') : end;
            }
            case YamlSequenceNode sequence:
            {
                int end = (int)sequence.Start.Index;
                foreach (var item in sequence.Children)
                    end = Math.Max(end, NodeEnd(text, item));
                return sequence.Style == SequenceStyle.Flow ? PastBracket(text, end, ']') : end;
            }
            default:
                return (int)Math.Min(node.End.Index, text.Length);
        }
    }

    private static int PastBracket(string text, int from, char bracket)
    {
        int i = text.IndexOf(bracket, from);
        return i < 0 ? text.Length : i + 1;
    }

    private static string InsertAfter(string text, int index, string insert) => text[..index] + insert + text[index..];

    private static string AppendAtEnd(string text, string block, string newline)
    {
        var trimmed = text.TrimEnd('\r', '\n', ' ', '\t');
        return trimmed.Length == 0 ? block : trimmed + newline + block;
    }
}
