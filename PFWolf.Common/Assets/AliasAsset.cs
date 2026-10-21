namespace PFWolf.Assets;

/// <summary>A game pack's alias.yaml: names for things the original games refer to by number</summary>
public record AliasAsset : Asset
{
    /// <summary>
    /// The pictures articles' ^G and ^T commands draw, by the number they give (the picture's
    /// chunk number in VGAGRAPH, its place in raw-data-map's graphics)
    /// </summary>
    public Dictionary<int, string> ArtExtern { get; set; } = [];
}
