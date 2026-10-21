namespace PFWolf.Assets;

/// <summary>
/// A game pack's raw-data-map.yaml: the names its original data files' contents go by, each list
/// in the order the file has them (the first name is the first chunk, and so on)
/// </summary>
public record RawDataMapAsset : Asset
{
    /// <summary>VSWAP's wall textures</summary>
    public List<string> Walls { get; set; } = [];

    /// <summary>VSWAP's sprites</summary>
    public List<string> Sprites { get; set; } = [];

    /// <summary>VSWAP's digitized sounds</summary>
    public List<string> DigitizedAudio { get; set; } = [];

    /// <summary>AUDIOT's sound effects: the PC speaker ones, then the AdLib ones</summary>
    public List<string> Audio { get; set; } = [];

    /// <summary>AUDIOT's music</summary>
    public List<string> Music { get; set; } = [];

    /// <summary>VGAGRAPH's pictures and other chunks</summary>
    public List<string> Graphics { get; set; } = [];

    /// <summary>GAMEMAPS' levels (MAP01...)</summary>
    public List<string> Maps { get; set; } = [];
}
