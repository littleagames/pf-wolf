using PFWolf.Assets;
using PFWolf.Managers;

namespace PFWolf.Menus;

/// <summary>
/// The $NAME text keys menus, messages and labels can use in place of their text: language/{lang}
/// shared by every game pack, then the running pack's gamepacks/{pack}/language/{lang}, whose
/// strings win. Shared by the game (its GetText) and the editor's previews.
/// </summary>
public static class LanguageStrings
{
    /// <summary>The confirm keys messages name ({YesButtonName}, {NoButtonName})</summary>
    public const string YesButtonName = "Y", NoButtonName = "N";

    /// <summary>The strings of a language as the running pack has them, or null with none</summary>
    public static Dictionary<string, string>? Load(AssetManager assets, string language)
    {
        var name = language.ToLowerInvariant();
        Dictionary<string, string>? strings = null;
        foreach (var asset in new[] { assets.Find<LanguageAsset>($"language/{name}"), assets.FindInGamePackIfAny<LanguageAsset>($"language/{name}") })
        {
            if (asset == null)
                continue;
            strings ??= [];
            foreach (var (key, text) in asset.Strings)
                strings[key] = text;
        }
        return strings;
    }

    /// <summary>
    /// A $NAME key's string (anything else is used as it is), with the {YesButtonName} /
    /// {NoButtonName} placeholders filled in
    /// </summary>
    public static string Translate(string? text, IReadOnlyDictionary<string, string>? strings)
    {
        text ??= string.Empty;
        if (strings != null && text.StartsWith('$') && strings.TryGetValue(text, out var result))
            text = result;

        return text
            .Replace("{YESBUTTONNAME}", YesButtonName)
            .Replace("{NOBUTTONNAME}", NoButtonName);
    }
}
