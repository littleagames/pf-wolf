using PFWolf.Assets;
using PFWolf.Fonts;

namespace PFWolf.Managers;

/// <summary>
/// Finds fonts by name: the game pack's font definitions (gamepacks/{pack}/fonts.yaml) first,
/// then any font registered in code, then a Wolf3D font of that name (a VGAGRAPH font chunk or
/// a file in the pk3's fonts/ folder), so "SmallFont" and "LargeFont" work without a
/// definition. Each font is built once, the first time it's used, by <see cref="FontBuilder"/>.
/// </summary>
internal class FontManager
{
    public FontManager(Lazy<AssetManager> assetManager, VideoManager videoManager)
    {
        this.assetManager = assetManager;
        builder = new Lazy<FontBuilder>(() => new FontBuilder(
            source => assetManager.Value.Find<FontAsset>(source),
            picName => assetManager.Value.Exists<GraphicAsset>(picName) ? assetManager.Value.Find<GraphicAsset>(picName) : null,
            videoManager.ResolveColor,
            Console.WriteLine));
    }

    private readonly Lazy<AssetManager> assetManager;
    private readonly Lazy<FontBuilder> builder;
    private readonly Dictionary<string, Font> fonts = new(StringComparer.OrdinalIgnoreCase);
    private FontDefinitionsAsset? definitions;
    private bool definitionsRead;

    public void Register(string name, Font font) => fonts[name] = font;

    public Font? Find(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        if (fonts.TryGetValue(name, out var font))
            return font;

        FontDefinition? definition = null;
        Definitions?.Fonts.TryGetValue(name, out definition);
        font = builder.Value.Build(name, definition);
        if (font == null)
            return null;

        fonts[name] = font;
        return font;
    }

    private FontDefinitionsAsset? Definitions
    {
        get
        {
            if (!definitionsRead)
            {
                definitionsRead = true;
                if (assetManager.Value.Exists<FontDefinitionsAsset>($"{GamePackId}/fonts"))
                    definitions = assetManager.Value.FindInGamePack<FontDefinitionsAsset>("fonts");
            }
            return definitions;
        }
    }

    private string GamePackId => assetManager.Value.GamePackId;

    /// <summary>The character the typing cursor is drawn as (UiText.XORICursor)</summary>
    internal const char CursorChar = FontBuilder.CursorChar;
}
