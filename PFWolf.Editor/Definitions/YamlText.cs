using System.Globalization;
using System.Text;
using PFWolf.Loaders;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.Definitions;

/// <summary>
/// Changes a YAML file's text the way a person would: only the lines of what changes are
/// rewritten, in the file's own indentation, and everything else (comments, blank lines, how
/// the rest is quoted and laid out) is left as it was. Values are found by key path through the
/// file's block mappings ({ } mappings are rewritten whole); lists are always written whole.
/// </summary>
public static class YamlText
{
    /// <summary>The text with each change made in turn</summary>
    public static string Apply(string text, IEnumerable<YamlChange> changes)
        => changes.Aggregate(text, (current, change) => Set(current, change.Path, change.Value));

    /// <summary>
    /// Makes the value at <paramref name="path"/> say <paramref name="value"/> (or takes the key
    /// out, for null) with as few changes as it can: the block mappings the file and the value
    /// both have are gone into key by key, so what's the same keeps its lines and comments
    /// </summary>
    public static string Sync(string text, IReadOnlyList<string> path, YamlNode? value)
    {
        var current = YamlNodes.Get(ParseRoot(Clean(text)), path);
        if (path.Count == 0)
        {
            if (value is not YamlMappingNode)
                throw new ArgumentException("A whole file can only be made a mapping", nameof(value));
            current ??= new YamlMappingNode();
            var changes = new List<YamlChange>();
            var root = (YamlMappingNode)current;
            foreach (var (key, child) in ((YamlMappingNode)value).Children)
            {
                if (key is YamlScalarNode { Value: { } name })
                    changes.AddRange(YamlNodes.Changes(YamlNodes.Get(root, [name]), child, [name], IsBlock));
            }
            foreach (var (key, _) in root.Children)
            {
                if (key is YamlScalarNode { Value: { } name } && YamlNodes.Get(value, [name]) == null)
                    changes.Add(new YamlChange([name], null));
            }
            return Apply(text, changes);
        }
        return Apply(text, YamlNodes.Changes(current, value, path, IsBlock));
    }

    private static bool IsBlock(YamlMappingNode mapping) => mapping.Style != MappingStyle.Flow;

    /// <summary>
    /// Sets the value at a key path, adding the keys on the way that aren't there; null takes
    /// the key out (and the mappings it leaves empty, which would read as nothing at all)
    /// </summary>
    public static string Set(string text, IReadOnlyList<string> path, YamlNode? value)
    {
        if (path.Count == 0)
            throw new ArgumentException("A key path needs at least one key", nameof(path));

        text = Clean(text);
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var step = IndentStep(text);
        var root = ParseRoot(text);
        if (root == null)
        {
            if (value == null)
                return text;
            return AppendAtEnd(text, Entry("", KeyText(path[0], null), Nest(path, 1, value), step, newline), newline);
        }
        if (root.Style == MappingStyle.Flow)
            throw new InvalidDataException("The file is one { } mapping, which the editor can't change in place");

        // The mappings walked through, and each one's key in the one before
        var walked = new List<(YamlMappingNode Mapping, YamlNode Key)>();
        var mapping = root;
        for (int i = 0; i < path.Count; i++)
        {
            var key = YamlNodes.FindKey(mapping, path[i]);
            if (key == null)
            {
                if (value == null)
                    return text;
                return InsertEntry(text, mapping, mapping == root, path[i], Nest(path, i + 1, value), step, newline);
            }

            var child = mapping.Children[key];
            walked.Add((mapping, key));
            if (i == path.Count - 1)
            {
                return value == null
                    ? RemoveEntry(text, walked)
                    : ReplaceValue(text, key, child, value, step, newline);
            }

            if (child is YamlMappingNode { Style: not MappingStyle.Flow } block)
            {
                mapping = block;
                continue;
            }

            // Not a block mapping: the rest goes into a copy of it, which takes its place
            if (value == null && child is not YamlMappingNode)
                return text;
            var replacement = child is YamlMappingNode flow ? (YamlMappingNode)YamlNodes.Clone(flow) : new YamlMappingNode();
            YamlNodes.Set(replacement, path.Skip(i + 1).ToList(), value);
            return replacement.Children.Count == 0
                ? RemoveEntry(text, walked)
                : ReplaceValue(text, key, child, replacement, step, newline);
        }
        return text;
    }

    /// <summary>The document's top-level mapping, null for an empty file; anything else can't be edited</summary>
    public static YamlMappingNode? ParseRoot(string text)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        if (stream.Documents.Count == 0)
            return null;
        if (stream.Documents[0].RootNode is YamlScalarNode { Value: null or "" or "~" })
            return null;
        return stream.Documents[0].RootNode as YamlMappingNode
               ?? throw new InvalidDataException("The file isn't a mapping of keys to values");
    }

    // Without the byte order mark some editors save, which YamlDotNet rejects
    private static string Clean(string text) => text.TrimStart('﻿');

    // The value at the end of a path built from the keys from `start` on
    private static YamlNode Nest(IReadOnlyList<string> path, int start, YamlNode value)
    {
        for (int i = path.Count - 1; i >= start; i--)
            value = new YamlMappingNode { { YamlNodes.Scalar(path[i]), value } };
        return value;
    }

    private static string InsertEntry(string text, YamlMappingNode mapping, bool isRoot, string name, YamlNode value, int step, string newline)
    {
        var first = mapping.Children.Keys.FirstOrDefault();
        var keyText = KeyText(name, first as YamlScalarNode);
        if (isRoot)
            return AppendAtEnd(text, Entry("", keyText, value, step, newline), newline);

        var indent = first != null ? Indent(first) : "";
        int at = LineEnd(text, NodeEnd(text, mapping));
        return text[..at] + newline + Entry(indent, keyText, value, step, newline) + text[at..];
    }

    private static string RemoveEntry(string text, List<(YamlMappingNode Mapping, YamlNode Key)> walked)
    {
        // A mapping left empty goes too, up to the top
        int index = walked.Count - 1;
        while (index > 0 && walked[index].Mapping.Children.Count == 1)
            index--;

        var (mapping, key) = walked[index];
        var value = mapping.Children[key];
        if (mapping.Style == MappingStyle.Flow)
            throw new InvalidDataException("The key is in a { } mapping, which the editor can't change in place");

        int start = LineStart(text, key.Start.Index);
        int end = SkipNewline(text, LineEnd(text, ValueEnd(text, key, value)));
        return text[..start] + text[end..];
    }

    private static string ReplaceValue(string text, YamlNode key, YamlNode oldValue, YamlNode newValue, int step, string newline)
    {
        // A value on its own changed into another: just its own text, so a comment after it stays
        if (oldValue is YamlScalarNode oldScalar && !IsEmpty(oldScalar) && newValue is YamlScalarNode newScalar
            && oldScalar.Style is not (ScalarStyle.Literal or ScalarStyle.Folded))
        {
            int start = (int)oldValue.Start.Index, end = (int)Math.Min(oldValue.End.Index, text.Length);
            return text[..start] + Scalar(newScalar, inFlow: false) + text[end..];
        }

        var keyIndent = Indent(key);
        var childIndent = oldValue switch
        {
            YamlMappingNode { Style: not MappingStyle.Flow, Children.Count: > 0 } mapping => Indent(mapping.Children.Keys.First()),
            YamlSequenceNode { Style: not SequenceStyle.Flow, Children.Count: > 0 } sequence => new string(' ', (int)sequence.Start.Column - 1),
            _ => keyIndent + new string(' ', step),
        };
        if (childIndent.Length <= keyIndent.Length)
            childIndent = keyIndent + new string(' ', step);

        int from = (int)key.Start.Index;
        int to = ValueEnd(text, key, oldValue);

        // Lines in place of lines: what's on the key's line after it (a comment) stays
        var newLines = Value(newValue, childIndent, step, newline);
        if (BlockStart(oldValue) is { } oldStart && newLines.StartsWith(newline) && !IsTagged(oldValue))
        {
            int linesFrom = LineStart(text, oldStart);
            if (linesFrom > LineEnd(text, key.End.Index))
                return text[..linesFrom] + newLines[newline.Length..] + text[to..];
        }

        var keyText = text[from..(int)Math.Min(key.End.Index, text.Length)];
        return text[..from] + keyText + ":" + newLines + text[to..];
    }

    // Where a block mapping or list written under its key starts: its first key, or its first "-"
    private static long? BlockStart(YamlNode value) => value switch
    {
        YamlMappingNode { Style: not MappingStyle.Flow, Children.Count: > 0 } mapping => mapping.Children.Keys.First().Start.Index,
        YamlSequenceNode { Style: not SequenceStyle.Flow, Children.Count: > 0 } sequence => sequence.Start.Index,
        _ => null,
    };

    // Where a key's value ends; for "key:" with nothing after it, the key's end
    private static int ValueEnd(string text, YamlNode key, YamlNode value)
        => IsEmpty(value) ? (int)Math.Min(key.End.Index, text.Length) : NodeEnd(text, value);

    //
    // Writing values
    //

    /// <summary>"key: value", or "key:" with the value's lines after it, at this indentation</summary>
    private static string Entry(string indent, string keyText, YamlNode value, int step, string newline)
        => indent + keyText + ":" + Value(value, indent + new string(' ', step), step, newline);

    /// <summary>What follows a key's colon: " value", or the value's lines, each on a new line</summary>
    private static string Value(YamlNode value, string childIndent, int step, string newline)
    {
        switch (value)
        {
            case YamlScalarNode scalar:
                return IsEmpty(scalar) && !IsTagged(scalar) ? "" : " " + Scalar(scalar, inFlow: false);

            case YamlMappingNode mapping when IsFlowWritten(mapping):
                return " " + Flow(mapping);

            case YamlSequenceNode sequence when IsFlowWritten(sequence):
                return " " + Flow(sequence);

            default:
                var tag = IsTagged(value) ? " " + value.Tag.Value : "";
                var lines = new StringBuilder();
                AppendBlock(lines, value, childIndent, step, newline);
                return tag + newline + lines.ToString().TrimEnd('\r', '\n');
        }
    }

    private static void AppendBlock(StringBuilder lines, YamlNode value, string indent, int step, string newline)
    {
        if (value is YamlMappingNode mapping)
        {
            var first = mapping.Children.Keys.FirstOrDefault() as YamlScalarNode;
            foreach (var (key, child) in mapping.Children)
                lines.Append(indent).Append(KeyText((key as YamlScalarNode)?.Value ?? "", key as YamlScalarNode ?? first))
                    .Append(':').Append(Value(child, indent + new string(' ', step), step, newline)).Append(newline);
            return;
        }

        foreach (var item in ((YamlSequenceNode)value).Children)
        {
            switch (item)
            {
                case YamlMappingNode { Children.Count: > 0 } itemMapping when !IsFlowWritten(itemMapping):
                {
                    // "- key: value", the item's other keys under its first
                    var itemLines = new StringBuilder();
                    AppendBlock(itemLines, itemMapping, indent + "  ", step, newline);
                    var text = itemLines.ToString();
                    lines.Append(indent).Append("- ").Append(text[(indent.Length + 2)..]);
                    break;
                }
                case YamlSequenceNode { Children.Count: > 0 } itemSequence when !IsFlowWritten(itemSequence):
                    lines.Append(indent).Append('-').Append(newline);
                    AppendBlock(lines, itemSequence, indent + new string(' ', step), step, newline);
                    break;
                case YamlScalarNode scalar:
                    lines.Append(indent).Append("- ").Append(Scalar(scalar, inFlow: false)).Append(newline);
                    break;
                default:
                    lines.Append(indent).Append("- ").Append(item is YamlMappingNode m ? Flow(m) : Flow((YamlSequenceNode)item)).Append(newline);
                    break;
            }
        }
    }

    // { } and [ ] as written, or empty; a list of short words is written [ ] too, as the files write
    // frames: [A] and flags (a list of sentences, a line each)
    private static bool IsFlowWritten(YamlMappingNode mapping) => mapping.Style == MappingStyle.Flow || mapping.Children.Count == 0;

    private static bool IsFlowWritten(YamlSequenceNode sequence)
        => sequence.Style == SequenceStyle.Flow || sequence.Children.Count == 0
           || sequence.Style == SequenceStyle.Any && sequence.Children.All(item => item is YamlScalarNode { Value: var value }
                                                                                    && (value ?? "").Length <= 24 && !(value ?? "").Any(char.IsWhiteSpace));

    private static string Flow(YamlMappingNode mapping)
    {
        var tag = IsTagged(mapping) ? mapping.Tag.Value + " " : "";
        if (mapping.Children.Count == 0)
            return tag + "{}";
        return tag + "{ " + string.Join(", ", mapping.Children.Select(pair =>
            Scalar(pair.Key as YamlScalarNode ?? YamlNodes.Scalar(""), inFlow: true) + ": " + FlowValue(pair.Value))) + " }";
    }

    private static string Flow(YamlSequenceNode sequence)
    {
        var tag = IsTagged(sequence) ? sequence.Tag.Value + " " : "";
        return tag + "[" + string.Join(", ", sequence.Children.Select(FlowValue)) + "]";
    }

    /// <summary>A value written on one line, with { } and [ ] for mappings and lists</summary>
    public static string Inline(YamlNode node) => FlowValue(node);

    private static string FlowValue(YamlNode node) => node switch
    {
        YamlScalarNode scalar => Scalar(scalar, inFlow: true),
        YamlMappingNode mapping => Flow(mapping),
        YamlSequenceNode sequence => Flow(sequence),
        _ => "''",
    };

    /// <summary>A key as it's written: plain when it can be, quoted as the mapping's other keys are</summary>
    private static string KeyText(string name, YamlScalarNode? sibling)
    {
        var style = sibling?.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted ? sibling.Style : ScalarStyle.Any;
        return Scalar(new YamlScalarNode(name) { Style = style }, inFlow: false);
    }

    /// <summary>A value as it's written: as it was quoted, or plain when it can be</summary>
    public static string Scalar(YamlScalarNode scalar, bool inFlow)
    {
        var tag = IsTagged(scalar) ? scalar.Tag.Value : "";
        var value = scalar.Value ?? "";
        if (tag == YamlTree.RemoveTag)
            return tag;
        if (tag.Length > 0)
            tag += " ";

        var style = scalar.Style;
        if (value.Contains('\n') || value.Contains('\r') || value.Any(c => char.IsControl(c) && c != '\t'))
            style = ScalarStyle.DoubleQuoted;
        else if (style is ScalarStyle.Any or ScalarStyle.Plain or ScalarStyle.Literal or ScalarStyle.Folded)
            style = NeedsQuotes(value, inFlow) ? (value.Contains('\'') && !value.Contains('"') ? ScalarStyle.DoubleQuoted : ScalarStyle.SingleQuoted) : ScalarStyle.Plain;

        return tag + style switch
        {
            ScalarStyle.SingleQuoted => "'" + value.Replace("'", "''") + "'",
            ScalarStyle.DoubleQuoted => "\"" + Escape(value) + "\"",
            _ => value,
        };
    }

    private static string Escape(string value)
    {
        var escaped = new StringBuilder();
        foreach (var c in value)
        {
            escaped.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ when char.IsControl(c) => "\\x" + ((int)c).ToString("X2", CultureInfo.InvariantCulture),
                _ => c.ToString(),
            });
        }
        return escaped.ToString();
    }

    /// <summary>Whether a value written plain would read as something else, or not at all</summary>
    private static bool NeedsQuotes(string value, bool inFlow)
    {
        if (value.Length == 0 || value != value.Trim())
            return true;
        // "-1" and "-foo" are plain values; "- foo" starts a list
        if (value[0] == '-' ? value.Length == 1 || value[1] == ' ' : "?:,[]{}#&*!|>'\"%@`".Contains(value[0]))
            return true;
        if (value.Contains(": ") || value.Contains(" #") || value.EndsWith(':'))
            return true;
        if (inFlow && value.IndexOfAny([',', '[', ']', '{', '}']) >= 0)
            return true;
        return value is "~" or "null" or "Null" or "NULL";
    }

    private static bool IsTagged(YamlNode node) => !node.Tag.IsEmpty;

    //
    // Finding things in the text (shared with GameInfoFile's way of doing it)
    //

    // "key:" with nothing after it
    private static bool IsEmpty(YamlNode node)
        => node is YamlScalarNode { Value: null or "" or "~" } scalar && scalar.Style is ScalarStyle.Plain or ScalarStyle.Any && !IsTagged(scalar);

    private static string Indent(YamlNode key) => new(' ', (int)key.Start.Column - 1);

    /// <summary>
    /// How far the file indents each level: the smallest step in from one line to the next
    /// (2 when it can't tell)
    /// </summary>
    public static int IndentStep(string text)
    {
        int best = int.MaxValue, previous = -1;
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimStart(' ');
            if (trimmed.Length == 0 || trimmed[0] is '#' or '\r' || trimmed.StartsWith("- "))
                continue;
            int indent = line.Length - trimmed.Length;
            if (previous >= 0 && indent > previous)
                best = Math.Min(best, indent - previous);
            previous = indent;
        }
        return best is int.MaxValue or > 8 ? 2 : best;
    }

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

    private static int SkipNewline(string text, int index)
    {
        if (index < text.Length && text[index] == '\r')
            index++;
        if (index < text.Length && text[index] == '\n')
            index++;
        return index;
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
        int i = text.IndexOf(bracket, Math.Min(from, text.Length));
        return i < 0 ? text.Length : i + 1;
    }

    private static string AppendAtEnd(string text, string block, string newline)
    {
        var trimmed = text.TrimEnd('\r', '\n', ' ', '\t');
        return trimmed.Length == 0 ? block + newline : trimmed + newline + block + newline;
    }
}
