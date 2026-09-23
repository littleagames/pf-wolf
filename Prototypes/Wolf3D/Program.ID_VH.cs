namespace Wolf3D;


internal partial class Program
{
    static int px, py;
    [Obsolete("Should build this into each rendered item instead of using a global variable.")]
    static string fontcolor, backcolor;
    [Obsolete("Should build this into each rendered item instead of using a global variable.")]
    static string fontnumber;

    [Obsolete("Should build this into each rendered item instead of using a global variable.")]
    internal static void SETFONTCOLOR(string f, string b)
    {
        fontcolor = f;
        backcolor = b;
    }
}
