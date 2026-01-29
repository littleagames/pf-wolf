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

internal enum controldirs
{
    di_north,
    di_east,
    di_south,
    di_west,
}
internal enum doortypes
{
    dr_normal,
    dr_lock1,
    dr_lock2,
    dr_lock3,
    dr_lock4,
    dr_elevator,
}

internal enum activetypes
{
    ac_badobject = -1,
    ac_no,
    ac_yes,
    ac_allways,
}

internal enum classtypes
{
    nothing,
    playerobj,
    inertobj,
    guardobj,
    officerobj,
    ssobj,
    dogobj,
    bossobj,
    schabbobj,
    fakeobj,
    mechahitlerobj,
    mutantobj,
    needleobj,
    fireobj,
    bjobj,
    ghostobj,
    realhitlerobj,
    gretelobj,
    giftobj,
    fatobj,
    rocketobj,

    spectreobj,
    angelobj,
    transobj,
    uberobj,
    willobj,
    deathobj,
    hrocketobj,
    sparkobj,
}

enum wl_stat_types
{
    none,
    block,
    bo_gibs,
    bo_alpo,
    bo_firstaid,
    bo_key1,
    bo_key2,
    bo_key3,
    bo_key4,
    bo_cross,
    bo_chalice,
    bo_bible,
    bo_crown,
    bo_clip,
    bo_clip2,
    bo_machinegun,
    bo_chaingun,
    bo_food,
    bo_fullheal,
    bo_25clip,
    bo_spear,
}

enum objdirtypes
{
    east,
    northeast,
    north,
    northwest,
    west,
    southwest,
    south,
    southeast,
    nodir,
}

enum enemytypes
{
    en_guard,
    en_officer,
    en_ss,
    en_dog,
    en_boss,
    en_schabbs,
    en_fake,
    en_hitler,
    en_mutant,
    en_blinky,
    en_clyde,
    en_pinky,
    en_inky,
    en_gretel,
    en_gift,
    en_fat,
    en_spectre,
    en_angel,
    en_trans,
    en_uber,
    en_will,
    en_death,

    NUMENEMIES
}

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
internal enum dooractiontypes
{
    dr_open, dr_closed, dr_opening, dr_closing,
};

internal class doorobj_t
{
    public byte tilex, tiley;
    public byte vertical; // boolean
    public byte locknum;
    public byte action;
    public short ticcount;
    public ushort position;            // leading edge of door (0 = closed, 0xffff = fully open)
}

internal class objstruct
{
    public byte active;
    public short ticcount;
    public byte obclass;
    public statestruct state;

    public uint flags;              // FL_SHOOTABLE, etc

    public int distance;           // if negative, wait for that door to open
    public byte dir;

    public int x, y;

    public byte tilex, tiley;
    public byte areanumber;

    public short viewx;
    public ushort viewheight;
    public int transx;             // in global coord

    public short angle;
    public short hitpoints;
    public int speed;

    public short temp1, temp2;
    public bool hidden;

    /**
    //
    // WARNING: DO NOT ADD ANY MEMBERS AFTER THESE!!!
    */
    objstruct next,prev;

    public objstruct()
    {
        active = (byte)activetypes.ac_no;
    }
}

internal class statestruct
{
    public byte rotate; // boolean
    public short shapenum;           // a shapenum of -1 means get from ob->temp1
    public short tictime;
    public Action<objstruct>? think;
    public Action<objstruct>? action;
    public statestruct next;

    public statestruct(
        byte rotate,
        short shapenum, 
        short tictime,
        Action<objstruct>? think,
        Action<objstruct>? action)
    {
        this.rotate = rotate;
        this.shapenum = shapenum;
        this.tictime = tictime;
        this.think = think;
        this.action = action;
    }
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

    internal static int DOORWALL => PMSpriteStart - 8;

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

    internal const int LRpack = 8; // # of levels to store in endgame

    internal const long PLAYERSIZE = MINDIST;// player radius
    internal const long MINACTORDIST = 0x10000L;// minimum dist from player center
                                                // to any actor center

    internal const float PI = 3.141592657f;
    internal const float M_PI = PI;
    internal const long GLOBAL1 = (1L << 16);
    internal const long TILEGLOBAL = GLOBAL1;
    internal const int TILESHIFT = 16;
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

    internal enum objflags
    {
        FL_SHOOTABLE = 0x00000001,
        FL_BONUS = 0x00000002,
        FL_NEVERMARK = 0x00000004,
        FL_VISABLE = 0x00000008,
        FL_ATTACKMODE = 0x00000010,
        FL_FIRSTATTACK = 0x00000020,
        FL_AMBUSH = 0x00000040,
        FL_NONMARK = 0x00000080,
        FL_FULLBRIGHT = 0x00000100
    }

    internal static void ClearMemory() => SD_StopDigitized();


    /*
    =============================================================================

                                    WL_INTER

    =============================================================================
    */


    internal struct LRstruct
    {
        public short kill, secret, treasure;
        public int time;
    }

    /*
    =============================================================================

                                WL_PLAY DEFINITIONS

    =============================================================================
    */

    internal static int BASEMOVE  = 35;
    internal static int RUNMOVE   = 70;
    internal static int BASETURN  = 35;
    internal static int RUNTURN = 70;

    internal static int JOYSCALE = 2;

    internal static int MAPSPOT(int x, int y, int plane) => (mapsegs[(plane)][((y) << MAPSHIFT) + (x)]);
    internal static void SetMapSpot(int x, int y, int plane, ushort value)
    {
        (mapsegs[(plane)][((y) << MAPSHIFT) + (x)]) = value;
    }
    internal static bool ISPOINTER(objstruct? x) => x != null; // TODO: How to? actorat was a list of pointers, vs just the objects themselves
}
