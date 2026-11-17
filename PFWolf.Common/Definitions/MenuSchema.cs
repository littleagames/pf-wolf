using PFWolf.Assets;
using PFWolf.Menus;

namespace PFWolf.Definitions;

/// <summary>
/// A menudefs menu's fields, for the editor's menu window: MenuAsset's, each with its doc comment,
/// with the types and actions as choices and the names that are menus, texts, pictures, fonts and
/// colors marked so they're offered. A component's params are edited as one record of every
/// component type's params (the ones its type doesn't read left out unless set), the editor
/// turning menudefs' list of one-key mappings into it and back.
/// </summary>
public static class MenuSchema
{
    /// <summary>The component types, and the record each is read into</summary>
    public static IReadOnlyDictionary<string, Type> ComponentTypes { get; } = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
    {
        ["Background"] = typeof(Background),
        ["Window"] = typeof(Window),
        ["Fill"] = typeof(Fill),
        ["Graphic"] = typeof(Graphic),
        ["Stripe"] = typeof(Stripe),
        ["Label"] = typeof(Label),
    };

    /// <summary>The menu item types</summary>
    public static IReadOnlyList<string> ItemTypes { get; } =
        ["MenuSwitcher", "ToggleMenuItem", "ChoiceMenuItem", "SliderMenuItem", "MusicMenuItem", "ControlMenuItem", "HeaderMenuItem", "BlankMenuItem"];

    // MergeYaml's own keys: a mod's file places or takes out an entry with them; nothing in a menu as it's edited
    private static readonly string[] MergeKeys = ["before", "after", "remove"];

    public static FieldSchema Build()
    {
        var fields = new List<FieldSchema>();
        foreach (var field in FieldSchema.For(typeof(MenuAsset)).Fields)
        {
            fields.Add(field.Key switch
            {
                "items-source" => FieldSchema.Value(field.Key, FieldType.Choice, field.Description, choices: [.. MenuNames.ItemsSources.Keys]),
                "item-shadow" => FieldSchema.Value(field.Key, FieldType.Text, field.Description, hint: FieldHint.Color),
                "components" => FieldSchema.List(field.Key, field.Description, Component(), inline: false),
                "menu-items" => FieldSchema.List(field.Key, field.Description, Item(), inline: false),
                _ => field,
            });
        }
        return FieldSchema.Record("menu", "A menudefs menu: what's drawn behind it, its rows and how they're laid out", fields);
    }

    private static FieldSchema Item()
    {
        var fields = FieldSchema.For(typeof(MenuItemEntry)).Fields
            .Where(field => !MergeKeys.Contains(field.Key, StringComparer.OrdinalIgnoreCase))
            .Select(field => field.Key switch
            {
                "type" => FieldSchema.Value(field.Key, FieldType.Choice, field.Description, choices: ItemTypes),
                "action" => FieldSchema.Value(field.Key, FieldType.Choice, field.Description, choices: [.. MenuNames.AllActions]),
                "menu" => FieldSchema.Value(field.Key, FieldType.Text, field.Description, hint: FieldHint.Menu),
                "article" => FieldSchema.Value(field.Key, FieldType.Text, field.Description, hint: FieldHint.Article),
                "music" => FieldSchema.Value(field.Key, FieldType.Text, field.Description, hint: FieldHint.Music),
                _ => field,
            }).ToList();
        return FieldSchema.Record("", "A row of the menu", fields);
    }

    private static FieldSchema Component()
    {
        var entry = FieldSchema.For(typeof(ComponentEntry)).Fields.ToDictionary(field => field.Key, StringComparer.OrdinalIgnoreCase);
        return FieldSchema.Record("", "Something drawn behind the rows, in order", [
            FieldSchema.Value("id", FieldType.Text, entry["id"].Description),
            FieldSchema.Value("type", FieldType.Choice, entry["type"].Description, choices: [.. ComponentTypes.Keys]),
            entry["game-packs"],
            Params(),
        ]);
    }

    /// <summary>A component's params as one record: every type's, each once</summary>
    public static FieldSchema Params()
    {
        var fields = new List<FieldSchema>();
        foreach (var type in ComponentTypes.Values)
            foreach (var field in FieldSchema.For(type).Fields)
                if (field.Key is not ("game-packs" or "id") && !fields.Any(known => known.Key.Equals(field.Key, StringComparison.OrdinalIgnoreCase)))
                    fields.Add(field.Key == "name" ? FieldSchema.Value("name", FieldType.Text, "The picture's name", hint: FieldHint.Picture) : field);
        return FieldSchema.Record("params", "What the component has: its place and size, color, picture, text", fields);
    }

    /// <summary>Whether a component of <paramref name="type"/> reads the param <paramref name="key"/> (any does, for an unknown type)</summary>
    public static bool ParamAppliesTo(string key, string? type)
        => type == null || !ComponentTypes.TryGetValue(type, out var record)
           || FieldSchema.For(record).Fields.Any(field => field.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}
