using YamlDotNet.Serialization;

namespace PFWolf.Assets.Sounds;

public record SoundSequenceAsset : Asset
{
    /// <summary>
    /// The game's sounds by the names actordefs, mapdefs and the rest play them by (doors/open):
    /// each the digitized, AdLib and PC speaker sound it is. A name not listed plays a sound file
    /// (sounds/NAME), or a digitized, AdLib or PC speaker sound, of that name.
    /// </summary>
    public Dictionary<string, SoundProfile> SoundInfo { get; set; } = [];
}

/// <summary>
/// One of the game's sounds: the best of its digitized, AdLib and PC speaker sounds whose device is
/// switched on plays (silent with none), or else one of random, or alias
/// </summary>
public record SoundProfile
{
    /// <summary>The digitized sound (raw-data-map's digitized-audio: DSDROPN); a pk3's sounds/NAME file of that name wins over it</summary>
    public string? Digitized { get; set; }

    /// <summary>The AdLib sound (ALDROPN), played when digitized sound is off or the sound has none</summary>
    [YamlMember(Alias = "adlib")]
    public string? AdLib { get; set; }

    /// <summary>The PC speaker sound (PCDROPN), played when AdLib is off too or the sound has none</summary>
    [YamlMember(Alias = "pc")]
    public string? PC { get; set; }

    /// <summary>Sound names, one picked at random each time it plays (a guard's death cries)</summary>
    public List<string> Random { get; set; } = [];

    /// <summary>Another sound's name, played in its place (switches/exitbutn: switches/normbutn)</summary>
    public string? Alias { get; set; }
}
