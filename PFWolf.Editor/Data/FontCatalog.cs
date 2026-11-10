using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using PFWolf.Assets;
using PFWolf.Editor.Definitions;
using PFWolf.Fonts;
using PFWolf.Loaders;
using PFWolf.Managers;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.Data;

/// <summary>Where Go to takes a font's use: an entry in the Definitions window, or a line of a script</summary>
public sealed record FontUseTarget(DefinitionKind? Kind = null, IReadOnlyList<string>? Path = null, string? Script = null, int Line = 0);

/// <summary>One place that prints in a font, or builds a font from it</summary>
public sealed record FontUse(string Label, FontUseTarget? Target = null)
{
    public override string ToString() => Label;
}

/// <summary>A fonts.yaml file that defines a font, and the font's lines in it</summary>
public sealed record FontDefinitionSite(string File, string Yaml);

/// <summary>
/// A font the game can print in: a fonts.yaml definition (as the files merge, the last one
/// whole), or a Wolf3D font asset with none (a VGAGRAPH chunk such as Blake Stone's Font1, or
/// a file in a pk3's fonts/ folder)
/// </summary>
/// <param name="Type">vga, graphic or sheet (or what the definition says, when it's none of them)</param>
/// <param name="SourceName">A vga font's font asset; null for the others</param>
/// <param name="SourceOrigins">Where the vga font's asset comes from, last one used</param>
public sealed record FontEntry(string Name, string Type, FontDefinition? Definition, IReadOnlyList<FontDefinitionSite> DefinedIn,
    string? SourceName, IReadOnlyList<AssetOrigin> SourceOrigins, IReadOnlyList<FontUse> Uses)
{
    public bool IsDefined => Definition != null;

    public override string ToString() => Name;
}

/// <summary>
/// Every font of the loaded game, as the game finds them (<see cref="FontBuilder"/>), where each
/// is defined and what prints in it
/// </summary>
public static partial class FontCatalog
{
    public static List<FontEntry> Build(GameContent content)
    {
        var definitions = Definitions(content);
        var sites = DefinitionSites(content);
        var uses = Uses(content, definitions);

        var entries = new List<FontEntry>();
        foreach (var (name, definition) in definitions.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var type = string.IsNullOrEmpty(definition.Type) ? "vga" : definition.Type.ToLowerInvariant();
            var source = type == "vga" ? (string.IsNullOrEmpty(definition.Source) ? name : definition.Source) : null;
            entries.Add(new FontEntry(name, type, definition, sites.GetValueOrDefault(name) ?? [], source,
                source != null ? FontOrigins(content, source) : [], uses.GetValueOrDefault(name) ?? []));
        }

        // Font assets nothing defines under their own name: a font by that name is still there
        foreach (var name in FontAssetNames(content).Where(name => !definitions.ContainsKey(name)))
            entries.Add(new FontEntry(name, "vga", null, [], name, FontOrigins(content, name), uses.GetValueOrDefault(name) ?? []));

        return [.. entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The game pack's font definitions as the game merged them</summary>
    public static IReadOnlyDictionary<string, FontDefinition> Definitions(GameContent content)
        => content.Assets.Exists<FontDefinitionsAsset>($"{content.PackId}/fonts")
            ? content.Assets.FindInGamePack<FontDefinitionsAsset>("fonts")?.Fonts ?? []
            : new Dictionary<string, FontDefinition>();

    /// <summary>The Wolf3D font assets (VGAGRAPH chunks, fonts/ files), by the names the game finds them by</summary>
    public static List<string> FontAssetNames(GameContent content)
        => [.. content.Assets.AssetNames.Where(name => !name.Contains('/') && content.Assets.Exists<FontAsset>(name))
            .Select(name => ProperName(content, name))];

    // Asset names are kept lower case: the raw-data-map's or the file's own spelling where there is one
    private static string ProperName(GameContent content, string name)
    {
        foreach (var known in KnownChunkNames)
            if (known.Equals(name, StringComparison.OrdinalIgnoreCase))
                return known;
        var origin = FontOrigins(content, name).LastOrDefault(origin => origin.Action != AssetOrigin.LeftOut);
        if (origin != null)
        {
            var file = Path.GetFileNameWithoutExtension(origin.Path.Replace('\\', '/').Split('/')[^1]);
            if (file.Equals(name, StringComparison.OrdinalIgnoreCase))
                return file;
        }
        return name;
    }

    private static readonly string[] KnownChunkNames = [FontNames.Small, FontNames.Large, "Font1", "Font2", "Font3", "Font4", "Font5"];

    public static IReadOnlyList<AssetOrigin> FontOrigins(GameContent content, string name)
        => [.. content.Assets.FindAssetOrigins(name).Where(asset => asset.Type == nameof(FontAsset)).SelectMany(asset => asset.Origins)];

    /// <summary>
    /// A font built as the game builds it, with what's wrong building it (missing pictures, an
    /// unknown type); colors resolve through <paramref name="colors"/>
    /// </summary>
    public static Font? BuildFont(GameContent content, FontEntry entry, ITextCanvas colors, List<string> problems)
    {
        var builder = new FontBuilder(content.Find<FontAsset>,
            name => content.Assets.Exists<GraphicAsset>(name) ? content.Find<GraphicAsset>(name) : null,
            colors.ResolveColor, problems.Add);
        var font = builder.Build(entry.Name, entry.Definition);
        if (font == null && entry.SourceName != null && content.Find<FontAsset>(entry.SourceName) == null)
            problems.Add($"There's no font {entry.SourceName} (a VGAGRAPH font chunk or a fonts/ file) for it to be made from");
        return font;
    }

    /// <summary>What's wrong with a name for a new font (none, a space or a colon in it, taken), or null when it'll do</summary>
    public static string? NameProblem(string candidate, Func<string, bool> taken)
    {
        var name = candidate.Trim();
        if (name.Length == 0 || name.Any(c => char.IsWhiteSpace(c) || c is ':' or '/' or '\\' or '#' or '"' or '\''))
            return "A name without spaces, colons, slashes, quotes or #";
        return taken(name) ? $"There's a font {name} already" : null;
    }

    /// <summary>What's odd about a font that the game won't say: Blake Stone's Font1 can't print plain text</summary>
    public static IEnumerable<string> Notes(FontEntry entry)
    {
        if (entry.SourceName?.Equals("Font1", StringComparison.OrdinalIgnoreCase) == true)
            yield return "Blake Stone's Font1 has its glyphs 31 codes below their characters (code 32 is \"?\", 34 is \"A\"), so it can't print plain text";
        if (entry.Definition is { } definition && !string.IsNullOrEmpty(definition.Type)
            && definition.Type.ToLowerInvariant() is not ("vga" or "graphic" or "sheet"))
            yield return $"Its type '{definition.Type}' isn't vga, graphic or sheet, so the game can't print in it";
    }

    //
    // Where each font is defined
    //

    // Each font name's fonts.yaml files in load order, with its lines in each
    private static Dictionary<string, List<FontDefinitionSite>> DefinitionSites(GameContent content)
    {
        var sites = new Dictionary<string, List<FontDefinitionSite>>(StringComparer.OrdinalIgnoreCase);
        var basePk3 = new Pk3AssetSource(Path.Combine(content.GameFolder, AssetManager.BasePk3FileName));
        var origins = content.Assets.FindAssetOrigins($"{content.PackId}/fonts")
            .Where(asset => asset.Type == nameof(FontDefinitionsAsset))
            .SelectMany(asset => asset.Origins)
            .Where(origin => origin.Action != AssetOrigin.LeftOut);
        foreach (var origin in origins)
        {
            DefinitionFile? file = null;
            if (origin.Source.Equals(basePk3.Name, StringComparison.OrdinalIgnoreCase))
                file = DefinitionFile.Read(basePk3, null, origin.Path);
            else if (content.Assets.LoadedMods.FirstOrDefault(mod => mod.Source.Name.Equals(origin.Source, StringComparison.OrdinalIgnoreCase)) is { } mod)
                file = DefinitionFile.Read(mod.Source, mod.FullPath, origin.Path);
            if (file?.Tree is not { } tree)
                continue;

            var keys = tree.Children.Keys.OfType<YamlScalarNode>().ToList();
            var lines = file.Text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < keys.Count; i++)
            {
                var name = keys[i].Value ?? "";
                int start = (int)keys[i].Start.Line - 1;
                int end = i + 1 < keys.Count ? (int)keys[i + 1].Start.Line - 1 : lines.Length;
                // Comments and blank lines before the next font are about it, not this one
                while (end > start + 1 && (string.IsNullOrWhiteSpace(lines[end - 1]) || lines[end - 1].TrimStart().StartsWith('#')))
                    end--;
                var yaml = string.Join("\n", lines[start..end]);
                if (!sites.TryGetValue(name, out var list))
                    sites[name] = list = [];
                list.Add(new FontDefinitionSite(file.Label, yaml));
            }
        }
        return sites;
    }

    //
    // What prints in each font
    //

    private static Dictionary<string, List<FontUse>> Uses(GameContent content, IReadOnlyDictionary<string, FontDefinition> definitions)
    {
        var uses = new Dictionary<string, List<FontUse>>(StringComparer.OrdinalIgnoreCase);
        void Add(string font, FontUse use)
        {
            if (string.IsNullOrWhiteSpace(font))
                return;
            if (!uses.TryGetValue(font, out var list))
                uses[font] = list = [];
            if (!list.Contains(use))
                list.Add(use);
        }

        foreach (var (font, label) in FontNames.EngineUses)
            Add(font, new FontUse(label));

        // fonts.yaml: a vga font made from another font's asset
        foreach (var (name, definition) in definitions)
        {
            if ((string.IsNullOrEmpty(definition.Type) || definition.Type.Equals("vga", StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrEmpty(definition.Source) && !definition.Source.Equals(name, StringComparison.OrdinalIgnoreCase))
                Add(definition.Source, new FontUse($"fonts.yaml: {name} is made from it"));
        }

        var assets = content.Assets;
        void Scan(object? asset, string file, DefinitionKind? kind)
        {
            foreach (var (path, font) in FontReferences(asset))
                Add(font, new FontUse($"{file}: {string.Join(" › ", path.Select(key => int.TryParse(key, out int index) ? $"#{index + 1}" : key))}",
                    kind is { } k ? new FontUseTarget(k, path) : null));
        }

        Scan(content.GameInfo, "game-info", DefinitionKind.GameInfo);
        foreach (var name in new[] { "hud-messages", "statusbar", "statusbar-fullscreen", "intermission", "presenter" })
        {
            Asset? asset = name switch
            {
                "hud-messages" => assets.FindInGamePackIfAny<HudMessageStylesAsset>(name),
                "intermission" => assets.FindInGamePackIfAny<IntermissionAsset>(name),
                "presenter" => assets.FindInGamePackIfAny<PresenterAsset>(name),
                _ => assets.FindInGamePackIfAny<StatusBarAsset>(name),
            };
            Scan(asset, name, name == "presenter" ? DefinitionKind.Presenter : null);
        }
        foreach (var menu in assets.GetMenuNames().Order(StringComparer.OrdinalIgnoreCase))
        {
            var asset = assets.Exists<MenuAsset>($"{content.PackId}/{menu}") ? assets.Find<MenuAsset>($"{content.PackId}/{menu}")
                : assets.Exists<MenuAsset>(menu) ? assets.Find<MenuAsset>(menu) : null;
            Scan(asset, $"menu {menu}", null);
        }

        // Lua: game.drawtext / game.textwidth naming a font in a string
        foreach (var script in ScriptCatalog.Build(content))
        {
            if (ScriptCatalog.Read(content, script.Name) is not { } text)
                continue;
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (Match call in TextCall().Matches(lines[i]))
                    foreach (Match literal in StringLiteral().Matches(call.Groups["args"].Value))
                    {
                        var value = literal.Groups["s"].Value;
                        if (definitions.ContainsKey(value) || FontNames.EngineUses.ContainsKey(value))
                            Add(value, new FontUse($"script {script.Name}, line {i + 1}: game.{call.Groups["fn"].Value}", new FontUseTarget(Script: script.Name, Line: i + 1)));
                    }
            }
        }
        return uses;
    }

    [GeneratedRegex(@"game\.(?<fn>drawtext|textwidth)\s*\((?<args>[^)]*)\)")]
    private static partial Regex TextCall();

    [GeneratedRegex(@"""(?<s>[^""]*)""|'(?<s>[^']*)'")]
    private static partial Regex StringLiteral();

    /// <summary>
    /// Every font a definition names, with its path of YAML keys: a string property whose name
    /// ends in Font (font, title-font...), each of a Fonts list, and a "font" entry of a map of
    /// strings (a menu component's params)
    /// </summary>
    public static List<(IReadOnlyList<string> Path, string Font)> FontReferences(object? asset)
    {
        var found = new List<(IReadOnlyList<string>, string)>();
        Walk(asset, [], found, 0);
        return found;
    }

    private static void Walk(object? value, List<string> path, List<(IReadOnlyList<string>, string)> found, int depth)
    {
        if (value == null || depth > 16)
            return;
        var type = value.GetType();
        if (value is string || value is byte[] || type.IsPrimitive || type.IsEnum || value is decimal)
            return;

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString() ?? "";
                if (entry.Value is string font && key.EndsWith("font", StringComparison.OrdinalIgnoreCase))
                    found.Add(([.. path, key], font));
                else
                    Walk(entry.Value, [.. path, key], found, depth + 1);
            }
            return;
        }
        if (value is IEnumerable list)
        {
            int index = 0;
            foreach (var item in list)
                Walk(item, [.. path, (index++).ToString(System.Globalization.CultureInfo.InvariantCulture)], found, depth + 1);
            return;
        }
        if (type.Namespace?.StartsWith("PFWolf", StringComparison.Ordinal) != true)
            return;

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || property.DeclaringType == typeof(Asset))
                continue;
            object? child;
            try
            {
                child = property.GetValue(value);
            }
            catch (TargetInvocationException)
            {
                continue;
            }
            var key = Kebab(property.Name);
            if (child is string font && property.Name.EndsWith("Font", StringComparison.Ordinal) && property.PropertyType == typeof(string))
                found.Add(([.. path, key], font));
            else if (property.Name == "Fonts" && child is IEnumerable<string> fonts)
            {
                int index = 0;
                foreach (var each in fonts)
                    found.Add(([.. path, key, (index++).ToString(System.Globalization.CultureInfo.InvariantCulture)], each));
            }
            else
                Walk(child, [.. path, key], found, depth + 1);
        }
    }

    /// <summary>A property's name as its YAML key: TitleFont → title-font</summary>
    public static string Kebab(string name)
    {
        var text = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
                text.Append('-');
            text.Append(char.ToLowerInvariant(name[i]));
        }
        return text.ToString();
    }
}
