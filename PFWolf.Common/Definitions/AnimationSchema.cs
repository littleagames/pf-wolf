using PFWolf.Assets;

namespace PFWolf.Definitions;

/// <summary>
/// An animdefs animation's fields. Its frames are names, { name, tics } mappings or a last
/// `stop`, read by hand (AnimationDefinition.Read), so its schema is written out here: a frame is
/// a name, and one written as a mapping is edited as YAML on its line.
/// </summary>
public static class AnimationSchema
{
    public static FieldSchema Build()
        => FieldSchema.Record("animation",
            "An animation: a name that walls, doors, flats, skies and pictures (menus, the status bar) use like a texture or picture, which shows its frames one after another. "
            + "It can take the name of one of its own frames (GSTONEA1: frames [GSTONEA1, ...]) to animate a texture everywhere it's used; frames are always the textures and pictures themselves, never other animations. "
            + "Walls, doors, flats and skies go by the level's clock: every tile showing an animation is on the same frame, and they stop while the game is paused. "
            + "Pictures go by real time, and change whenever the screen they're on is drawn again. The automap shows a wall's first frame.",
        [
            FieldSchema.Value("tics", FieldType.Integer,
                "How long each frame shows, in tics (70 a second), unless the frame gives its own", defaultText: AnimationDefinition.DefaultTics.ToString()),
            FieldSchema.List("frames",
                "Its frames in order: texture or picture names, all one or the other. { name: SLIME2, tics: 16 } gives a frame its own tics. stop last plays it once and holds the last frame; without it, it loops.",
                FieldSchema.Value("frame", FieldType.Text, "A texture or picture name; { name: X, tics: N } for its own tics; stop, last, to play once", hint: FieldHint.Texture),
                inline: false),
        ]);
}
