namespace PFWolf;

internal class ControlPanelItem
{
    public ControlPanelItem(short active, string text, Func<int, int>? routine)
    {
        this.Active = active;
        this.Text = text;
        this.Routine = routine;
        this.Data = null;
    }

    public ControlPanelItem(short active, string text, Func<int, int>? routine, object data)
    {
        this.Active = active;
        this.Text = text;
        this.Routine = routine;
        this.Data = data;
    }

    public short Active;
    public string Text;
    public Func<int, int>? Routine;
    public object? Data = null;
    public string? Id = null;
    /// <summary>
    /// Key that jumps to this item; 0 means use the first letter of the text
    /// </summary>
    public char ShortKey = '\0';
    /// <summary>The item's CheckBox as last drawn (on or off), so a highlight bar can draw it again; null for none</summary>
    public bool? CheckBox;
}

internal class ControlPanelMenuInfo
{
    public short X, Y, Amount, CurPos, Indent;

    // How the menu looks (its menudef): pixels between items, the items' font (null: the menu
    // font) and shadow color (null: none), and a highlight bar in place of the gun cursor
    public int RowHeight = 13;
    public string? Font;
    public string? ItemShadow;
    public Assets.MenuCursorInfo? Cursor;

    /// <summary>Where the highlighted item's picture goes; null for the menu's own placement</summary>
    public Assets.MenuPoint? SelectionPic;

    /// <summary>A toggle item's CheckBox, from its text's left and top</summary>
    public int CheckboxX = -24, CheckboxY = 3;

    /// <summary>How many items are shown at once (menudef visible-rows); 0 for all of them</summary>
    public int VisibleRows;

    /// <summary>The first item shown, when the menu scrolls (VisibleRows)</summary>
    public int TopRow;

    /// <summary>Whether the menu has more items than it shows at once</summary>
    public bool Scrolls => VisibleRows > 0 && Amount > VisibleRows;

    /// <summary>Whether an item is in view (always, unless the menu scrolls)</summary>
    public bool IsRowVisible(int which) => !Scrolls || which >= TopRow && which < TopRow + VisibleRows;

    /// <summary>The top of an item's row on the screen, counting from the first item shown</summary>
    public int RowY(int which) => Y + (which - (Scrolls ? TopRow : 0)) * RowHeight;

    /// <summary>Scrolls the least that brings an item into view; true if it scrolled</summary>
    public bool ScrollTo(int which)
    {
        if (!Scrolls)
            return false;
        int top = Math.Clamp(TopRow, Math.Max(which - VisibleRows + 1, 0), Math.Min(which, Amount - VisibleRows));
        if (top == TopRow)
            return false;
        TopRow = top;
        return true;
    }

    public ControlPanelMenuInfo(short x, short y, short amount, short curpos, short indent)
    {
        this.X = x;
        this.Y = y;
        this.Amount = amount;
        this.CurPos = curpos;
        this.Indent = indent;
    }
}
