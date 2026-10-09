using YamlDotNet.Serialization;

namespace PFWolf.Assets.Sounds;

public record SoundSequenceAsset : Asset
{
    public Dictionary<string, SoundProfile> SoundInfo { get; set; } = [];
}

public record SoundProfile
{
    public string? Digitized { get; set; }

    [YamlMember(Alias = "adlib")]
    public string? AdLib { get; set; }

    [YamlMember(Alias = "pc")]
    public string? PC { get; set; }

    public List<string> Random { get; set; } = [];
    public string? Alias { get; set; }
}
