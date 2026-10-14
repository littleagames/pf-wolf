using PFWolf.Assets;
using PFWolf.Extensions;
using PFWolf.Fonts;

namespace PFWolf.Ui;

/// <summary>
/// The menus menudefs/ defines: their items and layout (LoadMenu), and their non-interactive
/// parts (DrawMenuComponents). What an item does is an action registered by name (the CP_
/// routines); a menu built from game data has an items-source, registered too unless it's
/// episodes or skills, which come from game-info.
/// </summary>
internal sealed class MenuDefs(AssetManager assets, GameEngineManager engine, GraphicManager graphics, AudioManager audio, UiText ui)
{
    readonly Dictionary<string, Func<int, int>> actions = new(StringComparer.Ordinal);
    readonly Dictionary<string, Func<CP_itemtype[]>> sources = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A page of presenter text (Blake Stone's instructions, story and ordering info): the
    /// CP_TextScreen action, made for a menu item's script
    /// </summary>
    internal Func<string, Func<int, int>>? TextScreen { get; set; }

    internal const string TextScreenAction = "CP_TextScreen";

    /// <summary>What a menu item with action: <paramref name="name"/> runs</summary>
    internal void RegisterAction(string name, Func<int, int> action) => actions[name] = action;

    /// <summary>The rows of a menu with items-source: <paramref name="name"/></summary>
    internal void RegisterSource(string name, Func<CP_itemtype[]> rows) => sources[name] = rows;

    /// <summary>
    /// Builds the item list and layout for a menu defined in menudefs/
    /// </summary>
    internal (CP_itemtype[] items, CP_iteminfo info) LoadMenu(string name, short curpos = 0)
    {
        var menuAsset = assets.GetMenu(name)
            ?? throw new InvalidOperationException($"Menu '{name}' not found in menudefs");
        var language = assets.GetText("en-us");

        CP_itemtype[] items;
        if (menuAsset.ItemsSource != null)
        {
            if (menuAsset.MenuItems.Count > 0)
                Console.WriteLine($"Menu '{name}': has items-source '{menuAsset.ItemsSource}', so its menu-items are ignored");
            items = BuildSourceItems(name, menuAsset.ItemsSource);
        }
        else
        {
            items = menuAsset.MenuItems
                    .Where(mi => InCurrentGamePack(mi.GamePacks))
                    .Select(mi =>
                    new CP_itemtype(
                        (short)(mi.Enabled && mi is not BlankMenuItem ? (mi.Highlighted ? 2 : 1) : 0),
                        (mi.Text ?? "").ToLanguageText(language),
                        MapFunction(name, mi as MenuSwitcher))
                    {
                        id = mi.Id,
                        shortKey = string.IsNullOrEmpty(mi.ShortKey) ? '\0' : mi.ShortKey[0],
                        data = (mi as MusicMenuItem)?.Music
                    })
                    .ToArray();
        }

        if (menuAsset.DefaultSelection is int selection)
        {
            if (selection >= 0 && selection < items.Length)
                curpos = (short)selection;
            else if (menuAsset.ItemsSource != null)
                curpos = (short)Math.Max(0, items.Length - 1);    // the game has fewer (one skill, say): the last
            else
                Console.WriteLine($"Menu '{name}': default-selection {selection} is out of range (0-{items.Length - 1})");
        }

        var info = new CP_iteminfo(
            (short)menuAsset.Position.X,
            (short)menuAsset.Position.Y,
            amount: (short)items.Length,
            curpos: curpos,
            indent: (short)menuAsset.Indent)
        {
            rowHeight = menuAsset.RowHeight is > 0 and int rowHeight ? rowHeight : 13,
            font = menuAsset.Font,
            itemShadow = menuAsset.ItemShadow,
            cursor = menuAsset.Cursor,
            selectionPic = menuAsset.SelectionPic,
            checkboxX = menuAsset.CheckboxOffset?.X ?? -24,
            checkboxY = menuAsset.CheckboxOffset?.Y ?? 3,
        };

        return (items, info);
    }

    /// <summary>
    /// Builds menu items from game data for a menu's items-source
    /// </summary>
    private CP_itemtype[] BuildSourceItems(string menuName, string source)
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
                        .Select(ep => new CP_itemtype(1, ep.Name.ToLanguageText(language), null, ep) { shortKey = ep.Key })
                        .ToArray();
                return gameInfo.Episodes.Values.SelectMany(ep =>
                    new CP_itemtype[]
                    {
                        new CP_itemtype(1, ep.Name.ToLanguageText(language), null, ep) { shortKey = ep.Key },
                        new CP_itemtype(0, "", null)
                    }).SkipLast(1)
                    .ToArray();

            case "skills":
                return gameInfo.Skills.Values
                    .Select(skill => new CP_itemtype(1, skill.Name.ToLanguageText(language), null, skill))
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
    internal static CP_itemtype? FindMenuItem(CP_itemtype[] items, string id)
        => items.FirstOrDefault(item => item.id == id);

    /// <summary>
    /// Id of the chosen item from HandleMenu's result, or null for escape / an item without an id
    /// </summary>
    internal static string? SelectedId(CP_itemtype[] items, int which)
        => which >= 0 && which < items.Length ? items[which].id : null;

    /// <summary>
    /// Draws the non-interactive parts of a menu (background, windows, graphics, labels)
    /// </summary>
    internal void DrawMenuComponents(string name)
    {
        var menu = assets.GetMenu(name);
        if (menu == null)
            return;

        StartMenuMusic(menu);

        foreach (var component in menu.Components)
        {
            if (!InCurrentGamePack(component.GamePacks))
                continue;

            if (component is Label label)
                DrawLabel(label);
            else
                graphics.DrawComponent(component);
        }
    }

    /// <summary>
    /// Whether a menu item or component with this game-packs list belongs in the running game pack
    /// </summary>
    internal bool InCurrentGamePack(List<string>? gamePacks)
        => GamePackList.Includes(gamePacks, engine.GamePackId, assets.BasePackIds);

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

    /// <summary>A label's shadow: its own, none, or null to keep the font's</summary>
    internal static FontShadow? LabelShadow(Label label)
    {
        if (label.Shadow == false)
            return FontShadow.None;

        if (label.Shadow == true || label.ShadowX != null || label.ShadowY != null || label.ShadowColor != null)
        {
            var d = FontShadow.Default;
            return new FontShadow(label.ShadowX ?? d.X, label.ShadowY ?? d.Y, label.ShadowColor ?? d.Color);
        }

        return null;
    }

    /// <summary>A label's gradient: its own, none, or null to keep the font's</summary>
    internal static FontGradient? LabelGradient(Label label)
    {
        if (label.Gradient == false)
            return FontGradient.None;

        if (label.Gradient == true || label.GradientTop != null || label.GradientBottom != null)
        {
            var d = FontGradient.Default;
            return new FontGradient(label.GradientTop ?? d.Top, label.GradientBottom ?? d.Bottom);
        }

        return null;
    }

    /// <summary>A label's outline: its own, none, or null to keep the font's</summary>
    internal static FontOutline? LabelOutline(Label label)
    {
        if (label.Outline == false)
            return FontOutline.None;

        if (label.Outline == true || label.OutlineColor != null || label.OutlineThickness != null)
        {
            var d = FontOutline.Default;
            return new FontOutline(label.OutlineColor ?? d.Color, Math.Max(label.OutlineThickness ?? d.Thickness, 1));
        }

        return null;
    }

    /// <summary>A label's glow: its own, none, or null to keep the font's</summary>
    internal static FontGlow? LabelGlow(Label label)
    {
        if (label.Glow == false)
            return FontGlow.None;

        if (label.Glow == true || label.GlowColor != null || label.GlowRadius != null || label.GlowStrength != null)
        {
            var d = FontGlow.Default;
            return new FontGlow(label.GlowColor ?? d.Color, Math.Max(label.GlowRadius ?? d.Radius, 1),
                Math.Clamp(label.GlowStrength ?? d.Strength, 0, 100));
        }

        return null;
    }

    private void DrawLabel(Label label)
    {
        var language = assets.GetText("en-us");
        var style = new TextStyle(label.Font, label.Color, Shadow: LabelShadow(label), Gradient: LabelGradient(label),
            Outline: LabelOutline(label), Glow: LabelGlow(label));

        var text = label.Text.ToLanguageText(language);
        if (label.HorizontalOrientation == HorizontalOrientation.Center && label.Width > 0)
            ui.CenteredText(label.X, label.Width, label.Y, style).CPrint(text);
        else if (label.HorizontalOrientation == HorizontalOrientation.Center)
            ui.CenteredText(0, 320, label.Y, style).CPrint(text);
        else
            ui.TextAt(label.X, label.Y, style).Print(text);
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

        if (!actions.TryGetValue(mi.Action, out var func))
        {
            Console.WriteLine($"Menu '{menuName}': unknown action '{mi.Action}' on item '{mi.Id ?? mi.Text}'");
            return null;
        }

        return func;
    }
}
