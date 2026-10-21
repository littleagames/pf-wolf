using PFWolf.Assets;

namespace PFWolf.Definitions;

/// <summary>
/// A graphicdefs picture's fields: a rectangle cut out of a sprite, wall or picture
/// (GraphicDefinition), written out so each field says what it does
/// </summary>
public static class GraphicSchema
{
    public static FieldSchema Build()
        => FieldSchema.Record("picture", "A picture cut out of a sprite, wall or other picture, exactly width by height, used by name like any picture (menus, the status bar, player.face)",
        [
            FieldSchema.Value("source", FieldType.Text, "The sprite, wall or picture it's cut from (GARDA1)", hint: FieldHint.Art),
            FieldSchema.Value("kind", FieldType.Choice,
                "Which the source is, where a sprite, wall or picture share its name; left out, the first of sprite, texture, graphic there is",
                choices: [GraphicDefinition.SpriteKind, GraphicDefinition.TextureKind, GraphicDefinition.GraphicKind]),
            FieldSchema.Value("x", FieldType.Integer, "The crop's left edge in the source, in pixels", defaultText: "0"),
            FieldSchema.Value("y", FieldType.Integer, "The crop's top edge in the source, in pixels", defaultText: "0"),
            FieldSchema.Value("width", FieldType.Integer, "The picture's width, in pixels"),
            FieldSchema.Value("height", FieldType.Integer, "The picture's height, in pixels"),
            FieldSchema.Value("background", FieldType.Text,
                "Fills the pixels that don't show (a sprite's, a masked wall's, past the source's edge): a colors.yaml name, #RRGGBB or palette index. Left out, they stay see-through.",
                hint: FieldHint.Color),
        ]);
}
