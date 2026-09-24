namespace Wolf3D.Assets;

/// <summary>
/// Text strings for one language, keyed by $NAME (language/en-us.yaml, gamepacks/{pack}/language/en-us.yaml)
/// </summary>
internal record LanguageAsset : Asset
{
    public LanguageAsset()
    {
    }

    public LanguageAsset(Dictionary<string, string> strings)
    {
        Strings = strings;
    }

    public Dictionary<string, string> Strings { get; set; } = [];

    public override void Merge(Asset other)
    {
        if (other is LanguageAsset otherAsset)
        {
            foreach (var item in otherAsset.Strings)
                this.Strings[item.Key] = item.Value;
        }
    }
}
