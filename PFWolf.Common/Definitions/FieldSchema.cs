using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PFWolf.Assets;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PFWolf.Definitions;

/// <summary>What a field holds, which picks how it's edited</summary>
public enum FieldType
{
    Text,
    Integer,
    Number,
    Boolean,
    /// <summary>One of a set of names (an enum)</summary>
    Choice,
    /// <summary>A mapping of named fields (a record)</summary>
    Record,
    /// <summary>A mapping of any keys to values of one kind</summary>
    Dictionary,
    List,
}

/// <summary>What a text field names, so it can offer the names there are</summary>
public enum FieldHint
{
    None,
    Texture,
    Picture,
    Class,
    Sound,
    Music,
    Map,
    Font,
    Color,
    /// <summary>An actor flag (SHOOTABLE)</summary>
    Flag,
    /// <summary>A sprite's four-letter name (GARD)</summary>
    Sprite,
    /// <summary>A kind of rain or snow (game-info weather-types, or a built-in one)</summary>
    Weather,
    /// <summary>A side actors are on (factions.yaml)</summary>
    Faction,
    /// <summary>A sprite color translation (translations.yaml)</summary>
    Translation,
    /// <summary>A sprite (GARDA1), wall texture or picture by its own name: what graphicdefs crops</summary>
    Art,
}

/// <summary>
/// A field of a definition as the game reads it: worked out from the asset type the YAML is read
/// into (its properties, named as the files name them, hyphenated), with the property's doc
/// comment to say what it's for, and its default (what the game uses when it's left out)
/// </summary>
public sealed class FieldSchema
{
    private readonly Func<FieldSchema?>? _item;
    private readonly Type? _recordType;
    private IReadOnlyList<FieldSchema>? _fields;
    private FieldSchema? _itemSchema;
    private bool _itemBuilt;

    private FieldSchema(string key, string label, string? description, FieldType type, Type clrType, IReadOnlyList<string> choices,
        string? defaultText, FieldHint hint, Func<FieldSchema?>? item = null, Type? keyType = null, Type? recordType = null)
    {
        Key = key;
        Label = label;
        Description = description;
        Type = type;
        ClrType = clrType;
        Choices = choices;
        Default = defaultText;
        Hint = hint;
        _item = item;
        KeyType = keyType;
        _recordType = recordType;
    }

    /// <summary>Its key in the YAML ("par-time")</summary>
    public string Key { get; }

    /// <summary>Its name as shown ("Par time")</summary>
    public string Label { get; }

    /// <summary>What it's for, from the property's doc comment; null when it has none</summary>
    public string? Description { get; }

    public FieldType Type { get; }
    public Type ClrType { get; }

    /// <summary>The names a Choice can be (and true/false for a Boolean)</summary>
    public IReadOnlyList<string> Choices { get; }

    /// <summary>What the game uses when it's left out, as text; null when that's nothing</summary>
    public string? Default { get; }

    public FieldHint Hint { get; }

    /// <summary>
    /// How it's written when it's called or typed rather than set (`A_Jump(chance, "State")`,
    /// `map &lt;MAP##&gt;`): an action, a script function, a command or an option. Null for a field.
    /// </summary>
    public string? Signature { get; private init; }

    /// <summary>
    /// A record that only gathers entries of a reference under a heading ("The weapon in hand"):
    /// they keep their own names, rather than being named within it as a record's fields are
    /// (Lua's game.random)
    /// </summary>
    public bool IsGroup { get; private init; }

    /// <summary>A Dictionary's key type (int for the mapdefs tables)</summary>
    public Type? KeyType { get; }

    /// <summary>What a List's items, or a Dictionary's values, are</summary>
    public FieldSchema? Item
    {
        get
        {
            if (!_itemBuilt)
            {
                _itemSchema = _item?.Invoke();
                _itemBuilt = true;
            }
            return _itemSchema;
        }
    }

    /// <summary>A Record's fields, in the order the type has them</summary>
    public IReadOnlyList<FieldSchema> Fields => _fields ??= _recordType == null ? [] : RecordFields(_recordType);

    public bool IsScalar => Type is FieldType.Text or FieldType.Integer or FieldType.Number or FieldType.Boolean or FieldType.Choice;

    /// <summary>A list of numbers or names, edited as one line ("1, 2, 3") rather than a row each</summary>
    public bool IsInlineList => Type == FieldType.List && (_inline ?? (Item is { Type: FieldType.Integer or FieldType.Number or FieldType.Choice or FieldType.Boolean }
                                                                    || Item is { Type: FieldType.Text, Hint: not FieldHint.None }));

    // Whether a list is edited on one line: null works it out from its items
    private bool? _inline;

    /// <summary>A field of a hand-made schema: a value</summary>
    public static FieldSchema Value(string key, FieldType type, string? description, string? defaultText = null, FieldHint hint = FieldHint.None,
        IReadOnlyList<string>? choices = null)
        => new(key, LabelFor(key), description, type, typeof(string), choices ?? (type == FieldType.Boolean ? ["true", "false"] : []), defaultText, hint);

    /// <summary>An entry of a hand-made reference of things that are called or typed: how it's written, and what it does</summary>
    public static FieldSchema Callable(string name, string signature, string description)
        => new(name, name, description, FieldType.Text, typeof(string), [], null, FieldHint.None) { Signature = signature };

    /// <summary>A heading over some of a reference's entries (see <see cref="IsGroup"/>)</summary>
    public static FieldSchema Group(string title, string? description, IReadOnlyList<FieldSchema> entries)
        => new(title, title, description, FieldType.Record, typeof(object), [], null, FieldHint.None) { _fields = entries, IsGroup = true };

    /// <summary>A field of a hand-made schema: named fields of its own</summary>
    public static FieldSchema Record(string key, string? description, IReadOnlyList<FieldSchema> fields)
        => new(key, LabelFor(key), description, FieldType.Record, typeof(object), [], null, FieldHint.None) { _fields = fields };

    /// <summary>A field of a hand-made schema: entries by key, each an <paramref name="item"/></summary>
    public static FieldSchema Entries(string key, string? description, FieldSchema item)
        => new(key, LabelFor(key), description, FieldType.Dictionary, typeof(object), [], null, FieldHint.None, () => item, keyType: typeof(string));

    /// <summary>
    /// A field of a hand-made schema: a list of <paramref name="item"/>, inline (A, B, C) or a row
    /// each; left out, inline for numbers, choices and names
    /// </summary>
    public static FieldSchema List(string key, string? description, FieldSchema item, bool? inline = null, string? defaultText = null)
        => new(key, LabelFor(key), description, FieldType.List, typeof(object), [], defaultText, item.Hint, () => item) { _inline = inline };

    /// <summary>
    /// A value to start a field the game doesn't set from, to fill in: a list or dictionary
    /// empty, a record its fields that have defaults (at those defaults), a value its default
    /// (or empty text)
    /// </summary>
    public YamlDotNet.RepresentationModel.YamlNode StarterValue()
    {
        switch (Type)
        {
            case FieldType.List:
                return new YamlDotNet.RepresentationModel.YamlSequenceNode();
            case FieldType.Dictionary:
                return new YamlDotNet.RepresentationModel.YamlMappingNode();
            case FieldType.Record:
            {
                var mapping = new YamlDotNet.RepresentationModel.YamlMappingNode();
                foreach (var field in Fields.Where(field => field.IsScalar && field.Default != null))
                    mapping.Add(field.Key, field.StarterValue());
                return mapping;
            }
            default:
                return new YamlDotNet.RepresentationModel.YamlScalarNode(Default ?? "")
                {
                    Style = Default == null ? YamlDotNet.Core.ScalarStyle.SingleQuoted : YamlDotNet.Core.ScalarStyle.Any,
                };
        }
    }

    /// <summary>A Record's field by its key, ignoring case (as the game reads them)</summary>
    public FieldSchema? Field(string key) => Fields.FirstOrDefault(field => field.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public override string ToString() => $"{Key} ({Type})";

    /// <summary>The schema of a type the YAML is read as (GameInfoAsset, MapTextureTranslation...)</summary>
    public static FieldSchema For(Type type, string key = "", string? label = null, string? description = null, string? defaultText = null, FieldHint hint = FieldHint.None)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        label ??= key.Length > 0 ? LabelFor(key) : underlying.Name;

        if (underlying == typeof(string) || underlying == typeof(char) || underlying == typeof(object))
            return new FieldSchema(key, label, description, FieldType.Text, underlying, [], defaultText, hint);
        if (underlying == typeof(bool))
            return new FieldSchema(key, label, description, FieldType.Boolean, underlying, ["true", "false"], defaultText, FieldHint.None);
        if (underlying == typeof(int) || underlying == typeof(short) || underlying == typeof(long) || underlying == typeof(byte)
            || underlying == typeof(ushort) || underlying == typeof(uint) || underlying == typeof(sbyte))
            return new FieldSchema(key, label, description, FieldType.Integer, underlying, [], defaultText, FieldHint.None);
        if (underlying == typeof(float) || underlying == typeof(double) || underlying == typeof(decimal))
            return new FieldSchema(key, label, description, FieldType.Number, underlying, [], defaultText, FieldHint.None);
        if (underlying.IsEnum)
            return new FieldSchema(key, label, description, FieldType.Choice, underlying, Enum.GetNames(underlying), defaultText, FieldHint.None);

        if (underlying.IsGenericType && underlying.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var args = underlying.GetGenericArguments();
            return new FieldSchema(key, label, description, FieldType.Dictionary, underlying, [], null, hint,
                () => For(args[1], "", "Entry", null, null, hint), keyType: args[0]);
        }
        if (underlying.IsGenericType && underlying.GetGenericTypeDefinition() is var generic
            && (generic == typeof(List<>) || generic == typeof(HashSet<>) || generic == typeof(IReadOnlyList<>)))
        {
            var itemType = underlying.GetGenericArguments()[0];
            return new FieldSchema(key, label, description, FieldType.List, underlying, [], defaultText, hint,
                () => For(itemType, "", "Item", null, null, hint));
        }

        return new FieldSchema(key, label, description, FieldType.Record, underlying, [], null, hint, recordType: underlying);
    }

    // The properties the YAML sets, and the public fields (a menu's position, a Vector2's X and Y),
    // with their defaults from a new instance
    private static List<FieldSchema> RecordFields(Type type)
    {
        object? instance = null;
        try
        {
            if (type.IsValueType || type.GetConstructor(System.Type.EmptyTypes) != null)
                instance = Activator.CreateInstance(type);
        }
        catch (TargetInvocationException)
        {
        }

        var fields = new List<FieldSchema>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0 || property.GetSetMethod(nonPublic: true) == null
                || property.DeclaringType == typeof(Asset) || property.GetCustomAttribute<YamlIgnoreAttribute>() != null)
                continue;

            var key = property.GetCustomAttribute<YamlMemberAttribute>()?.Alias ?? HyphenatedNamingConvention.Instance.Apply(property.Name);
            var propertyType = property.PropertyType;
            var defaultText = instance == null ? null : DefaultText(property.GetValue(instance));
            var hint = HintFor(property.Name);
            fields.Add(For(propertyType, key, LabelFor(key), DocComments.Summary(property), defaultText, hint));
        }

        // YamlDotNet sets public fields as it does properties
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.IsInitOnly || field.GetCustomAttribute<YamlIgnoreAttribute>() != null)
                continue;

            var key = field.GetCustomAttribute<YamlMemberAttribute>()?.Alias ?? HyphenatedNamingConvention.Instance.Apply(field.Name);
            var defaultText = instance == null ? null : DefaultText(field.GetValue(instance));
            fields.Add(For(field.FieldType, key, LabelFor(key), DocComments.Summary(field), defaultText, HintFor(field.Name)));
        }
        return fields;
    }

    private static string? DefaultText(object? value) => value switch
    {
        null => null,
        string text => text.Length > 0 ? text : null,
        bool flag => flag ? "true" : "false",
        char c => c == '\0' ? null : c.ToString(),     // before IFormattable, which char is too
        IFormattable number when value is not Enum => number.ToString(null, CultureInfo.InvariantCulture),
        Enum choice => choice.ToString(),
        IDictionary => null,
        IEnumerable items when value is not string => items.Cast<object?>().Select(DefaultText).ToList() is { Count: > 0 } list ? string.Join(", ", list) : null,
        _ => null,
    };

    /// <summary>"par-time" as it's shown: "Par time"</summary>
    public static string LabelFor(string key)
    {
        var words = key.Replace('-', ' ').Replace('_', ' ').Replace('.', ' ').Trim();
        return words.Length == 0 ? key : char.ToUpperInvariant(words[0]) + words[1..];
    }

    // What a property names, by its name: north is a texture, music a song, next a level
    private static FieldHint HintFor(string propertyName)
    {
        var name = propertyName.ToLowerInvariant();
        if (name.EndsWith("music"))
            return FieldHint.Music;
        if (name is "next" or "secretnext" or "startmap")
            return FieldHint.Map;
        if (name.EndsWith("sound"))
            return FieldHint.Sound;
        if (name is "north" or "south" or "east" or "west" or "texture" or "sky" or "defaultfloor" or "defaultceiling" or "floor" or "ceiling")
            return FieldHint.Texture;
        if (name is "class" or "else" or "drop" or "lock" or "parent")
            return FieldHint.Class;
        if (name.Contains("pic") || name is "menubackdrop" or "frame")
            return FieldHint.Picture;
        if (name is "font")
            return FieldHint.Font;
        if (name.Contains("color"))
            return FieldHint.Color;
        if (name is "weather")
            return FieldHint.Weather;
        if (name.EndsWith("faction"))
            return FieldHint.Faction;
        if (name.Contains("translation") || name is "playercolors")
            return FieldHint.Translation;
        return FieldHint.None;
    }
}

/// <summary>The doc comments of PFWolf.Common's types, from the XML file built beside it</summary>
public static class DocComments
{
    private static Dictionary<string, string>? _summaries;

    /// <summary>A property's summary as plain text, or null when it has none</summary>
    public static string? Summary(PropertyInfo property)
    {
        var type = property.DeclaringType;
        if (type == null)
            return null;
        var name = $"P:{type.FullName?.Replace('+', '.')}.{property.Name}";
        return Load(type.Assembly).GetValueOrDefault(name);
    }

    /// <summary>A field's summary as plain text, or null when it has none</summary>
    public static string? Summary(FieldInfo field)
    {
        if (field.DeclaringType is not { } type)
            return null;
        var name = $"F:{type.FullName?.Replace('+', '.')}.{field.Name}";
        return type.Assembly == typeof(DocComments).Assembly ? Load(type.Assembly).GetValueOrDefault(name) : Outside.GetValueOrDefault(name);
    }

    // What the definitions' fields of types from outside PFWolf.Common say, as they've no doc comments of ours
    private static readonly Dictionary<string, string> Outside = new()
    {
        ["F:System.Numerics.Vector2.X"] = "Across, in pixels",
        ["F:System.Numerics.Vector2.Y"] = "Down, in pixels",
    };

    /// <summary>A type's summary as plain text, or null when it has none (or isn't PFWolf.Common's)</summary>
    public static string? TypeSummary(Type type)
    {
        if (type.Assembly != typeof(DocComments).Assembly)
            return null;
        return Load(type.Assembly).GetValueOrDefault($"T:{type.FullName?.Replace('+', '.')}");
    }

    private static Dictionary<string, string> Load(Assembly assembly)
    {
        if (_summaries != null)
            return _summaries;

        _summaries = [];
        try
        {
            var path = Path.ChangeExtension(assembly.Location, ".xml");
            if (!File.Exists(path))
                path = Path.Combine(AppContext.BaseDirectory, Path.GetFileNameWithoutExtension(assembly.Location) + ".xml");
            if (!File.Exists(path))
                return _summaries;

            foreach (var member in XDocument.Load(path).Descendants("member"))
            {
                if (member.Attribute("name")?.Value is { } name && member.Element("summary") is { } summary)
                    _summaries[name] = PlainText(summary);
            }
        }
        catch (Exception e) when (e is IOException or System.Xml.XmlException or UnauthorizedAccessException)
        {
        }
        return _summaries;
    }

    // <see cref="T:PFWolf.Assets.MapInfo"/> as MapInfo, <c>x</c> as x, all on one line
    private static string PlainText(XElement element)
    {
        var text = string.Concat(element.Nodes().Select(node => node switch
        {
            XText plain => plain.Value,
            XElement { Name.LocalName: "see" or "seealso" } see =>
                see.Attribute("cref")?.Value is { } cref ? cref[(cref.LastIndexOfAny([':', '.']) + 1)..] : see.Attribute("langword")?.Value ?? see.Value,
            XElement { Name.LocalName: "paramref" or "typeparamref" } reference => reference.Attribute("name")?.Value ?? "",
            XElement inner => PlainText(inner),
            _ => "",
        }));
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}
