namespace PFWolf.Assets.Sounds;

/// <summary>
/// The reverbs a zone, a map or a script can name (game-info reverb, game.reverb): Creative's
/// EFX presets as OpenAL's efx-presets.h has them (OpenTK's ReverbPresets), in hyphenated lower
/// case. The engine finds each one's numbers by its name without the hyphens; "none" is dry.
/// Kept here, apart from OpenAL, for the documentation, the editor and the warnings.
/// </summary>
public static class ReverbNames
{
    /// <summary>A reverb of none: dry</summary>
    public const string None = "none";

    /// <summary>Every preset's name</summary>
    public static readonly IReadOnlyList<string> All =
    [
        "alley", "arena", "auditorium", "bathroom", "carpeted-hallway",
        "castle-alcove", "castle-courtyard", "castle-cupboard", "castle-hall", "castle-large-room",
        "castle-long-passage", "castle-medium-room", "castle-short-passage", "castle-small-room",
        "cave", "chapel",
        "city", "city-abandoned", "city-library", "city-museum", "city-streets", "city-subway", "city-underpass",
        "concert-hall", "dizzy", "dome-saint-pauls", "dome-tomb",
        "driving-commentator", "driving-empty-grand-stand", "driving-full-grand-stand", "driving-in-car-luxury",
        "driving-in-car-racer", "driving-in-car-sports", "driving-pit-garage", "driving-tunnel",
        "drugged", "dusty-room",
        "factory-alcove", "factory-courtyard", "factory-cupboard", "factory-hall", "factory-large-room",
        "factory-long-passage", "factory-medium-room", "factory-short-passage", "factory-small-room",
        "forest", "generic", "hallway", "hangar",
        "ice-palace-alcove", "ice-palace-courtyard", "ice-palace-cupboard", "ice-palace-hall", "ice-palace-large-room",
        "ice-palace-long-passage", "ice-palace-medium-room", "ice-palace-short-passage", "ice-palace-small-room",
        "living-room", "mood-heaven", "mood-hell", "mood-memory", "mountains",
        "outdoors-backyard", "outdoors-creek", "outdoors-deep-canyon", "outdoors-rolling-plains", "outdoors-valley",
        "padded-cell", "parking-lot", "pipe-large", "pipe-long-thin", "pipe-resonant", "pipe-small", "plain",
        "prefab-caravan", "prefab-outhouse", "prefab-practise-room", "prefab-school-room", "prefab-workshop",
        "psychotic", "quarry", "room", "sewer-pipe", "small-water-room",
        "space-station-alcove", "space-station-cupboard", "space-station-hall", "space-station-large-room",
        "space-station-long-passage", "space-station-medium-room", "space-station-short-passage", "space-station-small-room",
        "sport-empty-stadium", "sport-full-stadium", "sport-gymnasium", "sport-large-swimming-pool",
        "sport-small-swimming-pool", "sport-squash-court", "sport-stadium-tannoy",
        "stone-corridor", "stone-room", "underwater",
        "wooden-galleon-alcove", "wooden-galleon-courtyard", "wooden-galleon-cupboard", "wooden-galleon-hall",
        "wooden-galleon-large-room", "wooden-galleon-long-passage", "wooden-galleon-medium-room",
        "wooden-galleon-short-passage", "wooden-galleon-small-room",
    ];

    /// <summary>A name as it's matched: no hyphens, spaces or underscores, any case</summary>
    public static string Key(string name) =>
        new string(name.Where(c => c is not ('-' or ' ' or '_')).ToArray()).ToLowerInvariant();

    static readonly Dictionary<string, string> byKey = All.ToDictionary(Key, name => name);

    /// <summary>A preset's name as the list has it, for a name written any way (Sewer_Pipe, sewerpipe); null for none there is</summary>
    public static string? Find(string name) => byKey.GetValueOrDefault(Key(name));

    /// <summary>Whether it's "none" (or "off"): dry</summary>
    public static bool IsNone(string? name) =>
        name == null || name.Trim().Equals(None, StringComparison.OrdinalIgnoreCase) || name.Trim().Equals("off", StringComparison.OrdinalIgnoreCase);
}
