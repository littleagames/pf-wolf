using PFWolf.Fonts;

namespace PFWolf.Menus;

/// <summary>
/// Draws menudefs menus, for the game and the editor alike: the backdrop and stripe, the
/// components (windows, fills, pictures, labels), a menu's rows and highlight bar, its sliders,
/// choices and checkboxes, the gun and the scroll marks. What the rows do, the fades and message
/// boxes stay the game's (MenuDrawing).
/// </summary>
public sealed class MenuPainter(IMenuCanvas canvas)
{
    public IMenuCanvas Canvas => canvas;

    // The rows' colors by their Active: greyed out, normal, read-it (Read This!), and the
    // episode menu's episodes that can't be picked
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

    /// <summary>The font menu rows print in when the menu names none</summary>
    public const string DefaultMenuFont = FontNames.Large;

    // ------------------------------------------------------------------ shapes

    public void HorizontalLine(int x1, int x2, int y, string color) => canvas.Bar(x1, y, x2 - x1 + 1, 1, color);

    public void VerticalLine(int y1, int y2, int x, string color) => canvas.Bar(x, y1, 1, y2 - y1 + 1, color);

    /// <summary>A window: filled, with a BORD2COLOR / DEACTIVE outline</summary>
    public void DrawWindow(int x, int y, int w, int h, string wcolor)
    {
        canvas.Bar(x, y, w, h, wcolor);
        DrawOutline(x, y, w, h, "BORD2COLOR", "DEACTIVE");
    }

    /// <summary>A box's edges: color2 along the top and left, color1 along the bottom and right</summary>
    public void DrawOutline(int x, int y, int w, int h, string color1, string color2)
    {
        HorizontalLine(x, x + w, y, color2);
        VerticalLine(y, y + h, x, color2);
        HorizontalLine(x, x + w, y + h, color1);
        VerticalLine(y, y + h, x + w, color1);
    }

    /// <summary>
    /// The screen filled with <paramref name="color"/>, or the menu backdrop with the color round
    /// it on a wide screen
    /// </summary>
    public void DrawBackground(string color)
    {
        // A backdrop covers the 320x200 in the middle of the screen; the color fills the rest
        var backdrop = canvas.MenuBackdrop;
        bool hasBackdrop = !string.IsNullOrEmpty(backdrop) && canvas.HasPicture(backdrop);
        if (!hasBackdrop || canvas.HasMargins)
            canvas.FillScreen(color);
        if (hasBackdrop)
            canvas.DrawPicture(backdrop!, 0, 0);
    }

    /// <summary>The menu stripe with its top at <paramref name="y"/>, in the given colors or the game's</summary>
    public void DrawStripe(int y, string? color = null, string? lineColor = null)
    {
        // Across the whole screen, however wide
        var stripe = canvas.MenuStripe;
        canvas.BarAcross(y, y + stripe.Height, color ?? stripe.Color);
        canvas.BarAcross(y + stripe.LineY, y + stripe.LineY + 1, lineColor ?? stripe.LineColor);
    }

    /// <summary>A small arrow saying a list scrolls further up or down</summary>
    public void DrawCustomArrow(int x, int y, bool up)
    {
        for (int i = 0; i < 4; i++)
            HorizontalLine(x - i, x + i, up ? y + i : y + 3 - i, "READCOLOR");
    }

    // ------------------------------------------------------------------ components

    /// <summary>
    /// A menu's components, in order, as its fit-items has them: those in the running pack
    /// (<paramref name="inPack"/>), leaving out those with an id in <paramref name="leaveOut"/>
    /// </summary>
    /// <param name="translate">A label's text as shown: a $language key's string</param>
    public void DrawComponents(MenuMetadata menu, MenuFit fit, Func<List<string>?, bool> inPack, Func<string, string> translate,
        IReadOnlyCollection<string>? leaveOut = null)
    {
        foreach (var entry in menu.Components)
        {
            if (!inPack(entry.GamePacks) || entry.Id != null && leaveOut != null && leaveOut.Contains(entry.Id))
                continue;
            DrawComponent(MenuFitting.Fitted(entry, menu.FitItems, fit), translate);
        }
    }

    /// <summary>One component</summary>
    public void DrawComponent(MenuComponent component, Func<string, string> translate)
    {
        switch (component)
        {
            case Background background:
                DrawBackground(background.Color);
                break;

            case Graphic graphic:
                if (string.IsNullOrEmpty(graphic.Name) || canvas.PictureSize(graphic.Name) is not { } size)
                    return;

                // Orientation overrides the explicit coordinate on that axis
                int x = graphic.HorizontalOrientation switch
                {
                    HorizontalOrientation.Center => 160 - size.Width / 2,
                    HorizontalOrientation.Right => 320 - size.Width,
                    _ => graphic.X
                };
                int y = graphic.VerticalOrientation switch
                {
                    VerticalOrientation.Center => 100 - size.Height / 2,
                    VerticalOrientation.Bottom => 200 - size.Height,
                    _ => graphic.Y
                };
                canvas.DrawPicture(graphic.Name, x, y);
                break;

            case Stripe stripe:
                DrawStripe(stripe.Y, stripe.BackingColor, stripe.LineColor);
                break;

            case Window window:
                canvas.Bar(window.X, window.Y, window.Width, window.Height, window.Color);
                DrawOutline(window.X, window.Y, window.Width, window.Height, "BORD2COLOR", "DEACTIVE");
                break;

            case Fill fill:
                canvas.Bar(fill.X, fill.Y, fill.Width, fill.Height, fill.Color);
                break;

            case Label label:
                DrawLabel(label, translate);
                break;
        }
    }

    /// <summary>A label: its text from x, or each line centered across its width (or the screen)</summary>
    public void DrawLabel(Label label, Func<string, string> translate)
    {
        var style = new TextStyle(label.Font, label.Color, Shadow: LabelShadow(label), Gradient: LabelGradient(label),
            Outline: LabelOutline(label), Glow: LabelGlow(label));

        var text = translate(label.Text);
        if (label.HorizontalOrientation == HorizontalOrientation.Center)
        {
            // Centered between X and X + Width, or across the screen. A line wider than that
            // runs past both sides rather than stopping the game (US_CPrintLine's Quit), since
            // it's a mod's text in a mod's font.
            var (left, width) = label.Width > 0 ? (label.X, label.Width) : (0, 320);
            int y = label.Y;
            foreach (var line in text.TrimEnd('\n').Split('\n'))
            {
                canvas.MeasureText(line, label.Font, out int w, out int h);
                TextWindow.At(canvas, left + (width - w) / 2, y, style).Print(line);
                y += h;
            }
        }
        else
            TextWindow.At(canvas, label.X, label.Y, style).Print(text);
    }

    /// <summary>A label's shadow: its own, none, or null to keep the font's</summary>
    public static FontShadow? LabelShadow(Label label)
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
    public static FontGradient? LabelGradient(Label label)
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
    public static FontOutline? LabelOutline(Label label)
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
    public static FontGlow? LabelGlow(Label label)
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

    // ------------------------------------------------------------------ rows

    /// <summary>Each row in view, highlighted where the cursor is</summary>
    public void DrawMenu(ControlPanelMenuInfo item_i, ControlPanelItem[] items)
    {
        int which = item_i.CurPos;

        for (int i = 0; i < item_i.Amount; i++)
            if (item_i.IsRowVisible(i))
                PrintMenuItem(item_i, items, i, items[i].Active > 0 ? MenuItemColor(items[i], which == i) : "DEACTIVE");
    }

    /// <summary>
    /// A scrolling menu's marks at its right edge (x): an arrow up by the first row shown and
    /// one down by the last while there are items above or below, each with how many (not
    /// counting blank rows: the episode menu's spacers)
    /// </summary>
    public void DrawMenuScrollMarks(ControlPanelMenuInfo item_i, ControlPanelItem[] items, int x)
    {
        if (!item_i.Scrolls)
            return;
        int bottom = Math.Min(item_i.TopRow + item_i.VisibleRows, items.Length);
        int above = items.Take(item_i.TopRow).Count(item => !string.IsNullOrEmpty(item.Text)),
            below = items.Skip(bottom).Count(item => !string.IsNullOrEmpty(item.Text));
        var style = new TextStyle(FontNames.Small, "READCOLOR");
        if (above > 0)
        {
            int y = item_i.RowY(item_i.TopRow) + 2;
            DrawCustomArrow(x, y, up: true);
            TextWindow.At(canvas, x - 6 - TextWidth($"{above} more", FontNames.Small), y - 3, style).Print($"{above} more");
        }
        if (below > 0)
        {
            int y = item_i.RowY(item_i.TopRow + item_i.VisibleRows - 1) + 5;
            DrawCustomArrow(x, y, up: false);
            TextWindow.At(canvas, x - 6 - TextWidth($"{below} more", FontNames.Small), y - 3, style).Print($"{below} more");
        }
    }

    private int TextWidth(string text, string font)
    {
        canvas.MeasureText(text, font, out int width, out _);
        return width;
    }

    /// <summary>An item's text in its row; a newline in it goes on under the start of the text</summary>
    public void PrintMenuItem(ControlPanelMenuInfo item_i, ControlPanelItem[] items, int which, string color)
        => TextWindow.At(canvas, item_i.X + item_i.Indent, MenuItemY(item_i, which), MenuItemStyle(item_i, color)).Print(items[which].Text);

    /// <summary>The top of an item's row</summary>
    public static int MenuItemY(ControlPanelMenuInfo item_i, int which) => item_i.RowY(which);

    /// <summary>The menu's text style for its items: its font and shadow (menudef font, item-shadow)</summary>
    public static TextStyle MenuItemStyle(ControlPanelMenuInfo item_i, string color)
        => new(item_i.Font ?? DefaultMenuFont, color,
            Shadow: item_i.ItemShadow is { } shadow ? new FontShadow(1, 1, shadow) : null);

    /// <summary>A row's color: highlighted (the cursor's on it) or not, by what it is (Active)</summary>
    public static string MenuItemColor(ControlPanelItem item, bool hlight)
        => hlight ? colorHlite[item.Active] : colorNorml[item.Active];

    /// <summary>
    /// Draws or erases a menu's highlight bar (menudef cursor) behind the item, printing the item
    /// over it highlighted, or plain when the bar is off
    /// </summary>
    public void DrawMenuBar(ControlPanelMenuInfo item_i, ControlPanelItem[] items, int which, bool on)
    {
        var bar = item_i.Cursor!;
        canvas.Bar(bar.X, MenuItemY(item_i, which) + bar.YOffset, bar.Width, bar.Height, on ? bar.Color : bar.EraseColor);
        PrintMenuItem(item_i, items, which, MenuItemColor(items[which], on));
        if (items[which].CheckBox is bool checkedOn)
            DrawMenuCheckbox(item_i, which, checkedOn, highlighted: on);
    }

    public const int MenuSliderWidth = 88;
    public const int MenuSliderOffset = 120; // from the start of the row's text, clear of "Sound Volume"; choices use it too

    /// <summary>
    /// A level bar to the right of a SliderMenuItem's text, drawn like the mouse sensitivity bar:
    /// a track with a highlighted knob at the current value (0 to max)
    /// </summary>
    public void DrawMenuSlider(ControlPanelMenuInfo iteminfo, ControlPanelItem[] items, string id, int value, int max)
    {
        int index = Array.FindIndex(items, item => item.Id == id);
        if (index < 0 || !iteminfo.IsRowVisible(index))
            return;

        int knobWidth = MenuSliderWidth / (max + 1);
        int x = iteminfo.X + iteminfo.Indent + MenuSliderOffset;
        int y = iteminfo.RowY(index) + 2;
        canvas.Bar(x, y, knobWidth * (max + 1), 9, "TEXTCOLOR");
        DrawOutline(x, y, knobWidth * (max + 1), 9, "Black", "HIGHLIGHT");
        DrawOutline(x + knobWidth * value, y, knobWidth, 9, "Black", "READCOLOR");
        canvas.Bar(x + knobWidth * value + 1, y + 1, knobWidth - 1, 8, "READHCOLOR");
    }

    public const int MenuChoiceWidth = 96; // room for the widest value, like "1920x1200"

    /// <summary>A ChoiceMenuItem's current value, in the same column as the sliders. Greyed out with the row.</summary>
    public void DrawMenuChoice(ControlPanelMenuInfo iteminfo, ControlPanelItem[] items, string id, string value)
    {
        int index = Array.FindIndex(items, item => item.Id == id);
        if (index < 0 || !iteminfo.IsRowVisible(index))
            return;

        int x = iteminfo.X + iteminfo.Indent + MenuSliderOffset;
        int y = iteminfo.RowY(index);
        canvas.Bar(x, y, MenuChoiceWidth, iteminfo.RowHeight, "BKGDCOLOR");

        TextWindow.At(canvas, x, y, MenuItemStyle(iteminfo, MenuItemColor(items[index], false))).Print(value);
    }

    /// <summary>
    /// The choice after stepping a ChoiceMenuItem by delta. Left/right stop at the ends of the
    /// list; Enter wraps round, so it can reach every value on its own.
    /// </summary>
    public static int StepMenuChoice(int current, int count, int delta, bool wrap)
    {
        if (count <= 0)
            return 0;

        int next = current + delta;
        return wrap ? ((next % count) + count) % count : Math.Clamp(next, 0, count - 1);
    }

    /// <summary>The on/off box to the left of a ToggleMenuItem's text, remembered on its row for the highlight bar</summary>
    public void DrawMenuCheckbox(ControlPanelMenuInfo iteminfo, ControlPanelItem[] items, string id, bool on)
    {
        int index = Array.FindIndex(items, item => item.Id == id);
        if (index >= 0)
        {
            items[index].CheckBox = on;
            DrawMenuCheckbox(iteminfo, index, on);
        }
    }

    /// <param name="highlighted">Under a highlight bar: the pack's c_selected_hi / c_notselected_hi, when it has them</param>
    public void DrawMenuCheckbox(ControlPanelMenuInfo iteminfo, int index, bool on, bool highlighted = false)
    {
        if (!iteminfo.IsRowVisible(index))
            return;
        int x = iteminfo.X + iteminfo.Indent + iteminfo.CheckboxX;
        int y = iteminfo.RowY(index) + iteminfo.CheckboxY;
        var pic = on ? "c_selected" : "c_notselected";
        if (highlighted && canvas.HasPicture(pic + "_hi"))
            pic += "_hi";
        canvas.DrawPicture(pic, x, y);
    }

    /// <summary>Wolf3D's gun cursor by the item the cursor's on, unless the menu has a highlight bar</summary>
    /// <param name="gun">Whether the game draws the gun at all (game-info menu-cursor isn't none)</param>
    public void DrawMenuGun(ControlPanelMenuInfo iteminfo, bool gun)
    {
        // A highlight bar goes on with its item's text when HandleMenu starts
        if (iteminfo.Cursor != null)
            return;

        int x = iteminfo.X & -8;    // same column HandleMenu draws and erases the gun in
        int y = iteminfo.RowY(iteminfo.CurPos) - 2;
        if (gun)
            canvas.DrawPicture("c_cursor1", x, y);
    }
}
