namespace PFWolf.Assets;

public record AliasAsset : Asset
{
    public Dictionary<int, string> ArtExtern { get; set; } = [];
}
