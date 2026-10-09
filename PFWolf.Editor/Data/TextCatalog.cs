using PFWolf.Assets;
using PFWolf.Managers;

namespace PFWolf.Editor.Data;

/// <summary>How a text is written, so how it can be shown</summary>
public enum TextFormat
{
    /// <summary>A Wolf3D article (^P pages, ^E at the end): the help screens and the episode end texts</summary>
    Article,
    /// <summary>A Blake Stone presenter script (Program.TextPresenter): briefings and the instructions</summary>
    Presenter,
    /// <summary>Anything else a data file's text chunk holds</summary>
    Other,
}

/// <summary>A text in the game's data files, where it came from and what shows it</summary>
public sealed record TextEntry(string Name, TextFormat Format, IReadOnlyList<string> Origins, IReadOnlyList<string> Uses)
{
    /// <summary>The file or pack it comes from, for the list's tooltip</summary>
    public string Source { get; init; } = "";

    public override string ToString() => Name;
}

/// <summary>
/// Every text the game's data files hold (VGAGRAPH's HELPART and ENDARTn, Blake Stone's
/// briefings), with what in game-info shows each, and what lays an article out: the small font,
/// the pictures it names by number, and the theme's colors
/// </summary>
public static class TextCatalog
{
    /// <summary>The font articles are printed in (Program.SMALL_FONT)</summary>
    public const string ArticleFont = "SmallFont";

    public static string FormatName(TextFormat format) => format switch
    {
        TextFormat.Article => "Article",
        TextFormat.Presenter => "Presenter script",
        _ => "Text",
    };

    public static bool IsBlake(GameContent content) => content.Game.Type is GameType.BlakeStone or GameType.PlanetStrike;

    public static List<TextEntry> Build(GameContent content)
    {
        var uses = Uses(content);
        var entries = new List<TextEntry>();
        var assets = content.Assets;
        foreach (var name in assets.AssetNames.Where(name => !name.Contains('/') && assets.Exists<TextAsset>(name)).Order(StringComparer.OrdinalIgnoreCase))
        {
            var text = Read(content, name) ?? "";
            var format = ArticleLayout.IsArticle(text) ? TextFormat.Article
                : IsBlake(content) ? TextFormat.Presenter
                : TextFormat.Other;
            var origins = assets.FindAssetOrigins(name)
                .Where(asset => asset.Type == nameof(TextAsset))
                .SelectMany(asset => asset.Origins)
                .ToList();
            var display = name.ToUpperInvariant();
            entries.Add(new TextEntry(display, format,
                origins.Select(origin => $"{origin.Action} by {origin.Source}: {origin.Path}").ToList(),
                uses.GetValueOrDefault(display, []))
            {
                Source = origins.LastOrDefault(origin => origin.Action != PFWolf.Loaders.AssetOrigin.LeftOut)?.Source ?? "",
            });
        }
        return entries;
    }

    /// <summary>A text as the game reads it, or null when there's none of that name</summary>
    public static string? Read(GameContent content, string name) => content.Find<TextAsset>(name)?.ToText();

    /// <summary>
    /// Writes a text to texts/name.txt in the mod (a folder or a pk3), where the game reads it in
    /// place of the text of that name, or as a new one. Returns where it went.
    /// </summary>
    public static string Save(string modPath, string name, string text)
        => Editing.ModFiles.Write(modPath, ($"texts/{name.ToLowerInvariant()}.txt", System.Text.Encoding.ASCII.GetBytes(text)));

    /// <summary>What's wrong with a name for a new text, or null when it will do</summary>
    public static string? CheckNewName(GameContent content, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Give it a name";
        if (!name.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-'))
            return "Letters, digits, - and _ only: it's the file name in texts/";
        if (content.Assets.Exists<TextAsset>(name))
            return $"There's a text called {name.ToUpperInvariant()} already: pick it in the list to change it";
        return null;
    }

    /// <summary>A two-page article to start a new text from</summary>
    public const string NewArticle = "^P\r\nThe first page's text goes here.\r\n^P\r\nAnd the next page's.\r\n^E\r\n";

    /// <summary>A Blake Stone presenter script to start a new text from</summary>
    public const string NewPresenterScript = "The text goes here.\r\n^XX\r\n";

    /// <summary>What shows each text, by its upper-case name: the help screens, game-info's clusters and episodes</summary>
    public static Dictionary<string, List<string>> Uses(GameContent content)
    {
        var uses = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        void Add(string? name, string label)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            if (!uses.TryGetValue(name, out var list))
                uses[name] = list = [];
            if (!list.Contains(label))
                list.Add(label);
        }

        var gameInfo = content.GameInfo;
        if (gameInfo?.HelpText is { Length: > 0 } helpText)
            Add(helpText, "The instructions (F1 and the menu), in the briefing window");
        else
            Add("HELPART", "Read This! (F1 and the menu's help screens)");

        if (gameInfo != null)
        {
            foreach (var (number, cluster) in gameInfo.Clusters.OrderBy(cluster => cluster.Key))
                Add(cluster.EndText, $"Cluster {number}'s end text, after its victory tally");
            foreach (var (key, episode) in gameInfo.Episodes)
            {
                var label = string.IsNullOrWhiteSpace(episode.Name) || episode.Name.StartsWith('$') ? $"Episode {key}" : episode.Name;
                Add(episode.Briefing, $"{label}'s briefing");
                Add(episode.EndBriefing, $"{label}'s end briefing");
            }
        }
        return uses;
    }

    /// <summary>
    /// What the game lays articles out with: the small font's widths (fonts.yaml's SmallFont, or
    /// the VGAGRAPH chunk of that name), alias.yaml's art-extern pictures, and the theme's Black
    /// and Dark Yellow. Says in <paramref name="problem"/> when the font can't be drawn here.
    /// </summary>
    public static ArticleArt Art(GameContent content, out FontAsset? font, out string? problem)
    {
        font = Font(content, ArticleFont, out problem);
        var alias = content.Assets.Exists<AliasAsset>($"{content.PackId}/alias")
            ? content.Assets.FindInGamePack<AliasAsset>("alias")
            : null;
        var theme = Theme(content);

        return new ArticleArt
        {
            FontWidths = font?.Width,
            PictureName = number => alias?.ArtExtern.GetValueOrDefault(number),
            PictureSize = name => content.Find<GraphicAsset>(name) is { } picture ? (picture.Width, picture.Height) : null,
            TextColor = ThemeColor(content, theme, "Black"),
            PageNumberColor = ThemeColor(content, theme, "Dark Yellow"),
        };
    }

    /// <summary>The background articles are drawn on (the theme's BACKCOLOR)</summary>
    public static byte BackColor(GameContent content) => ThemeColor(content, Theme(content), "BACKCOLOR");

    private static ColorThemeAsset? Theme(GameContent content)
        => content.Assets.Exists<ColorThemeAsset>($"{content.PackId}/colors")
            ? content.Assets.FindInGamePack<ColorThemeAsset>("colors")
            : null;

    /// <summary>
    /// A Wolf3D font by name, as the game's FontManager finds it: a "vga" entry in the pack's
    /// fonts.yaml (its source chunk, upper case only if it says so), else the chunk of that name.
    /// Graphic and sheet fonts aren't drawn by the editor: the chunk of the name is tried instead.
    /// </summary>
    public static FontAsset? Font(GameContent content, string name, out string? problem)
    {
        problem = null;
        var source = name;
        var upperCase = false;
        if (content.Assets.Exists<FontDefinitionsAsset>($"{content.PackId}/fonts")
            && content.Assets.FindInGamePack<FontDefinitionsAsset>("fonts")?.Fonts.GetValueOrDefault(name) is { } definition)
        {
            if (definition.Type.Equals("vga", StringComparison.OrdinalIgnoreCase))
            {
                source = string.IsNullOrEmpty(definition.Source) ? name : definition.Source;
                upperCase = definition.UpperCase;
            }
            else
                problem = $"{name} is a {definition.Type} font in fonts.yaml, which the preview can't draw: it uses the {name} chunk";
        }

        var font = content.Find<FontAsset>(source);
        if (font == null)
        {
            problem = $"There's no {source} font to print the text in";
            return null;
        }
        if (upperCase)
        {
            var location = (short[])font.Location.Clone();
            var width = (byte[])font.Width.Clone();
            for (char ch = 'a'; ch <= 'z'; ch++)
            {
                location[ch] = location[char.ToUpperInvariant(ch)];
                width[ch] = width[char.ToUpperInvariant(ch)];
            }
            font = font with { Location = location, Width = width };
        }
        return font;
    }

    // A theme color as the palette index nearest it, as the game's VideoManager picks it
    private static byte ThemeColor(GameContent content, ColorThemeAsset? theme, string name)
    {
        if (theme?.Colors.GetValueOrDefault(name) is not { } color)
            return 0;
        return Nearest(content.Palette, color.Red, color.Green, color.Blue);
    }

    public static byte Nearest(IReadOnlyList<PaletteColor> palette, byte red, byte green, byte blue)
    {
        byte closest = 0;
        int closestDistance = int.MaxValue;
        for (int i = 0; i < palette.Count && i < 256; i++)
        {
            int dr = red - palette[i].Red, dg = green - palette[i].Green, db = blue - palette[i].Blue;
            int distance = dr * dr + dg * dg + db * db;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = (byte)i;
                if (distance == 0)
                    break;
            }
        }
        return closest;
    }
}
