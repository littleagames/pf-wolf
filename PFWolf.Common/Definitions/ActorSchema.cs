namespace PFWolf.Definitions;

/// <summary>
/// An actordefs class's fields. ActorData is read by hand (its states are frames, gotos or
/// stop/loop markers), so its schema is written out here rather than worked out from the type.
/// </summary>
public static class ActorSchema
{
    public static FieldSchema Build()
    {
        var frame = FieldSchema.Record("frame",
            "A frame of the state: a sprite shown for a number of tics, with what it does. Written as stop, loop or { goto: State } instead, it ends the state there.",
            [
                FieldSchema.Value("sprite", FieldType.Text, "The sprite's four-letter name (GARD); the frame letter and view number are added to it", hint: FieldHint.Sprite),
                FieldSchema.List("frames", "The frame letters, shown one after the other (A, B, C)", FieldSchema.Value("frame", FieldType.Text, null), inline: true),
                FieldSchema.Value("tics-per-frame", FieldType.Number, "How long each frame shows, in tics (70 a second); -1 stays for good"),
                FieldSchema.Value("think", FieldType.Text, "Called every tic the frame shows (T_Chase)"),
                FieldSchema.Value("action", FieldType.Text, "Called once as the frame starts (A_Shoot)"),
                FieldSchema.Value("next-state", FieldType.Text, "The state that follows the last frame; left out, the next frame in the list"),
                FieldSchema.List("modifiers", "Frame modifiers (bright, fast...)", FieldSchema.Value("modifier", FieldType.Text, null), inline: true),
                FieldSchema.Value("light", FieldType.Text, "A light the actor gives off while the frame shows (see halo lights)"),
                FieldSchema.Value("goto", FieldType.Text, "Written on its own ({ goto: Spawn }): goes on to that state"),
            ]);

        return FieldSchema.Record("class", "An actor class",
        [
            FieldSchema.Value("parent", FieldType.Text, "The class it's built on: it has the parent's flags, properties and states, and changes or adds to them", hint: FieldHint.Class),
            FieldSchema.Value("extend", FieldType.Boolean,
                "true: it changes the class of its name loaded before it (a base pack's, say) rather than replacing it: its properties and states replace those of the same name, and its flags are added (~FLAG takes one away)",
                defaultText: "false"),
            FieldSchema.Value("radius", FieldType.Integer, "How far it reaches from its middle, for bumping into things"),
            FieldSchema.List("flags", "Its flags (SOLID, AMBUSH, NOTCOUNTED...), its parents' as well (~FLAG takes one away). The actor flags reference lists each one.",
                FieldSchema.Value("flag", FieldType.Text, null, hint: FieldHint.Flag), inline: true),
            FieldSchema.Entries("properties",
                "Its properties by name (health, speed, inventory.amount...), its parents' as well unless it sets them. The actor properties reference lists each one.",
                FieldSchema.Value("property", FieldType.Text, null)),
            FieldSchema.Entries("states", "Its states by name (Spawn, See, Death...): each a list of frames",
                FieldSchema.List("state", null, frame)),
        ]);
    }
}
