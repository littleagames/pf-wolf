namespace PFWolf.Assets;

/// <summary>
/// Named colors for a game pack (gamepacks/{pack}/colors.yaml), e.g. BORDCOLOR -> #890000
/// </summary>
public record ColorThemeAsset : Asset
{
    public ColorThemeAsset()
    {
    }

    public ColorThemeAsset(Dictionary<string, string> hexColors)
    {
        foreach (var (name, hex) in hexColors)
            Colors[name] = Color.FromHexRGBA(hex);
    }

    public Dictionary<string, Color> Colors { get; set; } = [];
}
