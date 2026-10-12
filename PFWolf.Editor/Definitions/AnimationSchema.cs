using PFWolf.Assets;

namespace PFWolf.Editor.Definitions;

/// <summary>
/// An animdefs animation's fields. Its frames are names, { name, tics } mappings or a last
/// `stop`, read by hand (AnimationDefinition.Read), so its schema is written out here: a frame is
/// a name, and one written as a mapping is edited as YAML on its line.
/// </summary>
public static class AnimationSchema
{
    public static FieldSchema Build()
        => FieldSchema.Record("animation", "An animation: a name that walls, doors, flats, skies and pictures use like a texture or picture, which shows its frames one after another",
        [
            FieldSchema.Value("tics", FieldType.Integer,
                "How long each frame shows, in tics (70 a second), unless the frame gives its own", defaultText: AnimationDefinition.DefaultTics.ToString()),
            FieldSchema.List("frames",
                "Its frames in order: texture or picture names, all one or the other. { name: SLIME2, tics: 16 } gives a frame its own tics. stop last plays it once and holds the last frame; without it, it loops.",
                FieldSchema.Value("frame", FieldType.Text, "A texture or picture name; { name: X, tics: N } for its own tics; stop, last, to play once", hint: FieldHint.Texture),
                inline: false),
        ]);
}
