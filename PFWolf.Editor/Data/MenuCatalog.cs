using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Loaders;
using PFWolf.Managers;
using PFWolf.Menus;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.Data;

/// <summary>A file that makes a menu: pfwolf.pk3's (which replaces the one before it) or a mod's (laid over it)</summary>
/// <param name="ModPath">The mod it's in, or null for pfwolf.pk3's own</param>
public sealed record MenuLayer(string SourceName, string? ModPath, string EntryPath)
{
    public bool IsBase => ModPath == null;
    public string Label => $"{SourceName}: {EntryPath}";
    public override string ToString() => Label;

    public bool Is(string? modPath, string entryPath)
        => (ModPath == null ? modPath == null : modPath != null && SamePath(ModPath, modPath))
           && EntryPath.Equals(entryPath, StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string a, string b)
        => string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
}

/// <summary>Something that opens a menu: the engine, game-info, or another menu's item (Menu: that menu, to go to)</summary>
public sealed record MenuUse(string Label, string? Menu = null)
{
    public override string ToString() => Label;
}

/// <summary>
/// A menu as the running game pack has it: by the name the game opens it by, from the asset it
/// loads (the pack's own, menudefs/{pack}/NAME, or the shared one), the files that make it, and
/// what opens it
/// </summary>
/// <param name="Reached">Whether the game can get to it: from the main menu (or the jukebox key, or game-info) by the items each menu has in this pack</param>
public sealed record MenuEntry(string Name, string AssetName, IReadOnlyList<MenuLayer> Layers, IReadOnlyList<MenuUse> Uses, bool Reached = true)
{
    /// <summary>menudefs/{pack}/NAME: the running pack's own, in place of the shared one</summary>
    public bool IsPackOwn => AssetName.Contains('/');

    /// <summary>A mod adds it or changes it</summary>
    public bool FromMod => Layers.Any(layer => !layer.IsBase);

    /// <summary>A mod adds it: pfwolf.pk3 hasn't one by this name</summary>
    public bool IsModsOwn => Layers.Count > 0 && Layers.All(layer => !layer.IsBase);

    public override string ToString() => Name;
}

/// <summary>A menu put together from its files: the document, the asset and model it reads as, or why it doesn't</summary>
public sealed record MenuBuild(YamlMappingNode? Tree, MenuAsset? Asset, MenuMetadata? Menu, string? Error, IReadOnlyList<string> Warnings);

/// <summary>
/// Every menudefs menu of the loaded game, as the game finds them (AssetManager.GetMenu), the files
/// each is made from (merged again as the loader merges them, so a file's unsaved text can be
/// tried), what opens each, and what's wrong with them as the game would say
/// </summary>
public static class MenuCatalog
{
    public static List<MenuEntry> Build(GameContent content)
    {
        var assets = content.Assets;
        var names = assets.NamesOf<MenuAsset>()
            .Select(name => name.Contains('/') ? name[(name.LastIndexOf('/') + 1)..] : name)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var entries = new List<MenuEntry>();
        foreach (var name in names)
        {
            var assetName = AssetNameOf(content, name);
            if (assetName == null)
                continue;      // another pack's own, which this one doesn't use
            entries.Add(new MenuEntry(name, assetName, Layers(content, assetName), []));
        }

        // What opens each: the engine, game-info, other menus' CP_Menu items
        var uses = Uses(content, entries);
        var reached = Reached(content, entries);
        return [.. entries.Select(entry => entry with { Uses = uses.GetValueOrDefault(entry.Name) ?? [], Reached = reached.Contains(entry.Name) })
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The asset the game reads a menu from (its GetMenu): the running pack's own, else the shared one; null for neither</summary>
    public static string? AssetNameOf(GameContent content, string name)
    {
        var lower = name.ToLowerInvariant();
        var packName = $"{content.PackId}/{lower}".ToLowerInvariant();
        if (content.Assets.Exists<MenuAsset>(packName))
            return packName;
        return content.Assets.Exists<MenuAsset>(lower) ? lower : null;
    }

    /// <summary>The files the loader read into a menu's asset, in order</summary>
    public static List<MenuLayer> Layers(GameContent content, string assetName)
    {
        var basePk3Name = AssetManager.BasePk3FileName;
        var mods = content.Assets.LoadedMods;
        return [.. content.Assets.FindAssetOrigins(assetName)
            .Where(asset => asset.Type == nameof(MenuAsset))
            .SelectMany(asset => asset.Origins)
            .Where(origin => origin.Action != AssetOrigin.LeftOut)
            .Select(origin =>
            {
                var mod = mods.FirstOrDefault(mod => mod.Source.Name.Equals(origin.Source, StringComparison.OrdinalIgnoreCase));
                return mod == null
                    ? new MenuLayer(basePk3Name, null, origin.Path)
                    : new MenuLayer(Path.GetFileName(mod.FullPath.TrimEnd('\\', '/')), mod.FullPath, origin.Path);
            })];
    }

    /// <summary>A file's text as it is now (a mod's saved to since it loaded, or not); null when it can't be read</summary>
    public static string? ReadText(GameContent content, MenuLayer layer)
    {
        try
        {
            var source = layer.IsBase
                ? new Pk3AssetSource(Path.Combine(content.GameFolder, AssetManager.BasePk3FileName))
                : ModSource.TryOpen(layer.ModPath!, [])?.Source;
            return source == null ? null : YamlDataEntryLoader.ReadText(source.Open(layer.EntryPath)).TrimStart('﻿');
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or KeyNotFoundException or FileNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// A menu's files merged as the loader merges them: pfwolf.pk3's replaces the one before it, a
    /// mod's is laid over it (MenuAsset.MergeYaml), and one that doesn't read is left out. A
    /// file's text can be given in place of what's saved (<paramref name="textOf"/>: null keeps it).
    /// </summary>
    public static MenuBuild Merge(GameContent content, IEnumerable<MenuLayer> layers, Func<MenuLayer, string?>? textOf = null)
    {
        YamlMappingNode? tree = null;
        string? error = null;
        foreach (var layer in layers)
        {
            var text = textOf?.Invoke(layer) ?? ReadText(content, layer);
            if (text == null)
            {
                error = $"{layer.Label} can't be read";
                continue;
            }

            YamlMappingNode? fileTree;
            try
            {
                fileTree = YamlTree.Parse(text) ?? (YamlTree.IsEmptyDocument(text) ? new YamlMappingNode() : null);
                // A file that doesn't read as a menu is left out (the game leaves out a mod's)
                Read(YamlTree.ToText(YamlTree.WithoutMergeTags(fileTree ?? new YamlMappingNode())));
            }
            catch (Exception e) when (IsReadError(e))
            {
                error = $"{layer.Label}: {Describe(e)}";
                continue;
            }
            if (fileTree == null)
                continue;

            if (layer.IsBase || tree == null)
                tree = YamlTree.WithoutMergeTags(fileTree);
            else
            {
                var merged = YamlTree.Clone(tree);
                MenuAsset.MergeYaml(merged, fileTree, true);
                try
                {
                    Read(YamlTree.ToText(merged));
                    tree = merged;
                }
                catch (Exception e) when (IsReadError(e))
                {
                    error = $"{layer.Label}: {Describe(e)}";
                }
            }
        }

        if (tree == null)
            return new MenuBuild(null, null, null, error ?? "No file makes it", []);

        var warnings = new List<string>();
        var asset = Read(YamlTree.ToText(tree));
        var menu = MenuMetadata.BuildFromAsset(asset, warnings.Add);
        return new MenuBuild(tree, asset, menu, error, warnings);
    }

    private static string Describe(Exception e) => e is YamlDotNet.Core.YamlException yaml ? Definitions.DefinitionKinds.Describe(yaml) : e.Message;

    private static MenuAsset Read(string yaml) => YamlDataEntryLoader.Deserialize<MenuAsset>(yaml);

    private static bool IsReadError(Exception e)
        => e is YamlDotNet.Core.YamlException or InvalidOperationException or FormatException or ArgumentException
            or InvalidCastException or NullReferenceException or KeyNotFoundException;

    /// <summary>A menu as the game has it (its files as they're saved)</summary>
    public static MenuBuild Merge(GameContent content, MenuEntry entry) => Merge(content, entry.Layers);

    // The menus the game can get to: from the engine's own, by each menu's items in the running pack
    private static HashSet<string> Reached(GameContent content, List<MenuEntry> entries)
    {
        var byName = entries.ToDictionary(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
        var reached = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(MenuNames.RootMenus);
        if (content.GameInfo?.HighScores is { } scores)
            foreach (var frame in new[] { scores.Frame, scores.EntryFrame }.OfType<string>())
                queue.Enqueue(frame);
        while (queue.Count > 0)
        {
            var name = queue.Dequeue().Trim();
            if (!byName.TryGetValue(name, out var entry) || !reached.Add(name))
                continue;
            foreach (var item in content.Find<MenuAsset>(entry.AssetName)?.MenuItems ?? [])
            {
                if (!InPack(content, item.GamePacks) || string.IsNullOrEmpty(item.Action))
                    continue;
                if (item.Action == MenuNames.SubMenuAction && !string.IsNullOrWhiteSpace(item.Menu))
                    queue.Enqueue(item.Menu);
                else if (MenuNames.ActionMenus.TryGetValue(item.Action, out var opened))
                    foreach (var menu in opened)
                        queue.Enqueue(menu);
            }
        }
        return reached;
    }

    // What opens each menu, by name
    private static Dictionary<string, List<MenuUse>> Uses(GameContent content, List<MenuEntry> entries)
    {
        var uses = new Dictionary<string, List<MenuUse>>(StringComparer.OrdinalIgnoreCase);
        void Add(string? name, MenuUse use)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            if (!uses.TryGetValue(name.Trim(), out var list))
                uses[name.Trim()] = list = [];
            if (!list.Contains(use))
                list.Add(use);
        }

        foreach (var (name, what) in MenuNames.EngineUses)
            Add(name, new MenuUse(what));
        if (content.GameInfo?.HighScores is { } scores)
        {
            Add(scores.Frame, new MenuUse("game-info high-scores frame: drawn as the high scores' frame"));
            Add(scores.EntryFrame, new MenuUse("game-info high-scores entry-frame: drawn while a new score's name is typed"));
        }

        foreach (var entry in entries)
        {
            var asset = content.Assets.Find<MenuAsset>(entry.AssetName);
            foreach (var item in asset?.MenuItems ?? [])
                if (item.Action == MenuNames.SubMenuAction)
                    Add(item.Menu, new MenuUse($"{entry.Name}: its \"{Shown(content, item.Text)}\" item (CP_Menu)", entry.Name));
        }
        return uses;
    }

    /// <summary>A text as the game shows it: a $language key's string</summary>
    public static string Shown(GameContent content, string? text) => LanguageStrings.Translate(text, Strings(content));

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameContent, Dictionary<string, string>> StringCaches = new();

    /// <summary>The game's en-us strings, read once per game loaded</summary>
    public static Dictionary<string, string> Strings(GameContent content)
        => StringCaches.GetValue(content, c => LanguageStrings.Load(c.Assets, "en-us") ?? []);

    /// <summary>
    /// What's wrong with a menu as the game would say (or quietly do): what didn't build, unknown
    /// actions, items-sources and component ids, what it names that the game hasn't
    /// </summary>
    public static List<string> Problems(GameContent content, MenuBuild build, IReadOnlyCollection<string> menuNames)
    {
        var problems = new List<string>();
        if (build.Error != null)
            problems.Add(build.Error);
        problems.AddRange(build.Warnings);
        if (build.Menu is not { } menu || build.Asset is not { } asset)
            return problems;

        if (menu.ItemsSource != null)
        {
            if (!MenuNames.ItemsSources.ContainsKey(menu.ItemsSource))
                problems.Add($"items-source '{menu.ItemsSource}' isn't one the game has ({string.Join(", ", MenuNames.ItemsSources.Keys)})");
            if (menu.MenuItems.Count > 0)
                problems.Add($"It has an items-source, so its menu-items are left out");
        }

        foreach (var item in menu.MenuItems.OfType<MenuSwitcher>())
        {
            var label = $"Item '{item.Id ?? Shown(content, item.Text)}'";
            switch (item.Action)
            {
                case null or "":
                    break;
                case MenuNames.SubMenuAction:
                    if (string.IsNullOrWhiteSpace(item.Menu))
                        problems.Add($"{label} is a CP_Menu without a menu");
                    else if (!menuNames.Contains(item.Menu.Trim()))
                        problems.Add($"{label} opens menu '{item.Menu}', which there isn't");
                    break;
                case MenuNames.ArticleAction:
                    if (string.IsNullOrWhiteSpace(item.Article))
                        problems.Add($"{label} is a CP_Article without an article");
                    else if (content.Find<TextAsset>(item.Article.Trim()) == null)
                        problems.Add($"{label} shows text '{item.Article}', which there isn't");
                    break;
                case MenuNames.ScriptAction:
                    if (string.IsNullOrWhiteSpace(item.Function))
                        problems.Add($"{label} is a CP_Lua without a function");
                    break;
                case MenuNames.TextScreenAction:
                    if (string.IsNullOrWhiteSpace(item.Script))
                        problems.Add($"{label} is a CP_TextScreen without a script");
                    break;
                default:
                    if (!MenuNames.Actions.ContainsKey(item.Action))
                        problems.Add($"{label}'s action '{item.Action}' isn't one the game has");
                    break;
            }
        }

        if (menu.DefaultSelection is int selection && menu.ItemsSource == null)
        {
            int count = menu.MenuItems.Count(item => InPack(content, item.GamePacks));
            if (selection < 0 || selection >= count)
                problems.Add($"default-selection {selection} is out of range (0-{count - 1})");
        }

        if (menu.FitItems is { } fit)
        {
            var ids = menu.Components.Select(c => c.Id).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var id in fit.Grow.Concat(fit.Move).Where(id => !ids.Contains(id)))
                problems.Add($"fit-items names component '{id}', which the menu hasn't (give a component that id)");
        }

        foreach (var graphic in menu.Components.OfType<Graphic>())
            if (!string.IsNullOrEmpty(graphic.Name) && content.Find<GraphicAsset>(graphic.Name) == null)
                problems.Add($"There's no picture {graphic.Name} (a Graphic)");

        var fonts = FontNames(content);
        foreach (var font in new[] { menu.Font }.Concat(menu.Components.OfType<Label>().Select(label => label.Font))
                     .Concat([menu.StatsPanel?.Font]).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
            if (font.Length > 0 && !fonts.Contains(font))
                problems.Add($"There's no font {font}");

        // game-info menu-music plays in place of every menu's own
        if (!string.IsNullOrEmpty(menu.Music) && string.IsNullOrEmpty(content.GameInfo?.MenuMusic)
            && !content.Assets.Exists<MusicFileAsset>(menu.Music) && content.Find<Wolf3dImfAudio>(menu.Music) == null)
            problems.Add($"There's no music {menu.Music}");

        return problems;
    }

    /// <summary>Whether something with this game-packs list is in the running pack (by its own, its base packs' and its menus-like pack's names)</summary>
    public static bool InPack(GameContent content, List<string>? gamePacks)
        => GamePackList.Includes(gamePacks, content.PackId, content.Assets.MenuPackIds);

    // Every font name the game finds: fonts.yaml's and the font assets'
    private static HashSet<string> FontNames(GameContent content)
        => new(FontCatalog.Definitions(content).Keys.Concat(FontCatalog.FontAssetNames(content)), StringComparer.OrdinalIgnoreCase);
}
