using YamlDotNet.Serialization;

namespace Wolf3D.Assets.Sounds;

internal record SoundSequenceAsset : Asset
{
    public Dictionary<string, SoundProfile> SoundInfo { get; set; } = [];
    // A later file's entries replace these, name by name
    public override void Merge(Asset other)
    {
        if (other is SoundSequenceAsset sequence)
            foreach (var (name, profile) in sequence.SoundInfo)
                SoundInfo[name] = profile;
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
