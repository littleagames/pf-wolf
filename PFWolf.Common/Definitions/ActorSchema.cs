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
                FieldSchema.Value("tics-per-frame", FieldType.Number, "How long each frame shows, in tics (70 a second); 0 ends it the moment it's entered, and -1 stays for good"),
                FieldSchema.Value("think", FieldType.Text, "Called every tic the frame shows (T_Chase)"),
                FieldSchema.Value("action", FieldType.Text, "Called once as the frame ends, before the next one shows (A_Shoot); a 0-tic frame's is called as soon as it's entered"),
                FieldSchema.Value("next-state", FieldType.Text,
                    "The state that follows the last frame. Only the state's last entry can have one: the frames of an entry before it go on to the next entry's. Left out of the last entry, the state loops back to its own first frame"),
                FieldSchema.List("modifiers", "Frame modifiers: bright, drawn at full light, unshaded (the BRIGHT flag does it for every frame); lighten(N), darkening N fade steps (of 32) less with distance on a shaded level, as the lighten property does for every frame. Any other word is ignored, with a warning as the game loads.",
                    FieldSchema.Value("modifier", FieldType.Text, null), inline: true),
                FieldSchema.Value("light", FieldType.Text,
                    "The actor's light while the frame shows, in place of its light.* properties: none (or off) for none, an intensity (200) for its light that bright, or { intensity, radius, color } with any of them, the rest its own"),
                FieldSchema.Value("goto", FieldType.Text, "Written on its own ({ goto: Spawn }): goes on to that state"),
            ]);

        return FieldSchema.Record("class", "An actor class",
        [
            FieldSchema.Value("parent", FieldType.Text, "The class it's built on: it has the parent's flags, properties and states, and changes or adds to them", hint: FieldHint.Class),
            FieldSchema.Value("extend", FieldType.Boolean,
                "true: it changes the class of its name loaded before it (a base pack's, say) rather than replacing it: its properties and states replace those of the same name, and its flags are added (~FLAG takes one away)",
                defaultText: "false"),
            FieldSchema.Value("radius", FieldType.Integer,
                "How far it reaches from its middle, in texels (32: the whole tile). Nothing bumps into it by this (blocking goes by whole tiles, with the SOLID flag); it's how much health a Prop Hunt prop looking like it has, when it has no prophunt.health"),
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
