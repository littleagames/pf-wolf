using System.Runtime.CompilerServices;
using PFWolf.Assets;

namespace PFWolf.Managers;

/// <summary>
/// What the game builds from the assets, which the editor doesn't need: menus, language text
/// and the actordefs classes. Each is built once per asset manager.
/// </summary>
internal static class AssetManagerExtensions
{
    // Menus are drawn every redraw; build each one once
    private static readonly ConditionalWeakTable<AssetManager, Dictionary<string, MenuMetadata>> MenuCaches = new();
    // Built once per language; menus look text up on every redraw
    private static readonly ConditionalWeakTable<AssetManager, Dictionary<string, LanguageMetadata?>> LanguageCaches = new();

    /// <summary>
    /// A menu by name: the running pack's own (menudefs/{pack}/name.yaml) when it has one, else
    /// the shared one
    /// </summary>
    [Obsolete("Temporary endpoint until the asset types are implemented")]
    public static MenuMetadata? GetMenu(this AssetManager assets, string name)
    {
        var menuCache = MenuCaches.GetOrCreateValue(assets);
        var normalizedName = name.ToLowerInvariant();
        if (menuCache.TryGetValue(normalizedName, out var cached))
            return cached;

        var packName = $"{assets.GamePackId}/{normalizedName}".ToLowerInvariant();
        var asset = assets.Exists<MenuAsset>(packName) ? assets.Find<MenuAsset>(packName) : assets.Find<MenuAsset>(normalizedName);
        if (asset == null)
            return null;

        // TODO: MenuManager?
        var menu = MenuMetadata.BuildFromAsset(asset);
        if (menu != null)
            menuCache[normalizedName] = menu;
        return menu;
    }

    /// <summary>
    /// Text for a language: language/{lang} shared by every game pack, then the running
    /// pack's gamepacks/{pack}/language/{lang}, whose strings win
    /// </summary>
    [Obsolete]
    public static LanguageMetadata? GetText(this AssetManager assets, string language)
    {
        var languageCache = LanguageCaches.GetOrCreateValue(assets);
        var normalizedName = language.ToLowerInvariant();
        if (languageCache.TryGetValue(normalizedName, out var cached))
            return cached;

        LanguageMetadata? metadata = null;
        var languageAssets = new[]
        {
            assets.Find<LanguageAsset>($"language/{normalizedName}"),
            assets.FindInGamePack<LanguageAsset>($"language/{normalizedName}"),
        };
        foreach (var asset in languageAssets)
        {
            if (asset == null)
                continue;

            metadata ??= new LanguageMetadata();
            foreach (var (key, text) in asset.Strings)
                metadata.TextStrings[key] = text;
        }

        languageCache[normalizedName] = metadata;
        return metadata;
    }

    [Obsolete]
    public static ActorMetadata GetActorMetadata(this AssetManager assets)
    {
        try
        {
            // Files directly in actordefs/ belong to every pack; the running pack's own
            // (actordefs/{pack}/) come after, so a class it defines replaces the shared one.
            var data = new ActorMetadata();
            foreach (var actors in new[] { assets.Find<ActorTranslationAsset>("actordefs"), assets.FindInGamePack<ActorTranslationAsset>("actordefs") })
            {
                if (actors != null)
                    data.AddActors(actors.Actors);
            }

            return data;
        }
        catch (Exception e)
        {
            Console.Write(e);
            throw;
        }
    }
}
