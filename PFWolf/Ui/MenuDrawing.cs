using PFWolf.Fonts;
using PFWolf.Menus;

namespace PFWolf.Ui;

/// <summary>
/// Drawing the menus: the fades, the gun and message boxes here; the backdrop, stripes, windows,
/// a menu's rows and highlight bar, its sliders, choices and checkboxes are the shared
/// <see cref="MenuPainter"/>'s (PFWolf.Common), which the editor draws its menus with too
/// </summary>
/// <param name="settings">game-info's menu fade, as read (GameHost's GameInfoSettings)</param>
internal sealed class MenuDrawing(GraphicManager graphics, VideoManager video, FontManager fonts,
    GameEngineManager engine, Func<GameInfoSettings> settings)
{
    private MenuPainter Painter => graphics.Menus;

    internal void ClearMScreen()
    {
        graphics.DrawMenuBackground("BORDCOLOR");
    }

    internal void DrawStripes(int y)
    {
        graphics.DrawStripe(y);
    }

    internal void DrawWindow(int x, int y, int w, int h, string wcolor) => Painter.DrawWindow(x, y, w, h, wcolor);

    internal void DrawOutline(int x, int y, int w, int h, string color1, string color2) => Painter.DrawOutline(x, y, w, h, color1, color2);

    internal void MenuFadeOut()
    {
        var fadeColor = engine.GetGameInfo().MenuFadeColor;
        video.FadeOut(settings().MenuFadeStyle, string.IsNullOrEmpty(fadeColor) ? new Color { Alpha = 255 } : Color.FromHexRGBA(fadeColor), 10, settings().MenuFadeTics);
    }
    internal void MenuFadeIn() => video.FadeIn(settings().MenuFadeStyle, 10, settings().MenuFadeTics);

    internal void DrawMenu(ControlPanelMenuInfo item_i, ControlPanelItem[] items) => Painter.DrawMenu(item_i, items);

    /// <summary>A scrolling menu's marks at its right edge (x), with how many rows are above and below</summary>
    internal void DrawMenuScrollMarks(ControlPanelMenuInfo item_i, ControlPanelItem[] items, int x) => Painter.DrawMenuScrollMarks(item_i, items, x);

    /// <summary>An item's text in its row; a newline in it goes on under the start of the text</summary>
    internal void PrintMenuItem(ControlPanelMenuInfo item_i, ControlPanelItem[] items, int which, string color)
        => Painter.PrintMenuItem(item_i, items, which, color);

    /// <summary>The top of an item's row</summary>
    internal static int MenuItemY(ControlPanelMenuInfo item_i, int which) => MenuPainter.MenuItemY(item_i, which);

    /// <summary>The menu's text style for its items: its font and shadow (menudef font, item-shadow)</summary>
    internal static TextStyle MenuItemStyle(ControlPanelMenuInfo item_i, string color) => MenuPainter.MenuItemStyle(item_i, color);

    /// <summary>Draws or erases a menu's highlight bar (menudef cursor) behind the item</summary>
    internal void DrawMenuBar(ControlPanelMenuInfo item_i, ControlPanelItem[] items, int which, bool on) => Painter.DrawMenuBar(item_i, items, which, on);

    internal static string MenuItemColor(ControlPanelItem item, bool hlight) => MenuPainter.MenuItemColor(item, hlight);

    internal const int MenuSliderWidth = MenuPainter.MenuSliderWidth;
    internal const int MenuSliderOffset = MenuPainter.MenuSliderOffset;

    /// <summary>A level bar to the right of a SliderMenuItem's text, with a knob at the value (0 to max)</summary>
    internal void DrawMenuSlider(ControlPanelMenuInfo iteminfo, ControlPanelItem[] items, string id, int value, int max)
        => Painter.DrawMenuSlider(iteminfo, items, id, value, max);

    internal const int MenuChoiceWidth = MenuPainter.MenuChoiceWidth;

    /// <summary>A ChoiceMenuItem's current value, in the same column as the sliders</summary>
    internal void DrawMenuChoice(ControlPanelMenuInfo iteminfo, ControlPanelItem[] items, string id, string value)
        => Painter.DrawMenuChoice(iteminfo, items, id, value);

    /// <summary>The choice after stepping a ChoiceMenuItem by delta (see MenuPainter.StepMenuChoice)</summary>
    internal static int StepMenuChoice(int current, int count, int delta, bool wrap) => MenuPainter.StepMenuChoice(current, count, delta, wrap);

    /// <summary>The on/off box to the left of a ToggleMenuItem's text</summary>
    internal void DrawMenuCheckbox(ControlPanelMenuInfo iteminfo, ControlPanelItem[] items, string id, bool on)
        => Painter.DrawMenuCheckbox(iteminfo, items, id, on);

    /// <param name="highlighted">Under a highlight bar: the pack's c_selected_hi / c_notselected_hi, when it has them</param>
    internal void DrawMenuCheckbox(ControlPanelMenuInfo iteminfo, int index, bool on, bool highlighted = false)
        => Painter.DrawMenuCheckbox(iteminfo, index, on, highlighted);

    internal void DrawMenuGun(ControlPanelMenuInfo iteminfo) => Painter.DrawMenuGun(iteminfo, settings().MenuGun);

    /// <summary>The gun cursor's shape, unless game-info's menu-cursor is none</summary>
    internal void DrawGunPic(string shape, int x, int y)
    {
        if (settings().MenuGun)
            graphics.DrawPic(shape, x, y);
    }

    /// <summary>
    /// Shows text in a box in the middle of the screen, and returns the box's text window with its
    /// print position just after the text
    /// </summary>
    /// <param name="areaHeight">Height of the screen area the box is centered in: the play view (MaxY) in a game</param>
    internal TextWindow Message(string text, int areaHeight = 200)
    {
        int h = 0, w = 0, mw = 0, i, len = text.Length;
        var box = engine.GetGameInfo().MenuMessage;
        var style = box == null
            ? new TextStyle(UiFonts.LargeFont, "Black", "TEXTCOLOR")
            : new TextStyle(box.Font, box.Color, box.Med, Shadow: box.Shadow.Equals("none", StringComparison.OrdinalIgnoreCase)
                ? FontShadow.None : new FontShadow(1, 1, box.Shadow));

        var font = fonts.Find(style.Font);
        if (font == null)
            return TextWindow.FullScreen(graphics, style);
        h = font.LineHeight;

        for (i = 0; i < len; i++)
        {
            if (text[i] == '\n')
            {
                if (w > mw)
                    mw = w;
                w = 0;
                h += font.LineHeight;
            }
            else
                w += font.Advance(text[i]);
        }

        if (w + 10 > mw)
            mw = w + 10;

        int x = 160 - (mw / 2);
        int y = (areaHeight / 2) - (h / 2);

        if (box is { Window: true })
        {
            // Wolf3D's window in the game's colors (Midnight Assault's Message: BKGDCOLOR, edged
            // in BORDCOLOR and black)
            DrawWindow(x - 5, y - 5, mw + 10, h + 10, box.Med);
            DrawOutline(x - 5, y - 5, mw + 10, h + 10, box.Lo, box.Hi);
        }
        else if (box != null)
            BevelBox(x - 5, y - 5, mw + 10, h + 10, box.Hi, box.Med, box.Lo);
        else
        {
            DrawWindow(x - 5, y - 5, mw + 10, h + 10, "TEXTCOLOR");
            DrawOutline(x - 5, y - 5, mw + 10, h + 10, "Black", "HIGHLIGHT");
        }
        var window = new TextWindow(graphics, x, y, mw, h, style);
        window.Print(text);
        video.Update();
        return window;
    }

    /// <summary>A bevelled box: the face, its light top and left edges and dark bottom and right ones</summary>
    internal void BevelBox(int x, int y, int width, int height, int hi, int med, int lo)
        => BevelBox(x, y, width, height, hi.ToString(), med.ToString(), lo.ToString());

    /// <summary>
    /// A bevelled box in colors by name or palette index. Its two mixed corners are the palette
    /// color after the face's, when that's an index.
    /// </summary>
    internal void BevelBox(int x, int y, int width, int height, string hi, string med, string lo)
    {
        int x2 = x + width - 1, y2 = y + height - 1;
        video.Bar(x, y, width, height, med);
        video.HorizontalLine(x, x2, y, hi);
        video.HorizontalLine(x, x2, y2, lo);
        video.VerticalLine(y, y2, x, hi);
        video.VerticalLine(y, y2, x2, lo);
        var corner = int.TryParse(med, out int index) ? (index + 1).ToString() : med;
        video.Bar(x, y2, 1, 1, corner);
        video.Bar(x2, y, 1, 1, corner);
    }

    /// <summary>A small arrow saying a list scrolls further up or down (the customize and load/save screens)</summary>
    internal void DrawCustomArrow(int x, int y, bool up) => Painter.DrawCustomArrow(x, y, up);

    // Message boxes don't wrap, so break a long reason onto lines of about 28 characters.
    internal static string WrapForMessage(string text)
    {
        var lines = new List<string>();
        var line = new System.Text.StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > 28)
            {
                lines.Add(line.ToString());
                line.Clear();
            }
            if (line.Length > 0)
                line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0)
            lines.Add(line.ToString());
        return string.Join('\n', lines);
    }
}
