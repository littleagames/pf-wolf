using System;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Wolf3D;

internal enum buttontypes
{
    bt_nobutton = -1,
    bt_attack = 0,
    bt_strafe,
    bt_run,
    bt_use,
    bt_readyknife,
    bt_readypistol,
    bt_readymachinegun,
    bt_readychaingun,
    bt_nextweapon,
    bt_prevweapon,
    bt_esc,
    bt_pause,
    bt_strafeleft,
    bt_straferight,
    bt_moveforward,
    bt_movebackward,
    bt_turnleft,
    bt_turnright,

    NUMBUTTONS
};

internal enum weapontypes
{
    wp_knife,
    wp_pistol,
    wp_machinegun,
    wp_chaingun,
    NUMWEAPONS
};

internal enum difficultytypes
{
    gd_baby,
    gd_easy,
    gd_medium,
    gd_hard
};

//---------------
//
// gamestate structure
//
//---------------
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct gametype
{
    public short difficulty;
    public short mapon;
    public int oldscore, score, nextextra;
    public short lives;
    public short health;
    public short ammo;
    public short keys;
    public short bestweapon, weapon, chosenweapon;

    public short faceframe;
    public short attackframe, attackcount, weaponframe;

    public short episode, secretcount, treasurecount, killcount,
                secrettotal, treasuretotal, killtotal;
    public int TimeCount;
    public int killx, killy;
    public byte victoryflag;            // set during victory animations
}

internal struct objstruct
{
    byte active;
    short ticcount;
    byte obclass;
    statestruct[] state;

    uint flags;              // FL_SHOOTABLE, etc

    int distance;           // if negative, wait for that door to open
    byte dir;

    int x, y;

    byte tilex, tiley;
    byte areanumber;

    short viewx;
    ushort viewheight;
    int transx;             // in global coord

    short angle;
    short hitpoints;
    int speed;

    short temp1, temp2;
    bool hidden;

    /**
    //
    // WARNING: DO NOT ADD ANY MEMBERS AFTER THESE!!!
    */
    //objtype next,prev;
}

internal struct statestruct
{
    byte rotate; // boolean
    short shapenum;           // a shapenum of -1 means get from ob->temp1
    short tictime;
    //void (* think) (objtype*),(* action) (objtype*);
    //struct statestruct *next;
}

internal enum playstatetypes
{
    ex_stillplaying,
    ex_completed,
    ex_died,
    ex_warped,
    ex_resetgame,
    ex_loadedgame,
    ex_victorious,
    ex_abort,
    ex_demodone,
    ex_secretlevel,
}

internal partial class Program
{
    internal const string YESBUTTONNAME = "Y";
    internal const string NOBUTTONNAME = "N";

    internal const float PI = 3.141592657f;
    internal const long GLOBAL1 = (1L << 16);
    internal const long TILEGLOBAL = GLOBAL1;
    internal const long TILESHIFT = 16L;
    internal const int UNSIGNEDSHIFT = 8;

    internal const int ANGLES = 360;
    internal const int ANGLEQUAD = (ANGLES / 4);
    internal const int FINEANGLES = 3600;
    internal const int ANG90 = (FINEANGLES / 4);
    internal const int ANG180 = ANG90 * 2;
    internal const int ANG270 = ANG90 * 3;
    internal const int ANG360 = ANG90 * 4;
    internal const int VANG90 = (ANGLES / 4);
    internal const int VANG180 = VANG90 * 2;
    internal const int VANG270 = VANG90 * 3;
    internal const int VANG360 = VANG90 * 4;

    internal const long MINDIST = 0x5800L;

    internal const int MAPSHIFT = 6;
    internal const int MAPSIZE = (1 << MAPSHIFT);
    internal const int MAPAREA = MAPSIZE * MAPSIZE;

    internal const int TEXTURESHIFT = 6;
    internal const int FIXED2TEXSHIFT = (TEXTURESHIFT == 8) ? 0 : (TEXTURESHIFT == 7 ? 2 : 4);
    internal const int TEXTURESIZE = (1 << TEXTURESHIFT);
    internal const int TEXTUREMASK = (TEXTURESIZE * (TEXTURESIZE - 1));

    internal const int NORTH = 0;
    internal const int EAST = 1;
    internal const int SOUTH = 2;
    internal const int WEST = 3;

    internal const int STATUSLINES = 40;
    internal const int STARTAMMO = 8;


    /*
    =============================================================================

                                GLOBAL CONSTANTS

    =============================================================================
    */
    internal const int MAXTICS = 10;
    internal const int DEMOTICS = 4;

    internal const int WALLSHIFT = 6;
    internal const int BIT_WALL = (1 << WALLSHIFT);
    internal const int BIT_DOOR = (1 << (WALLSHIFT + 1));
    internal const int BIT_ALLTILES = (1 << (WALLSHIFT + 2));

    internal readonly int DOORWALL = PMSpriteStart - 8;

    internal const int MAXACTORS = 150;
    internal const int MAXSTATS = 400;
    internal const int MAXDOORS = 64;
    internal const int MAXWALLTILES = 64;
    internal const int MAXVISIBLE = 250;

    //
    // tile constants
    //

    internal const int ICONARROWS = 90;
    internal const int PUSHABLETILE = 98;
    internal const int EXITTILE = 99;          // at end of castle
    internal const int AREATILE = 107;         // first of NUMAREAS floor tiles
    internal const int NUMAREAS = 37;
    internal const int ELEVATORTILE = 21;
    internal const int AMBUSHTILE = 106;
    internal const int ALTELEVATORTILE = 107;

    internal const int NUMBERCHARS = 9;


    //----------------

    internal const int EXTRAPOINTS = 40000;

    internal const int RUNSPEED = 6000;

    internal const float HEIGHTRATIO = 0.50f;

    internal static void ClearMemory() => SD_StopDigitized();
}
