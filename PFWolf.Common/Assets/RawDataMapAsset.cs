namespace PFWolf.Assets;

public record RawDataMapAsset : Asset
{
    public List<string> Walls { get; set; } = [];
    public List<string> Sprites { get; set; } = [];
    public List<string> DigitizedAudio { get; set; } = [];
    public List<string> Audio { get; set; } = [];
    public List<string> Music { get; set; } = [];
    public List<string> Graphics { get; set; } = [];
    public List<string> Maps { get; set; } = [];
}
