namespace Wolf3D;

internal struct CP_itemtype
{
    public CP_itemtype(short active, string text, Func<int, int>? routine)
    {
        this.active = active;
        this.text = text;
        this.routine = routine;
    }

    public short active;
    public string text;
    public Func<int, int>? routine;
}

internal struct CP_iteminfo
{
    public short x, y, amount, curpos, indent;
    public CP_iteminfo(short x, short y, short amount, short curpos, short indent)
    {
        this.x = x;
        this.y = y;
        this.amount = amount;
        this.curpos = curpos;
        this.indent = indent;
    }
}
