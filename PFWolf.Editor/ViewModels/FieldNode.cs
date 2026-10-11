using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Editor.Definitions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.ViewModels;

/// <summary>What a field editor's fields share: the names to suggest, and who to tell of a change</summary>
public sealed class FieldContext
{
    public Func<FieldHint, IReadOnlyList<string>> Suggestions { get; init; } = _ => [];

    /// <summary>A field changed</summary>
    public Action? Changed { get; init; }

    /// <summary>Asks for a new key (title, prompt, check); null when cancelled</summary>
    public Func<string, string, Func<string, string?>, Task<string?>>? AskKey { get; init; }
}

/// <summary>How a field is edited</summary>
public enum FieldKind
{
    /// <summary>A line of text (a name, a number, a list of them)</summary>
    Text,
    /// <summary>One of a few names</summary>
    Choice,
    /// <summary>Fields or entries of its own, shown under it</summary>
    Group,
    /// <summary>A key the editor doesn't know, edited as YAML on one line</summary>
    Raw,
    /// <summary>Stands in for a group's fields until it's opened</summary>
    Placeholder,
}

/// <summary>
/// A field of a definition in the editor: a value to type or pick, or a group of fields (a
/// record's, a dictionary's entries, a list's items) shown under it. It's made from the YAML and
/// the field's schema, and builds the YAML back as edited: what's left empty is left out (so the
/// game uses its default, or the value before it), and keys it doesn't know are kept as they were.
/// </summary>
public sealed partial class FieldNode : ObservableObject
{
    private readonly FieldContext _context;
    private readonly YamlNode? _original;
    private readonly YamlNode? _baseValue;
    private readonly YamlScalarNode? _keyNode;
    private bool _childrenBuilt;
    private bool _loading = true;

    public FieldNode(FieldSchema? schema, string key, YamlNode? value, YamlNode? baseValue, FieldNode? parent, FieldContext context,
        bool isEntry = false, YamlScalarNode? keyNode = null)
    {
        Schema = schema;
        Key = key;
        Parent = parent;
        _context = context;
        _original = value;
        _baseValue = baseValue;
        _keyNode = keyNode;
        IsEntry = isEntry;
        IsModified = baseValue != null || value != null ? !YamlNodes.Equal(value, baseValue) : false;

        Kind = schema switch
        {
            null => FieldKind.Raw,
            { IsInlineList: true } => value is null or YamlSequenceNode ? FieldKind.Text : FieldKind.Raw,
            { IsScalar: true } => value is null or YamlScalarNode
                ? schema.Type is FieldType.Choice or FieldType.Boolean ? FieldKind.Choice : FieldKind.Text
                : FieldKind.Raw,
            _ => value is null || value is YamlMappingNode && schema.Type is FieldType.Record or FieldType.Dictionary
                               || value is YamlSequenceNode && schema.Type == FieldType.List
                ? FieldKind.Group
                : FieldKind.Raw,
        };

        _text = Kind switch
        {
            FieldKind.Text when schema!.IsInlineList && value is YamlSequenceNode list
                => string.Join(", ", list.Children.Select(item => item is YamlScalarNode scalar ? scalar.Value ?? "" : YamlText.Inline(item))),
            FieldKind.Text or FieldKind.Choice => (value as YamlScalarNode)?.Value ?? "",
            FieldKind.Raw => value == null ? "" : YamlText.Inline(value),
            _ => "",
        };

        if (Kind == FieldKind.Group)
        {
            if (value != null)
                EnsureChildren();
            else
                Children.Add(new FieldNode(this, context));
        }
        _loading = false;
    }

    // The stand-in child of a group not opened yet
    private FieldNode(FieldNode parent, FieldContext context)
    {
        Parent = parent;
        _context = context;
        Key = "";
        Kind = FieldKind.Placeholder;
        _text = "";
        _loading = false;
    }

    public FieldSchema? Schema { get; }

    /// <summary>Its key as written (a dictionary entry's key, a list item's number)</summary>
    public string Key { get; private set; }

    public FieldNode? Parent { get; }
    public FieldKind Kind { get; }

    /// <summary>A dictionary's entry or a list's item: written even when it has nothing set</summary>
    public bool IsEntry { get; }

    /// <summary>It differs from pfwolf.pk3's own (a mod, or an unsaved edit, changes it)</summary>
    public bool IsModified { get; }

    public ObservableCollection<FieldNode> Children { get; } = [];

    /// <summary>The field's text: its value, or a list of values with commas between</summary>
    [ObservableProperty] private string _text;

    [ObservableProperty] private bool _isExpanded;

    public bool IsText => Kind == FieldKind.Text && !HasSuggestions || Kind == FieldKind.Raw;
    public bool IsSuggested => Kind == FieldKind.Text && HasSuggestions;
    public bool IsChoice => Kind == FieldKind.Choice;
    public bool IsGroup => Kind == FieldKind.Group;
    public bool IsPlaceholder => Kind == FieldKind.Placeholder;

    public string Label => Kind == FieldKind.Placeholder ? "…"
        : Parent?.Schema?.Type == FieldType.List && IsEntry ? $"#{Parent.Children.IndexOf(this) + 1}"
        : IsEntry ? Key
        : Schema?.Label ?? Key;

    public string ToolTip
    {
        get
        {
            var lines = new List<string>();
            if (Schema?.Description is { } description)
                lines.Add(description);
            if (Kind == FieldKind.Raw)
                lines.Add(Schema == null
                    ? "Not a field the editor knows, so it's edited as YAML on one line ({ } for a mapping, [ ] for a list)"
                    : "Not written the way the editor expects, so it's edited as YAML on one line");
            if (!IsEntry && Key.Length > 0)
                lines.Add($"Key: {Key}");
            if (Schema?.Default is { } defaultText && Kind != FieldKind.Group)
                lines.Add($"Left empty: {defaultText}");
            if (IsModified)
                lines.Add("Changed from pfwolf.pk3's own");
            return string.Join("\n", lines);
        }
    }

    /// <summary>What's shown in an empty field: the game's default</summary>
    public string Placeholder => Kind == FieldKind.Raw ? "YAML" : Schema?.Default ?? "";

    public IReadOnlyList<string> Choices => Schema == null ? [] : ["", .. Schema.Choices];

    public IReadOnlyList<string> Suggestions => Schema == null ? []
        : _context.Suggestions(Schema.Type == FieldType.List ? Schema.Item?.Hint ?? FieldHint.None : Schema.Hint);

    private bool HasSuggestions => Schema != null && !Schema.IsInlineList && Schema.Hint is not (FieldHint.None or FieldHint.Color)
                                   && Suggestions.Count > 0;

    /// <summary>A group's one-line summary ("4 entries", "not set")</summary>
    public string Summary
    {
        get
        {
            if (Kind != FieldKind.Group)
                return "";
            var built = _childrenBuilt ? Children.Count : (_original as YamlMappingNode)?.Children.Count ?? (_original as YamlSequenceNode)?.Children.Count ?? 0;
            return Schema!.Type switch
            {
                FieldType.Dictionary => built == 1 ? "1 entry" : $"{built} entries",
                FieldType.List => built == 1 ? "1 item" : $"{built} items",
                _ => _original == null && !HasValues() ? "not set" : ValuesSummary(),
            };
        }
    }

    // A record's values set, on one line ("GARD · A · -1 · T_Stand"), so a list of them can be told apart closed
    private string ValuesSummary()
    {
        IEnumerable<string> values = _childrenBuilt
            ? Children.Where(child => child.Kind is FieldKind.Text or FieldKind.Choice or FieldKind.Raw && child.Text.Length > 0).Select(child => child.Text)
            : (_original as YamlMappingNode)?.Children.Values.OfType<YamlScalarNode>().Select(value => value.Value ?? "").Where(value => value.Length > 0) ?? [];
        var text = string.Join(" · ", values.Take(6));
        return text.Length > 90 ? text[..90] + "…" : text;
    }

    public bool CanAdd => Kind == FieldKind.Group && Schema!.Type is FieldType.Dictionary or FieldType.List;
    public bool CanRemove => IsEntry && Parent != null;
    public bool CanMove => IsEntry && Parent?.Schema?.Type == FieldType.List;
    public bool CanReset => !IsEntry && Kind != FieldKind.Placeholder && (Kind == FieldKind.Group ? HasValues() : Text.Length > 0);

    public bool HasModified => IsModified;

    partial void OnTextChanged(string value)
    {
        if (_loading)
            return;
        OnPropertyChanged(nameof(CanReset));
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        for (var node = Parent; node != null; node = node.Parent)
        {
            node.OnPropertyChanged(nameof(Summary));
            node.OnPropertyChanged(nameof(CanReset));
        }
        _context.Changed?.Invoke();
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value)
            EnsureChildren();
    }

    /// <summary>Leaves out the group's fields that aren't set and don't apply here (a door's fields on a wall)</summary>
    public void HideUnset(Func<FieldSchema, bool> doesNotApply)
    {
        EnsureChildren();
        foreach (var child in Children.ToList())
        {
            if (child.Schema != null && doesNotApply(child.Schema) && child.Build([]) == null)
                Children.Remove(child);
        }
    }

    /// <summary>Makes the group's fields (opening one that wasn't set)</summary>
    public void EnsureChildren()
    {
        if (Kind != FieldKind.Group || _childrenBuilt)
            return;
        _childrenBuilt = true;
        Children.Clear();
        var schema = Schema!;

        switch (schema.Type)
        {
            case FieldType.Record:
            {
                var mapping = _original as YamlMappingNode;
                var used = new HashSet<YamlNode>();
                foreach (var field in schema.Fields)
                {
                    var keyNode = mapping == null ? null : YamlNodes.FindKey(mapping, field.Key) as YamlScalarNode;
                    if (keyNode != null)
                        used.Add(keyNode);
                    var value = keyNode != null ? mapping!.Children[keyNode] : null;
                    Children.Add(new FieldNode(field, keyNode?.Value ?? field.Key, value, YamlNodes.Get(_baseValue, [keyNode?.Value ?? field.Key]), this, _context,
                        keyNode: keyNode));
                }
                if (mapping != null)
                {
                    foreach (var (key, value) in mapping.Children)
                    {
                        if (!used.Contains(key) && key is YamlScalarNode { Value: { } name } scalarKey)
                            Children.Add(new FieldNode(null, name, value, YamlNodes.Get(_baseValue, [name]), this, _context, keyNode: scalarKey));
                    }
                }
                break;
            }
            case FieldType.Dictionary when _original is YamlMappingNode entries:
                foreach (var (key, value) in entries.Children)
                {
                    if (key is YamlScalarNode { Value: { } name } scalarKey)
                        Children.Add(new FieldNode(schema.Item, name, value, YamlNodes.Get(_baseValue, [name]), this, _context, isEntry: true, keyNode: scalarKey));
                }
                break;
            case FieldType.List when _original is YamlSequenceNode items:
            {
                var baseItems = (_baseValue as YamlSequenceNode)?.Children;
                for (int i = 0; i < items.Children.Count; i++)
                {
                    var baseItem = baseItems != null && i < baseItems.Count ? baseItems[i] : null;
                    Children.Add(new FieldNode(schema.Item, i.ToString(CultureInfo.InvariantCulture), items.Children[i], baseItem, this, _context, isEntry: true));
                }
                break;
            }
        }
        OnPropertyChanged(nameof(Summary));
    }

    // Whether anything under the group is set
    private bool HasValues()
    {
        if (!_childrenBuilt)
            return _original != null;
        return Schema?.Type is FieldType.Dictionary or FieldType.List
            ? Children.Count > 0
            : Children.Any(child => child.Kind == FieldKind.Group ? child.HasValues() : child.Text.Length > 0);
    }

    /// <summary>
    /// The field as YAML (null when it's left out), noting in <paramref name="errors"/> a value
    /// that isn't what the field takes (a word where a number goes)
    /// </summary>
    public YamlNode? Build(List<string> errors)
    {
        switch (Kind)
        {
            case FieldKind.Placeholder:
                return null;

            case FieldKind.Raw:
            {
                var text = Text.Trim();
                if (text.Length == 0)
                    return IsEntry ? new YamlScalarNode("") : null;
                if (_original != null && YamlText.Inline(_original) == text)
                    return YamlNodes.Clone(_original);
                try
                {
                    return YamlNodes.Get(YamlText.ParseRoot("value: " + text), ["value"]) ?? new YamlScalarNode("");
                }
                catch (Exception e) when (e is YamlException or InvalidDataException)
                {
                    errors.Add($"{Path}: {e.Message}");
                    return _original == null ? null : YamlNodes.Clone(_original);
                }
            }

            case FieldKind.Text when Schema!.IsInlineList:
            {
                var items = Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (items.Length == 0)
                    return IsEntry ? new YamlSequenceNode { Style = SequenceStyle.Flow } : null;
                var sequence = new YamlSequenceNode { Style = (_original as YamlSequenceNode)?.Style ?? SequenceStyle.Flow };
                foreach (var item in items)
                    sequence.Add(Scalar(item, Schema.Item!, errors));
                return _original != null && YamlNodes.Equal(_original, sequence) ? YamlNodes.Clone(_original) : sequence;
            }

            case FieldKind.Text or FieldKind.Choice:
            {
                var text = Text.Trim();
                if (text.Length == 0)
                    return IsEntry ? new YamlScalarNode("") : null;
                if (_original is YamlScalarNode original && original.Value == text)
                    return YamlNodes.Clone(original);
                var scalar = Scalar(text, Schema!, errors);
                // Quoted as it was ("#383838" stays in double quotes), so the file's line changes only in its value
                if (_original is YamlScalarNode { Style: ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted } quoted)
                    scalar.Style = quoted.Style;
                return scalar;
            }

            default:
                if (!_childrenBuilt)
                    return _original == null ? null : YamlNodes.Clone(_original);

                if (Schema!.Type == FieldType.List)
                {
                    var list = new YamlSequenceNode { Style = (_original as YamlSequenceNode)?.Style ?? SequenceStyle.Any };
                    foreach (var child in Children)
                        list.Add(child.Build(errors) ?? new YamlScalarNode(""));
                    return list.Children.Count > 0 || IsEntry || _original != null ? list : null;
                }

                var mapping = new YamlMappingNode { Style = (_original as YamlMappingNode)?.Style ?? MappingStyle.Any };
                foreach (var child in Children)
                {
                    if (child.Kind != FieldKind.Placeholder && child.Build(errors) is { } value)
                        mapping.Add(child._keyNode != null ? YamlNodes.Clone(child._keyNode) : new YamlScalarNode(child.Key), value);
                }
                if (mapping.Children.Count == 0 && !IsEntry)
                    return Schema.Type == FieldType.Dictionary && _original != null ? mapping : null;
                return mapping;
        }
    }

    // A value as the field takes it, noting one that isn't
    private YamlScalarNode Scalar(string text, FieldSchema schema, List<string> errors)
    {
        switch (schema.Type)
        {
            case FieldType.Integer when !long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _):
                errors.Add($"{Path}: '{text}' isn't a whole number");
                break;
            case FieldType.Number when !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _):
                errors.Add($"{Path}: '{text}' isn't a number");
                break;
            case FieldType.Boolean when !bool.TryParse(text, out _):
                errors.Add($"{Path}: '{text}' isn't true or false");
                break;
            case FieldType.Choice when !schema.Choices.Contains(text, StringComparer.OrdinalIgnoreCase):
                errors.Add($"{Path}: '{text}' isn't one of {string.Join(", ", schema.Choices)}");
                break;
        }
        return new YamlScalarNode(text);
    }

    /// <summary>Where the field is, for messages: "maps › MAP01 › par-time"</summary>
    public string Path
    {
        get
        {
            var names = new List<string>();
            for (var node = this; node != null; node = node.Parent)
            {
                if (node.Key.Length > 0)
                    names.Add(node.Parent?.Schema?.Type == FieldType.List ? $"#{int.Parse(node.Key, CultureInfo.InvariantCulture) + 1}" : node.Key);
            }
            names.Reverse();
            return YamlNodes.Describe(names);
        }
    }

    /// <summary>Empties the field, so the game uses its default (or the value before the mod)</summary>
    [RelayCommand]
    private void Reset()
    {
        if (Kind == FieldKind.Group)
        {
            foreach (var child in Children.ToList())
            {
                if (child.IsEntry)
                    Children.Remove(child);
                else
                    child.ResetQuietly();
            }
        }
        else
            Text = "";
        OnPropertyChanged(nameof(CanReset));
        NotifyChanged();
    }

    private void ResetQuietly()
    {
        _loading = true;
        Text = "";
        _loading = false;
        foreach (var child in Children.ToList())
        {
            if (child.IsEntry)
                Children.Remove(child);
            else
                child.ResetQuietly();
        }
    }

    /// <summary>Adds an entry to a dictionary (asking its key) or an item to a list</summary>
    [RelayCommand]
    private async Task AddAsync()
    {
        if (!CanAdd)
            return;
        EnsureChildren();
        var item = Schema!.Item;
        YamlNode? empty = item?.Type is FieldType.Record or FieldType.Dictionary ? new YamlMappingNode() : item?.Type == FieldType.List ? new YamlSequenceNode() : null;

        string key;
        if (Schema.Type == FieldType.List)
            key = Children.Count.ToString(CultureInfo.InvariantCulture);
        else
        {
            if (_context.AskKey == null)
                return;
            var isNumber = Schema.KeyType is { } keyType && keyType != typeof(string) && keyType != typeof(object);
            var asked = await _context.AskKey($"New entry in {Label}", isNumber ? "Its number:" : "Its key:", candidate =>
            {
                var trimmed = candidate.Trim();
                if (trimmed.Length == 0)
                    return "It needs a key";
                if (isNumber && !long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    return "It's a number";
                return Children.Any(child => child.Key.Equals(trimmed, StringComparison.OrdinalIgnoreCase)) ? $"There's a {trimmed} already" : null;
            });
            if (asked == null)
                return;
            key = asked.Trim();
        }

        var node = new FieldNode(item, key, empty, null, this, _context, isEntry: true);
        Children.Add(node);
        node.EnsureChildren();
        IsExpanded = true;
        node.IsExpanded = true;
        NotifyChanged();
    }

    /// <summary>Takes an entry or item out</summary>
    [RelayCommand]
    private void Remove()
    {
        if (Parent == null || !IsEntry)
            return;
        Parent.Children.Remove(this);
        Parent.RenumberItems();
        NotifyChanged();
    }

    [RelayCommand]
    private void MoveUp() => Move(-1);

    [RelayCommand]
    private void MoveDown() => Move(1);

    private void Move(int by)
    {
        if (Parent == null || !CanMove)
            return;
        int index = Parent.Children.IndexOf(this);
        int to = index + by;
        if (to < 0 || to >= Parent.Children.Count)
            return;
        Parent.Children.Move(index, to);
        Parent.RenumberItems();
        NotifyChanged();
    }

    private void RenumberItems()
    {
        if (Schema?.Type != FieldType.List)
            return;
        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Key = i.ToString(CultureInfo.InvariantCulture);
            Children[i].OnPropertyChanged(nameof(Label));
        }
    }
}
