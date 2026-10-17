using PFWolf.Assets;
using PFWolf.Fonts;

namespace PFWolf.Ui;

/// <summary>
/// Drawing the menus: the backdrop, stripes, windows and boxes, the fades, a menu's rows and
/// highlight bar, its sliders, choices and checkboxes, the gun, and message boxes
/// </summary>
/// <param name="settings">game-info's menu fade, as read (GameHost's GameInfoSettings)</param>
internal sealed class MenuDrawing(GraphicManager graphics, VideoManager video, AssetManager assets, FontManager fonts,
    GameEngineManager engine, UiText ui, Func<GameInfoSettings> settings)
{
    static readonly string[] colorHlite =
    {
        "DEACTIVE",
        "HIGHLIGHT",
        "READHCOLOR",
        "Green" // 4 165 0 (used for disabled episodes)
    };

    static readonly string[] colorNorml =
    {
        "DEACTIVE",
        "TEXTCOLOR",
        "READCOLOR",
        "DarkGreen" // 4 113 0 (used for disabled episodes)
    };

    internal void ClearMScreen()
    {
        graphics.DrawMenuBackground("BORDCOLOR");
    }

    internal void DrawStripes(int y)
    {
        graphics.DrawStripe(y);
    }

    internal void DrawWindow(int x, int y, int w, int h, string wcolor)
    {
        video.Bar(x, y, w, h, wcolor);
        DrawOutline(x, y, w, h, "BORD2COLOR", "DEACTIVE");
    }

    internal void DrawOutline(int x, int y, int w, int h, string color1, string color2)
    {
        video.HorizontalLine(x, x + w, y, color2);
        video.VerticalLine(y, y + h, x, color2);
        video.HorizontalLine(x, x + w, y + h, color1);
        video.VerticalLine(y, y + h, x + w, color1);
    }

    internal void MenuFadeOut()
    {
        var fadeColor = engine.GetGameInfo().MenuFadeColor;
        video.FadeOut(settings().MenuFadeStyle, string.IsNullOrEmpty(fadeColor) ? new Color { Alpha = 255 } : Color.FromHexRGBA(fadeColor), 10, settings().MenuFadeTics);
    }
    internal void MenuFadeIn() => video.FadeIn(settings().MenuFadeStyle, 10, settings().MenuFadeTics);

    internal void DrawMenu(ControlPanelMenuInfo item_i, ControlPanelItem[] items)
    {
        int which = item_i.CurPos;

        for (int i = 0; i < item_i.Amount; i++)
            PrintMenuItem(item_i, items, i, items[i].Active > 0 ? MenuItemColor(items[i], which == i) : "DEACTIVE");
    }

    /// <summary>An item's text in its row; a newline in it goes on under the start of the text</summary>
    internal void PrintMenuItem(ControlPanelMenuInfo item_i, ControlPanelItem[] items, int which, string color)
        => ui.TextAt(item_i.X + item_i.Indent, MenuItemY(item_i, which), MenuItemStyle(item_i, color)).Print(items[which].Text);

    /// <summary>The top of an item's row</summary>
    internal static int MenuItemY(ControlPanelMenuInfo item_i, int which) => item_i.Y + which * item_i.RowHeight;

    /// <summary>The menu's text style for its items: its font and shadow (menudef font, item-shadow)</summary>
    internal TextStyle MenuItemStyle(ControlPanelMenuInfo item_i, string color)
        => new(item_i.Font ?? UiFonts.MenuFont, color,
            Shadow: item_i.ItemShadow is { } shadow ? new FontShadow(1, 1, shadow) : null);

    /// <summary>
    /// Draws or erases a menu's highlight bar (menudef cursor) behind the item, printing the item
    /// over it highlighted, or plain when the bar is off
    /// </summary>
    internal void DrawMenuBar(ControlPanelMenuInfo item_i, ControlPanelItem[] items, int which, bool on)
    {
        var bar = item_i.Cursor!;
        video.Bar(bar.X, MenuItemY(item_i, which) + bar.YOffset, bar.Width, bar.Height, on ? bar.Color : bar.EraseColor);
        PrintMenuItem(item_i, items, which, MenuItemColor(items[which], on));
        if (items[which].CheckBox is bool checkedOn)
            DrawMenuCheckbox(item_i, which, checkedOn, highlighted: on);
    }

    internal static string MenuItemColor(ControlPanelItem item, bool hlight)
        => hlight ? colorHlite[item.Active] : colorNorml[item.Active];

    internal const int MenuSliderWidth = 88;
    internal const int MenuSliderOffset = 120; // from the start of the row's text, clear of "Sound Volume"; choices use it too

    // A level bar to the right of a SliderMenuItem's text, drawn like the mouse sensitivity bar:
    // a track with a highlighted knob at the current value (0 to max).
    internal void DrawMenuSlider(ControlPanelMenuInfo iteminfo, ControlPanelItem[] items, string id, int value, int max)
    {
        int index = Array.FindIndex(items, item => item.Id == id);
        if (index < 0)
            return;

        int knobWidth = MenuSliderWidth / (max + 1);
        int x = iteminfo.X + iteminfo.Indent + MenuSliderOffset;
        int y = iteminfo.Y + index * iteminfo.RowHeight + 2;
        video.Bar(x, y, knobWidth * (max + 1), 9, "TEXTCOLOR");
        DrawOutline(x, y, knobWidth * (max + 1), 9, "Black", "HIGHLIGHT");
        DrawOutline(x + knobWidth * value, y, knobWidth, 9, "Black", "READCOLOR");
        video.Bar(x + knobWidth * value + 1, y + 1, knobWidth - 1, 8, "READHCOLOR");
    }

    internal const int MenuChoiceWidth = 96; // room for the widest value, like "1920x1200"

    // A ChoiceMenuItem's current value, in the same column as the sliders. Greyed out with the row.
    internal void DrawMenuChoice(ControlPanelMenuInfo iteminfo, ControlPanelItem[] items, string id, string value)
    {
        int index = Array.FindIndex(items, item => item.Id == id);
        if (index < 0)
            return;

        int x = iteminfo.X + iteminfo.Indent + MenuSliderOffset;
        int y = iteminfo.Y + index * iteminfo.RowHeight;
        video.Bar(x, y, MenuChoiceWidth, iteminfo.RowHeight, "BKGDCOLOR");

        ui.TextAt(x, y, MenuItemStyle(iteminfo, MenuItemColor(items[index], false))).Print(value);
    }

    /// <summary>
    /// The choice after stepping a ChoiceMenuItem by delta. Left/right stop at the ends of the
    /// list; Enter wraps round, so it can reach every value on its own.
    /// </summary>
    internal static int StepMenuChoice(int current, int count, int delta, bool wrap)
    {
        if (count <= 0)
            return 0;

        int next = current + delta;
        return wrap ? ((next % count) + count) % count : Math.Clamp(next, 0, count - 1);
    }

    // The on/off box to the left of a ToggleMenuItem's text
    internal void DrawMenuCheckbox(ControlPanelMenuInfo iteminfo, ControlPanelItem[] items, string id, bool on)
    {
        int index = Array.FindIndex(items, item => item.Id == id);
        if (index >= 0)
        {
            items[index].CheckBox = on;
            DrawMenuCheckbox(iteminfo, index, on);
        }
    }

    /// <param name="highlighted">Under a highlight bar: the pack's c_selected_hi / c_notselected_hi, when it has them</param>
    internal void DrawMenuCheckbox(ControlPanelMenuInfo iteminfo, int index, bool on, bool highlighted = false)
    {
        int x = iteminfo.X + iteminfo.Indent + iteminfo.CheckboxX;
        int y = iteminfo.Y + index * iteminfo.RowHeight + iteminfo.CheckboxY;
        var pic = on ? "c_selected" : "c_notselected";
        if (highlighted && assets.Exists<GraphicAsset>(pic + "_hi"))
            pic += "_hi";
        graphics.DrawPic(pic, x, y);
    }

    internal void DrawMenuGun(ControlPanelMenuInfo iteminfo)
    {
        int x, y;

        // A highlight bar goes on with its item's text when HandleMenu starts
        if (iteminfo.Cursor != null)
            return;

        x = iteminfo.X & -8;    // same column HandleMenu draws and erases the gun in
        y = iteminfo.Y + iteminfo.CurPos * iteminfo.RowHeight - 2;
        graphics.DrawPic("c_cursor1", x, y);
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
            : new TextStyle(box.Font, box.Color, box.Med, Shadow: new FontShadow(1, 1, box.Shadow));

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

        if (box != null)
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
    internal void DrawCustomArrow(int x, int y, bool up)
    {
        for (int i = 0; i < 4; i++)
            video.HorizontalLine(x - i, x + i, up ? y + i : y + 3 - i, "READCOLOR");
    }

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
