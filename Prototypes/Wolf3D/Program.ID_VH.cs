namespace Wolf3D;


internal partial class Program
{
    internal static int WHITE = 15;			// graphics mode independant colors
    internal static int BLACK		= 0;
    internal static int FIRSTCOLOR	= 1;
    internal static int SECONDCOLOR	= 12;
    internal static int F_WHITE		= 15;
    internal static int F_BLACK		= 0;
    internal static int F_FIRSTCOLOR= 1;
    internal static int F_SECONDCOLOR = 12;

    static int px, py;
    static byte fontcolor, backcolor;
    static int fontnumber;

    internal static void SETFONTCOLOR(byte f, byte b)
    {
        fontcolor = f;
        backcolor = b;
    }

}
