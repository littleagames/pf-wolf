using Wolf3D.Assets;
using Wolf3D.Fonts;

namespace Wolf3D.Managers;

/// <summary>
/// Finds fonts by name. Fonts registered here (the game pack's font definitions) come first;
/// any other name is looked up as a VGAGRAPH font chunk, so "SmallFont" and "LargeFont" work
/// without a definition.
/// </summary>
internal class FontManager
{
    public FontManager(Lazy<AssetManager> assetManager)
    {
        this.assetManager = assetManager;
    }

    private readonly Lazy<AssetManager> assetManager;
    private readonly Dictionary<string, Font> fonts = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string name, Font font) => fonts[name] = font;

    public Font? Find(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        if (fonts.TryGetValue(name, out var font))
            return font;

        var asset = assetManager.Value.Find<FontAsset>(name);
        if (asset == null)
            return null;

        font = new VgaFont(asset);
        fonts[name] = font;
        return font;
    }
}
