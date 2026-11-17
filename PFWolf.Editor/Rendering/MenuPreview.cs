using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Fonts;
using PFWolf.Menus;

namespace PFWolf.Editor.Rendering;

/// <summary>What a part of a menu's picture is: one of its components, a row, the cursor, the highlighted row's picture, the stats panel</summary>
public enum MenuPartKind { Component, Row, Cursor, Picture, StatsPanel }

/// <summary>
/// Where a part of a menu is on its picture, in the 320x200 layout's pixels (the picture's
/// margins, on a wide one, are left of 0 and right of 320); Index is the component's in the
/// menu's components or the row's
/// </summary>
public sealed record MenuPart(MenuPartKind Kind, int Index, int X, int Y, int Width, int Height, string Label)
{
    public bool Contains(double x, double y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
}

/// <summary>
/// How the menu window shows a menu besides what menudefs say: where the cursor is, whether a
/// game is going on (the main menu's Back to Game), the toggles' checkboxes, a made-up number
/// of items-source rows (to try fit-items), the highlight bar's flash, a wide screen
/// </summary>
public sealed record MenuPreviewState(int? CursorRow = null, bool InGame = false, bool ChecksOn = true, int? RowCount = null,
    bool BarOn = true, bool Wide = false);

/// <summary>A menu drawn: the picture, the layout's place in it, its parts and rows, and what the drawing found wrong</summary>
public sealed record MenuPicture(IndexedPicture Picture, int OriginX, int OriginY, IReadOnlyList<MenuPart> Parts,
    ControlPanelItem[] Rows, ControlPanelMenuInfo Info, IReadOnlyList<string> Problems);

/// <summary>
/// Draws a menu as the game draws it (PFWolf.Common's MenuPainter, the game's own drawing) on a
/// picture in the loaded game's palette, with the game code's parts made up (MenuSamples), and
/// says where each part went, for picking them on the picture
/// </summary>
public sealed class MenuPreview
{
    /// <summary>The wide picture's width: 16:10's room at the game's 200 lines</summary>
    public const int WideWidth = 384;

    private readonly GameContent _content;
    private readonly IReadOnlyDictionary<string, FontDefinition> _definitions;
    private readonly FontBuilder _builder;
    private readonly IndexedTextCanvas _colors;
    private readonly Dictionary<string, Font?> _fonts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _fontProblems = [];

    public MenuPreview(GameContent content)
    {
        _content = content;
        _definitions = FontCatalog.Definitions(content);
        _colors = new IndexedTextCanvas(1, 1, content.Palette, Theme(content));
        _builder = new FontBuilder(content.Find<FontAsset>,
            name => content.Assets.Exists<GraphicAsset>(name) ? content.Find<GraphicAsset>(name) : null,
            _colors.ResolveColor, _fontProblems.Add);
    }

    private static ColorThemeAsset? Theme(GameContent content)
        => content.Assets.Exists<ColorThemeAsset>($"{content.PackId}/colors") ? content.Assets.FindInGamePack<ColorThemeAsset>("colors") : null;

    // A font as the game's FontManager finds it: built once
    private Font? FindFont(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (!_fonts.TryGetValue(name, out var font))
            _fonts[name] = font = _builder.Build(name, _definitions.GetValueOrDefault(name));
        return font;
    }

    private string Text(string? text) => MenuCatalog.Shown(_content, text);

    private bool InPack(List<string>? gamePacks) => MenuCatalog.InPack(_content, gamePacks);

    /// <summary>A menu's rows and layout, as the game's LoadMenu makes them (items-source rows made up where the game's code makes them)</summary>
    public (ControlPanelItem[] Rows, ControlPanelMenuInfo Info, MenuFit Fit) Layout(string name, MenuMetadata menu, MenuPreviewState state, List<string>? warnings = null)
    {
        ControlPanelItem[] rows;
        if (menu.ItemsSource != null)
            rows = MenuSamples.SourceRows(_content, menu, menu.ItemsSource, state.RowCount);
        else if (name.Equals("new-level", StringComparison.OrdinalIgnoreCase) && menu.MenuItems.Count == 0)
            rows = MenuSamples.LevelRows(_content);
        else
            rows = MenuRows.FromItems(menu, InPack, Text, _ => null);

        // The main menu in a game: Back to Game (read-it colors) and Save game
        if (name.Equals("main-menu", StringComparison.OrdinalIgnoreCase) && state.InGame)
        {
            foreach (var row in rows)
            {
                if (row.Id == "backtodemo")
                    (row.Text, row.Active) = (Text("$MENU_BACKTOGAME"), 2);
                else if (row.Id == "savegame")
                    row.Active = 1;
            }
        }

        var (info, fit) = MenuRows.Layout(name, menu, rows, 0, rows.Any(row => MenuSamples.PictureOf(row) is { Length: > 0 }),
            warning => warnings?.Add(warning));
        if (state.CursorRow is int cursor && rows.Length > 0)
            info.CurPos = (short)Math.Clamp(cursor, 0, rows.Length - 1);
        info.ScrollTo(info.CurPos);
        return (rows, info, fit);
    }

    /// <summary>Draws a menu with its made-up parts</summary>
    public MenuPicture Draw(string name, MenuMetadata menu, MenuPreviewState state)
    {
        var problems = new List<string>();
        _fontProblems.Clear();
        var (rows, info, fit) = Layout(name, menu, state, problems);

        var gameInfo = _content.GameInfo;
        var canvas = new IndexedMenuCanvas(state.Wide ? WideWidth : IndexedMenuCanvas.LayoutWidth, IndexedMenuCanvas.LayoutHeight,
            _content.Palette, Theme(_content), FindFont, picture => _content.Find<GraphicAsset>(picture),
            gameInfo?.MenuBackdrop, gameInfo?.MenuStripe);
        var painter = new MenuPainter(canvas);
        var parts = new List<MenuPart>();

        // The components, in order
        for (int i = 0; i < menu.Components.Count; i++)
        {
            var entry = menu.Components[i];
            if (!InPack(entry.GamePacks))
                continue;
            var component = MenuFitting.Fitted(entry, menu.FitItems, fit);
            painter.DrawComponent(component, Text);
            if (Bounds(canvas, component) is { } box)
                parts.Add(new MenuPart(MenuPartKind.Component, i, box.X, box.Y, box.W, box.H, Describe(component)));
        }

        // The rows, with what the game's code draws on them
        painter.DrawMenu(info, rows);
        for (int i = 0; i < rows.Length; i++)
        {
            if (!info.IsRowVisible(i))
                continue;
            canvas.MeasureText(rows[i].Text.Split('\n')[0], info.Font ?? MenuPainter.DefaultMenuFont, out int width, out _);
            parts.Add(new MenuPart(MenuPartKind.Row, i, info.X + info.Indent, info.RowY(i), Math.Max(width, 8), info.RowHeight,
                string.IsNullOrEmpty(rows[i].Text) ? $"Row {i + 1} (blank)" : $"Row {i + 1}: {rows[i].Text}"));
        }
        DrawValues(painter, menu, info, rows, state);
        painter.DrawMenuScrollMarks(info, rows, info.ScrollMarkX ?? 300);

        // The cursor: Blake Stone's highlight bar, or Wolf3D's gun
        if (rows.Length > 0)
        {
            if (info.Cursor is { } bar)
            {
                painter.DrawMenuBar(info, rows, info.CurPos, state.BarOn);
                parts.Add(new MenuPart(MenuPartKind.Cursor, 0, bar.X, info.RowY(info.CurPos) + bar.YOffset, bar.Width, bar.Height, "The highlight bar (cursor)"));
            }
            else
            {
                bool gun = !string.Equals(gameInfo?.MenuCursor?.Trim(), "none", StringComparison.OrdinalIgnoreCase);
                painter.DrawMenuGun(info, gun);
                if (gun && canvas.PictureSize("c_cursor1") is { } size)
                    parts.Add(new MenuPart(MenuPartKind.Cursor, 0, info.X & -8, info.RowY(info.CurPos) - 2, size.Width, size.Height, "The gun (cursor)"));
            }
        }

        DrawPictures(canvas, painter, name, info, rows, parts);

        if (menu.StatsPanel is { } panel)
        {
            StatsPanel.Draw(panel, MenuSamples.Stats, painter, Text);
            parts.Add(new MenuPart(MenuPartKind.StatsPanel, 0, panel.X, panel.Y, panel.Width + 1, panel.Height + 1, "The stats panel (a made-up best)"));
        }

        foreach (var color in canvas.UnknownColors)
            problems.Add($"There's no color {color} (not in colors.yaml, #RRGGBB or a palette index): drawn as palette index 0");
        problems.AddRange(_fontProblems.Distinct());

        var picture = new IndexedPicture(canvas.Width, canvas.Height, canvas.Pixels, null);
        return new MenuPicture(picture, canvas.OriginX, canvas.OriginY, parts, rows, info, problems);
    }

    // Sample values on the rows the game's code fills in: every other checkbox on (or all off), sliders, choices
    private static void DrawValues(MenuPainter painter, MenuMetadata menu, ControlPanelMenuInfo info, ControlPanelItem[] rows, MenuPreviewState state)
    {
        int toggles = 0;
        foreach (var item in menu.MenuItems)
        {
            if (item.Id == null || !rows.Any(row => row.Id == item.Id))
                continue;
            switch (item)
            {
                case ToggleMenuItem:
                    painter.DrawMenuCheckbox(info, rows, item.Id, state.ChecksOn && toggles++ % 2 == 0);
                    break;
                case SliderMenuItem:
                    painter.DrawMenuSlider(info, rows, item.Id, MenuSamples.SliderValue, MenuSamples.SliderMax);
                    break;
                case ChoiceMenuItem:
                    painter.DrawMenuChoice(info, rows, item.Id, MenuSamples.ChoiceValue);
                    break;
            }
        }
    }

    // The New Game menus' pictures, where the game's code puts them (Ui/NewGameMenu.cs)
    private static void DrawPictures(IMenuCanvas canvas, MenuPainter painter, string name, ControlPanelMenuInfo info, ControlPanelItem[] rows, List<MenuPart> parts)
    {
        string? Highlighted() => info.CurPos >= 0 && info.CurPos < rows.Length ? MenuSamples.PictureOf(rows[info.CurPos]) : null;

        void Draw(string? pic, int x, int y, int clearWidth = 0, int clearHeight = 0)
        {
            string color = "BKGDCOLOR";
            if (info.SelectionPic is { } at)
            {
                (x, y) = (at.X, at.Y);
                clearWidth = at.Width ?? clearWidth;
                clearHeight = at.Height ?? clearHeight;
                color = at.Color ?? color;
            }
            if (clearWidth > 0 && clearHeight > 0)
                canvas.Bar(x, y, clearWidth, clearHeight, color);
            if (!string.IsNullOrEmpty(pic))
                canvas.DrawPicture(pic, x, y);
            var size = string.IsNullOrEmpty(pic) ? null : canvas.PictureSize(pic);
            int w = Math.Max(clearWidth, size?.Width ?? 0), h = Math.Max(clearHeight, size?.Height ?? 0);
            if (w > 0 && h > 0)
                parts.Add(new MenuPart(MenuPartKind.Picture, info.CurPos, x, y, w, h, "The highlighted row's picture" + (pic != null ? $" ({pic})" : "")));
        }

        switch (name.ToLowerInvariant())
        {
            case "new-episode":
                if (info.SelectionPic != null)
                    Draw(Highlighted(), 0, 0);
                else
                    for (int i = 0; i < rows.Length; i++)
                        if (info.IsRowVisible(i) && MenuSamples.PictureOf(rows[i]) is { Length: > 0 } pic)
                            canvas.DrawPicture(pic, info.X + 32, info.RowY(i));
                break;
            case "new-game":
                Draw(Highlighted(), info.X + 185, Math.Min(info.Y, info.LayoutY) + 7);
                break;
            case "new-class":
                if (rows.Any(row => MenuSamples.PictureOf(row) is { Length: > 0 }))
                    Draw(Highlighted(), info.X + 175, info.LayoutY - 6, 96, 100);
                break;
        }
    }

    // Where a component drew, in layout pixels (an outline reaches a pixel past a window's width and height)
    private (int X, int Y, int W, int H)? Bounds(IMenuCanvas canvas, MenuComponent component)
    {
        switch (component)
        {
            case Background:
                return (0, 0, IndexedMenuCanvas.LayoutWidth, IndexedMenuCanvas.LayoutHeight);
            case Window window:
                return (window.X, window.Y, window.Width + 1, window.Height + 1);
            case Fill fill:
                return (fill.X, fill.Y, fill.Width, fill.Height);
            case Stripe stripe:
                return (0, stripe.Y, IndexedMenuCanvas.LayoutWidth, Math.Max(canvas.MenuStripe.Height, 1));
            case Graphic graphic when !string.IsNullOrEmpty(graphic.Name) && canvas.PictureSize(graphic.Name) is { } size:
                int x = graphic.HorizontalOrientation switch
                {
                    HorizontalOrientation.Center => 160 - size.Width / 2,
                    HorizontalOrientation.Right => 320 - size.Width,
                    _ => graphic.X,
                };
                int y = graphic.VerticalOrientation switch
                {
                    VerticalOrientation.Center => 100 - size.Height / 2,
                    VerticalOrientation.Bottom => 200 - size.Height,
                    _ => graphic.Y,
                };
                return (x, y, size.Width, size.Height);
            case Label label:
                var lines = Text(label.Text).TrimEnd('\n').Split('\n');
                int width = 0, height = 0;
                foreach (var line in lines)
                {
                    canvas.MeasureText(line, label.Font, out int w, out int h);
                    width = Math.Max(width, w);
                    height += h;
                }
                if (label.HorizontalOrientation == HorizontalOrientation.Center)
                {
                    var (left, across) = label.Width > 0 ? (label.X, label.Width) : (0, 320);
                    return (left + (across - width) / 2, label.Y, Math.Max(width, 1), Math.Max(height, 1));
                }
                return (label.X, label.Y, Math.Max(width, 1), Math.Max(height, 1));
            default:
                return null;
        }
    }

    private static string Describe(MenuComponent component)
    {
        var id = component.Id != null ? $" '{component.Id}'" : "";
        return component switch
        {
            Background background => $"Background{id} ({background.Color})",
            Window window => $"Window{id} at {window.X}, {window.Y}, {window.Width}x{window.Height}",
            Fill fill => $"Fill{id} at {fill.X}, {fill.Y}, {fill.Width}x{fill.Height}",
            Stripe stripe => $"Stripe{id} at y {stripe.Y}",
            Graphic graphic => $"Graphic{id}: {graphic.Name}",
            Label label => $"Label{id}: {label.Text.Split('\n')[0]}",
            _ => component.GetType().Name + id,
        };
    }
}
