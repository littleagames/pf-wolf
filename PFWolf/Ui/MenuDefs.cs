using PFWolf.Assets;
using PFWolf.Extensions;
using PFWolf.Menus;

namespace PFWolf.Ui;

/// <summary>
/// The menus menudefs/ defines: their items and layout (LoadMenu), and their non-interactive
/// parts (DrawMenuComponents). What an item does is an action registered by name (the CP_
/// routines); a menu built from game data has an items-source, registered too unless it's
/// episodes or skills, which come from game-info.
/// </summary>
internal sealed class MenuDefs(AssetManager assets, GameEngineManager engine, GraphicManager graphics, AudioManager audio)
{
    readonly Dictionary<string, Func<int, int>> actions = new(StringComparer.Ordinal);
    readonly Dictionary<string, Func<ControlPanelItem[]>> sources = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A page of presenter text (Blake Stone's instructions, story and ordering info): the
    /// CP_TextScreen action, made for a menu item's script
    /// </summary>
    internal Func<string, Func<int, int>>? TextScreen { get; set; }

    internal const string TextScreenAction = MenuNames.TextScreenAction;

    // MENU HOOKS: what a mod's menu items can run besides the game's own CP_ actions --
    // CP_Menu (`menu:` another menudefs menu, as a submenu), CP_Article (`article:` a text shown
    // as an article, as a level's briefing is) and CP_Lua (`function:` a script's global function)
    internal const string SubMenuAction = MenuNames.SubMenuAction, ArticleAction = MenuNames.ArticleAction, ScriptAction = MenuNames.ScriptAction;

    /// <summary>Runs a menudefs menu as a submenu (CP_Menu)</summary>
    internal Func<string, int>? SubMenu { get; set; }

    /// <summary>Shows a text as an article (CP_Article)</summary>
    internal Func<string, int>? Article { get; set; }

    /// <summary>Calls a script's global function (CP_Lua)</summary>
    internal Func<string, int>? Script { get; set; }

    /// <summary>What a menu item with action: <paramref name="name"/> runs</summary>
    internal void RegisterAction(string name, Func<int, int> action) => actions[name] = action;

    /// <summary>The rows of a menu with items-source: <paramref name="name"/></summary>
    internal void RegisterSource(string name, Func<ControlPanelItem[]> rows) => sources[name] = rows;

    /// <summary>
    /// Builds the item list and layout for a menu defined in menudefs/
    /// </summary>
    internal (ControlPanelItem[] items, ControlPanelMenuInfo info) LoadMenu(string name, short curpos = 0)
    {
        var menuAsset = assets.GetMenu(name)
            ?? throw new InvalidOperationException($"Menu '{name}' not found in menudefs");
        var language = assets.GetText("en-us");

        ControlPanelItem[] items;
        if (menuAsset.ItemsSource != null)
        {
            if (menuAsset.MenuItems.Count > 0)
                Console.WriteLine($"Menu '{name}': has items-source '{menuAsset.ItemsSource}', so its menu-items are ignored");
            items = BuildSourceItems(name, menuAsset.ItemsSource);
        }
        else
            items = MenuRows.FromItems(menuAsset, InCurrentGamePack, text => text.ToLanguageText(language), item => MapFunction(name, item));

        var (info, fit) = MenuRows.Layout(name, menuAsset, items, curpos,
            pictures: items.Any(item => PicName(item.Data) is { Length: > 0 }), Console.WriteLine);
        fits[name] = fit;
        return (items, info);
    }

    // Each menu's fit-items, as LoadMenu last worked it out for its items (DrawMenuComponents)
    readonly Dictionary<string, MenuFit> fits = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The fit-items LoadMenu last worked out for a menu (none before it's loaded)</summary>
    internal MenuFit FitOf(string name) => fits.TryGetValue(name, out var fit) ? fit : default;

    // An items-source row's picture (game-info pic-name): its skill's, class's or episode's
    static string? PicName(object? data) => data switch
    {
        SkillInfo skill => skill.PicName,
        NewGameMenu.PlayerClassChoice playerClass => playerClass.PicName,
        EpisodeInfo episode => episode.PicName,
        _ => null,
    };

    /// <summary>
    /// Builds menu items from game data for a menu's items-source
    /// </summary>
    private ControlPanelItem[] BuildSourceItems(string menuName, string source)
    {
        var gameInfo = engine.GetGameInfo();
        var language = assets.GetText("en-us");

        switch (source.ToLowerInvariant())
        {
            case "episodes":
                // Episode names are two lines, so a blank row follows each one, unless the menu's
                // rows are tall enough for both (spacer-rows: false)
                if (assets.GetMenu(menuName)?.SpacerRows == false)
                    return gameInfo.Episodes.Values
                        .Select(ep => new ControlPanelItem(1, ep.Name.ToLanguageText(language), null, ep) { ShortKey = ep.Key })
                        .ToArray();
                return gameInfo.Episodes.Values.SelectMany(ep =>
                    new ControlPanelItem[]
                    {
                        new ControlPanelItem(1, ep.Name.ToLanguageText(language), null, ep) { ShortKey = ep.Key },
                        new ControlPanelItem(0, "", null)
                    }).SkipLast(1)
                    .ToArray();

            case "skills":
                return gameInfo.Skills.Values
                    .Select(skill => new ControlPanelItem(1, skill.Name.ToLanguageText(language), null, skill))
                    .ToArray();

            default:
                if (sources.TryGetValue(source, out var rows))
                    return rows();
                Console.WriteLine($"Menu '{menuName}': unknown items-source '{source}'");
                return [];
        }
    }

    /// <summary>
    /// Finds a menu item by its YAML id, or null if the menu has no such item (or isn't loaded yet)
    /// </summary>
    internal static ControlPanelItem? FindMenuItem(ControlPanelItem[] items, string id)
        => items.FirstOrDefault(item => item.Id == id);

    /// <summary>
    /// Id of the chosen item from HandleMenu's result, or null for escape / an item without an id
    /// </summary>
    internal static string? SelectedId(ControlPanelItem[] items, int which)
        => which >= 0 && which < items.Length ? items[which].Id : null;

    /// <summary>
    /// Draws the non-interactive parts of a menu (background, windows, graphics, labels)
    /// </summary>
    /// <param name="leaveOut">Ids of components not to draw this time (the Mods screen's catalog hint, with no catalog)</param>
    internal void DrawMenuComponents(string name, params string[] leaveOut)
    {
        var menu = assets.GetMenu(name);
        if (menu == null)
            return;

        StartMenuMusic(menu);
        DrawComponents(name, leaveOut);
    }

    /// <summary>A menu's components as DrawMenuComponents draws them, without starting its music</summary>
    internal void DrawComponents(string name, params string[] leaveOut)
    {
        var menu = assets.GetMenu(name);
        if (menu == null)
            return;

        var language = assets.GetText("en-us");
        graphics.Menus.DrawComponents(menu, FitOf(name), InCurrentGamePack, text => text.ToLanguageText(language), leaveOut);
    }

    /// <summary>
    /// Whether a menu item or component with this game-packs list belongs in the running game pack
    /// (by its own, its base packs' and its menus-like pack's names)
    /// </summary>
    internal bool InCurrentGamePack(List<string>? gamePacks)
        => GamePackList.Includes(gamePacks, engine.GamePackId, assets.MenuPackIds);

    /// <summary>
    /// Plays a menu's music. A track that is already playing carries on rather than restarting,
    /// and a menu without music leaves the current track alone.
    /// </summary>
    private void StartMenuMusic(MenuMetadata menu)
    {
        if (string.IsNullOrEmpty(menu.Music))
            return;

        var music = engine.GetGameInfo().MenuMusic ?? menu.Music;
        if (string.Equals(audio.CurrentMusicTrack, music, StringComparison.OrdinalIgnoreCase))
        {
            audio.SetPaused(false);
            return;
        }

        audio.PlayMusic(music);     // StartCPMusic
    }

    /// <summary>What a menu item does: its action, from those registered, or null (having said why) for none</summary>
    internal Func<int, int>? MapFunction(string menuName, MenuSwitcher? mi)
    {
        if (mi == null || string.IsNullOrEmpty(mi.Action))
            return null;

        // A page of presenter text (Blake Stone's instructions, story and ordering info)
        if (mi.Action == TextScreenAction && TextScreen != null)
        {
            var script = mi.Script;
            if (string.IsNullOrEmpty(script))
            {
                Console.WriteLine($"Menu '{menuName}': item '{mi.Id ?? mi.Text}' is a CP_TextScreen without a script");
                return null;
            }
            return TextScreen(script);
        }

        // A mod's: another menu, a text or a script (MENU HOOKS)
        var (hook, target, field) = mi.Action switch
        {
            SubMenuAction => (SubMenu, mi.Menu, "menu"),
            ArticleAction => (Article, mi.Article, "article"),
            ScriptAction => (Script, mi.Function, "function"),
            _ => ((Func<string, int>?)null, (string?)null, (string?)null),
        };
        if (field != null)
        {
            if (string.IsNullOrWhiteSpace(target) || hook == null)
            {
                Console.WriteLine($"Menu '{menuName}': item '{mi.Id ?? mi.Text}' is a {mi.Action} without a {field}");
                return null;
            }
            return _ => hook(target.Trim());
        }

        if (!actions.TryGetValue(mi.Action, out var func))
        {
            Console.WriteLine($"Menu '{menuName}': unknown action '{mi.Action}' on item '{mi.Id ?? mi.Text}'");
            return null;
        }

        return func;
    }
}
