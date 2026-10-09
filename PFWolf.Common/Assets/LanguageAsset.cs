namespace PFWolf.Assets;

/// <summary>
/// Text strings for one language, keyed by $NAME (language/en-us.yaml, gamepacks/{pack}/language/en-us.yaml)
/// </summary>
public record LanguageAsset : Asset
{
    public LanguageAsset()
    {
    }

    public LanguageAsset(Dictionary<string, string> strings)
    {
        Strings = strings;
    }

    public Dictionary<string, string> Strings { get; set; } = [];
}
