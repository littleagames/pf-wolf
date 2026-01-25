namespace Wolf3D;

internal partial class Program
{
    static int lasttimecount;
    static int frameon;
    static bool fpscounter;

    static int fps_frames = 0, fps_time = 0, fps = 0;

    internal static short[] wallheight;

    //
    // math tables
    //
    internal static short[] pixelangle;
    internal static int[] finetangent = new int[FINEANGLES / 4];
    internal static int[] sintable = new int[ANGLES + ANGLES / 4];
    internal static int[] costable = sintable[(ANGLES/4) ..]; // same as sintable, just offset by ANGLES/4

    //
    // refresh variables
    internal static int viewx, viewy;
    internal static short viewangle;
    internal static int viewsin, viewcos;

    internal static int postx;
    internal static byte[] postsource;

    //
    // ray tracing variables
    //
    internal static short focaltx, focalty;
    internal static uint xpartialup, xpartialdown, ypartialup, ypartialdown;

    internal static short midangle;

    internal static ushort tilehit;
    internal static int pixx;

    internal static short xtile, ytile;
    internal static short xtilestep, ytilestep;
    internal static int xintercept, yintercept;
    internal static int xinttile, yinttile;
    internal static ushort texdelta;

    internal static ushort[] horizwall = new ushort[MAXWALLTILES];
    internal static ushort[] vertwall = new ushort[MAXWALLTILES];
}
