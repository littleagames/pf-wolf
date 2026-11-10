using PFWolf.Assets;

namespace PFWolf.Definitions;

/// <summary>
/// A fonts.yaml font's fields: FontDefinition's, each with its doc comment, with the type as a
/// choice and the names that are pictures, fonts or colors marked so they're offered
/// </summary>
public static class FontSchema
{
    public const string Vga = "vga", Graphic = "graphic", Sheet = "sheet";

    public static IReadOnlyList<string> Types { get; } = [Vga, Graphic, Sheet];

    public static FieldSchema Build()
    {
        var fields = FieldSchema.For(typeof(FontDefinition)).Fields.Select(field => field.Key switch
        {
            "type" => FieldSchema.Value("type", FieldType.Choice, field.Description, Vga, choices: Types),
            "source" => FieldSchema.Value("source", FieldType.Text, field.Description, hint: FieldHint.Font),
            "image" => FieldSchema.Value("image", FieldType.Text, field.Description, hint: FieldHint.Picture),
            "transparent" => FieldSchema.Value("transparent", FieldType.Text, field.Description, hint: FieldHint.Color),
            "glyphs" => FieldSchema.Entries("glyphs", field.Description,
                FieldSchema.Value("", FieldType.Text, "The picture for the character", hint: FieldHint.Picture)),
            _ => field,
        }).ToList();
        return FieldSchema.Record("font", "A font text is printed in by name: menus, messages, the status bar, the intermission, Lua's game.drawtext", fields);
    }

    // The keys only some types read; every other key is read by all three
    private static readonly Dictionary<string, string[]> OnlyFor = new(StringComparer.OrdinalIgnoreCase)
    {
        ["source"] = [Vga],
        ["cursor"] = [Vga],
        ["image"] = [Sheet],
        ["cell-height"] = [Sheet],
        ["first-char"] = [Sheet],
        ["proportional"] = [Sheet],
        ["spacing"] = [Sheet],
        ["colorize"] = [Graphic, Sheet],
        ["transparent"] = [Graphic, Sheet],
        ["pattern"] = [Graphic],
        ["chars"] = [Graphic, Sheet],
        ["glyphs"] = [Graphic],
        ["advances"] = [Graphic, Sheet],
        ["cell-width"] = [Graphic, Sheet],
        ["height"] = [Graphic, Sheet],
        ["line-height"] = [Graphic, Sheet],
        ["space-width"] = [Graphic, Sheet],
    };

    /// <summary>Whether a font of <paramref name="type"/> reads the key <paramref name="key"/></summary>
    public static bool AppliesTo(string key, string? type)
    {
        type = string.IsNullOrEmpty(type) ? Vga : type.ToLowerInvariant();
        return !OnlyFor.TryGetValue(key, out var types) || types.Contains(type);
    }
}
