using YamlDotNet.Serialization;

namespace Wolf3D.Assets.Sounds;

internal record SoundSequenceAsset : Asset
{
    public Dictionary<string, SoundProfile> SoundInfo { get; set; } = [];
    public override void Merge(Asset other)
    {
        throw new NotImplementedException();
    }
}

internal record SoundProfile
{
    public string? Digitized { get; set; }

    [YamlMember(Alias = "adlib")]
    public string? AdLib { get; set; }

    [YamlMember(Alias = "pc")]
    public string? PC { get; set; }

    public List<string> Random { get; set; } = [];
    public string? Alias { get; set; }
}
