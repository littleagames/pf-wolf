namespace PFWolf;

internal class CP_itemtype
{
    public CP_itemtype(short active, string text, Func<int, int>? routine)
    {
        this.active = active;
        this.text = text;
        this.routine = routine;
        this.data = null;
    }

    public CP_itemtype(short active, string text, Func<int, int>? routine, object data)
    {
        this.active = active;
        this.text = text;
        this.routine = routine;
        this.data = data;
    }

    public short active;
    public string text;
    public Func<int, int>? routine;
    public object? data = null;
    public string? id = null;
    /// <summary>
    /// Key that jumps to this item; 0 means use the first letter of the text
    /// </summary>
    public char shortKey = '\0';
    /// <summary>The item's checkbox as last drawn (on or off), so a highlight bar can draw it again; null for none</summary>
    public bool? checkbox;
}

internal class CP_iteminfo
{
    public short x, y, amount, curpos, indent;

    // How the menu looks (its menudef): pixels between items, the items' font (null: the menu
    // font) and shadow color (null: none), and a highlight bar in place of the gun cursor
    public int rowHeight = 13;
    public string? font;
    public string? itemShadow;
    public Assets.MenuCursorInfo? cursor;

    /// <summary>Where the highlighted item's picture goes; null for the menu's own placement</summary>
    public Assets.MenuPoint? selectionPic;

    /// <summary>A toggle item's checkbox, from its text's left and top</summary>
    public int checkboxX = -24, checkboxY = 3;

    public CP_iteminfo(short x, short y, short amount, short curpos, short indent)
    {
        this.x = x;
        this.y = y;
        this.amount = amount;
        this.curpos = curpos;
        this.indent = indent;
    }
}
