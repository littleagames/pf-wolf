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

    public ControlPanelMenuInfo(short x, short y, short amount, short curpos, short indent)
    {
        this.X = x;
        this.Y = y;
        this.Amount = amount;
        this.CurPos = curpos;
        this.Indent = indent;
    }
}
