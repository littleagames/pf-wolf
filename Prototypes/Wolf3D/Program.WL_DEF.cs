using System.Runtime.InteropServices;

namespace Wolf3D;

internal class visobj_t
{
    public byte tilex, tiley;
    public short viewx;
    public short viewheight;
    public short shapenum;
    public uint flags;
}

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

internal class compshape_t
{
    public ushort leftpix, rightpix;
    public ushort[] dataofs;
    // table data after dataofs[rightpix-leftpix+1]

    public compshape_t()
    {
        dataofs = new ushort[64];
    }

    public compshape_t(byte[] data)
    {
        var offset = 0;
        leftpix = BitConverter.ToUInt16(data);
        offset += sizeof(ushort);

        rightpix = BitConverter.ToUInt16(data.Skip(offset).ToArray());
        offset += sizeof(ushort);

        dataofs = new ushort[64];
        for (int i = 0; i < 64; i++)
        {
            dataofs[i] = BitConverter.ToUInt16(data.Skip(offset).ToArray());
            offset += sizeof(ushort);
        }
    }
}


internal class statobj_t
{
    public byte tilex, tiley;
    public short shapenum;           // if shapenum == -1 the obj has been removed
    public uint flags;
    public byte itemnumber;
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
    internal const int MAXVISABLE = 250;

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

    internal enum spritenums
    {
        SPR_DEMO,
        SPR_DEATHCAM,
        //
        // static sprites
        //
        SPR_STAT_0, SPR_STAT_1, SPR_STAT_2, SPR_STAT_3,
        SPR_STAT_4, SPR_STAT_5, SPR_STAT_6, SPR_STAT_7,

        SPR_STAT_8, SPR_STAT_9, SPR_STAT_10, SPR_STAT_11,
        SPR_STAT_12, SPR_STAT_13, SPR_STAT_14, SPR_STAT_15,

        SPR_STAT_16, SPR_STAT_17, SPR_STAT_18, SPR_STAT_19,
        SPR_STAT_20, SPR_STAT_21, SPR_STAT_22, SPR_STAT_23,

        SPR_STAT_24, SPR_STAT_25, SPR_STAT_26, SPR_STAT_27,
        SPR_STAT_28, SPR_STAT_29, SPR_STAT_30, SPR_STAT_31,

        SPR_STAT_32, SPR_STAT_33, SPR_STAT_34, SPR_STAT_35,
        SPR_STAT_36, SPR_STAT_37, SPR_STAT_38, SPR_STAT_39,

        SPR_STAT_40, SPR_STAT_41, SPR_STAT_42, SPR_STAT_43,
        SPR_STAT_44, SPR_STAT_45, SPR_STAT_46, SPR_STAT_47,

        //
        // guard
        //
        SPR_GRD_S_1, SPR_GRD_S_2, SPR_GRD_S_3, SPR_GRD_S_4,
        SPR_GRD_S_5, SPR_GRD_S_6, SPR_GRD_S_7, SPR_GRD_S_8,

        SPR_GRD_W1_1, SPR_GRD_W1_2, SPR_GRD_W1_3, SPR_GRD_W1_4,
        SPR_GRD_W1_5, SPR_GRD_W1_6, SPR_GRD_W1_7, SPR_GRD_W1_8,

        SPR_GRD_W2_1, SPR_GRD_W2_2, SPR_GRD_W2_3, SPR_GRD_W2_4,
        SPR_GRD_W2_5, SPR_GRD_W2_6, SPR_GRD_W2_7, SPR_GRD_W2_8,

        SPR_GRD_W3_1, SPR_GRD_W3_2, SPR_GRD_W3_3, SPR_GRD_W3_4,
        SPR_GRD_W3_5, SPR_GRD_W3_6, SPR_GRD_W3_7, SPR_GRD_W3_8,

        SPR_GRD_W4_1, SPR_GRD_W4_2, SPR_GRD_W4_3, SPR_GRD_W4_4,
        SPR_GRD_W4_5, SPR_GRD_W4_6, SPR_GRD_W4_7, SPR_GRD_W4_8,

        SPR_GRD_PAIN_1, SPR_GRD_DIE_1, SPR_GRD_DIE_2, SPR_GRD_DIE_3,
        SPR_GRD_PAIN_2, SPR_GRD_DEAD,

        SPR_GRD_SHOOT1, SPR_GRD_SHOOT2, SPR_GRD_SHOOT3,

        //
        // dogs
        //
        SPR_DOG_W1_1, SPR_DOG_W1_2, SPR_DOG_W1_3, SPR_DOG_W1_4,
        SPR_DOG_W1_5, SPR_DOG_W1_6, SPR_DOG_W1_7, SPR_DOG_W1_8,

        SPR_DOG_W2_1, SPR_DOG_W2_2, SPR_DOG_W2_3, SPR_DOG_W2_4,
        SPR_DOG_W2_5, SPR_DOG_W2_6, SPR_DOG_W2_7, SPR_DOG_W2_8,

        SPR_DOG_W3_1, SPR_DOG_W3_2, SPR_DOG_W3_3, SPR_DOG_W3_4,
        SPR_DOG_W3_5, SPR_DOG_W3_6, SPR_DOG_W3_7, SPR_DOG_W3_8,

        SPR_DOG_W4_1, SPR_DOG_W4_2, SPR_DOG_W4_3, SPR_DOG_W4_4,
        SPR_DOG_W4_5, SPR_DOG_W4_6, SPR_DOG_W4_7, SPR_DOG_W4_8,

        SPR_DOG_DIE_1, SPR_DOG_DIE_2, SPR_DOG_DIE_3, SPR_DOG_DEAD,
        SPR_DOG_JUMP1, SPR_DOG_JUMP2, SPR_DOG_JUMP3,



        //
        // ss
        //
        SPR_SS_S_1, SPR_SS_S_2, SPR_SS_S_3, SPR_SS_S_4,
        SPR_SS_S_5, SPR_SS_S_6, SPR_SS_S_7, SPR_SS_S_8,

        SPR_SS_W1_1, SPR_SS_W1_2, SPR_SS_W1_3, SPR_SS_W1_4,
        SPR_SS_W1_5, SPR_SS_W1_6, SPR_SS_W1_7, SPR_SS_W1_8,

        SPR_SS_W2_1, SPR_SS_W2_2, SPR_SS_W2_3, SPR_SS_W2_4,
        SPR_SS_W2_5, SPR_SS_W2_6, SPR_SS_W2_7, SPR_SS_W2_8,

        SPR_SS_W3_1, SPR_SS_W3_2, SPR_SS_W3_3, SPR_SS_W3_4,
        SPR_SS_W3_5, SPR_SS_W3_6, SPR_SS_W3_7, SPR_SS_W3_8,

        SPR_SS_W4_1, SPR_SS_W4_2, SPR_SS_W4_3, SPR_SS_W4_4,
        SPR_SS_W4_5, SPR_SS_W4_6, SPR_SS_W4_7, SPR_SS_W4_8,

        SPR_SS_PAIN_1, SPR_SS_DIE_1, SPR_SS_DIE_2, SPR_SS_DIE_3,
        SPR_SS_PAIN_2, SPR_SS_DEAD,

        SPR_SS_SHOOT1, SPR_SS_SHOOT2, SPR_SS_SHOOT3,

        //
        // mutant
        //
        SPR_MUT_S_1, SPR_MUT_S_2, SPR_MUT_S_3, SPR_MUT_S_4,
        SPR_MUT_S_5, SPR_MUT_S_6, SPR_MUT_S_7, SPR_MUT_S_8,

        SPR_MUT_W1_1, SPR_MUT_W1_2, SPR_MUT_W1_3, SPR_MUT_W1_4,
        SPR_MUT_W1_5, SPR_MUT_W1_6, SPR_MUT_W1_7, SPR_MUT_W1_8,

        SPR_MUT_W2_1, SPR_MUT_W2_2, SPR_MUT_W2_3, SPR_MUT_W2_4,
        SPR_MUT_W2_5, SPR_MUT_W2_6, SPR_MUT_W2_7, SPR_MUT_W2_8,

        SPR_MUT_W3_1, SPR_MUT_W3_2, SPR_MUT_W3_3, SPR_MUT_W3_4,
        SPR_MUT_W3_5, SPR_MUT_W3_6, SPR_MUT_W3_7, SPR_MUT_W3_8,

        SPR_MUT_W4_1, SPR_MUT_W4_2, SPR_MUT_W4_3, SPR_MUT_W4_4,
        SPR_MUT_W4_5, SPR_MUT_W4_6, SPR_MUT_W4_7, SPR_MUT_W4_8,

        SPR_MUT_PAIN_1, SPR_MUT_DIE_1, SPR_MUT_DIE_2, SPR_MUT_DIE_3,
        SPR_MUT_PAIN_2, SPR_MUT_DIE_4, SPR_MUT_DEAD,

        SPR_MUT_SHOOT1, SPR_MUT_SHOOT2, SPR_MUT_SHOOT3, SPR_MUT_SHOOT4,

        //
        // officer
        //
        SPR_OFC_S_1, SPR_OFC_S_2, SPR_OFC_S_3, SPR_OFC_S_4,
        SPR_OFC_S_5, SPR_OFC_S_6, SPR_OFC_S_7, SPR_OFC_S_8,

        SPR_OFC_W1_1, SPR_OFC_W1_2, SPR_OFC_W1_3, SPR_OFC_W1_4,
        SPR_OFC_W1_5, SPR_OFC_W1_6, SPR_OFC_W1_7, SPR_OFC_W1_8,

        SPR_OFC_W2_1, SPR_OFC_W2_2, SPR_OFC_W2_3, SPR_OFC_W2_4,
        SPR_OFC_W2_5, SPR_OFC_W2_6, SPR_OFC_W2_7, SPR_OFC_W2_8,

        SPR_OFC_W3_1, SPR_OFC_W3_2, SPR_OFC_W3_3, SPR_OFC_W3_4,
        SPR_OFC_W3_5, SPR_OFC_W3_6, SPR_OFC_W3_7, SPR_OFC_W3_8,

        SPR_OFC_W4_1, SPR_OFC_W4_2, SPR_OFC_W4_3, SPR_OFC_W4_4,
        SPR_OFC_W4_5, SPR_OFC_W4_6, SPR_OFC_W4_7, SPR_OFC_W4_8,

        SPR_OFC_PAIN_1, SPR_OFC_DIE_1, SPR_OFC_DIE_2, SPR_OFC_DIE_3,
        SPR_OFC_PAIN_2, SPR_OFC_DIE_4, SPR_OFC_DEAD,

        SPR_OFC_SHOOT1, SPR_OFC_SHOOT2, SPR_OFC_SHOOT3,

        //
        // ghosts
        //
        SPR_BLINKY_W1, SPR_BLINKY_W2, SPR_PINKY_W1, SPR_PINKY_W2,
        SPR_CLYDE_W1, SPR_CLYDE_W2, SPR_INKY_W1, SPR_INKY_W2,

        //
        // hans
        //
        SPR_BOSS_W1, SPR_BOSS_W2, SPR_BOSS_W3, SPR_BOSS_W4,
        SPR_BOSS_SHOOT1, SPR_BOSS_SHOOT2, SPR_BOSS_SHOOT3, SPR_BOSS_DEAD,

        SPR_BOSS_DIE1, SPR_BOSS_DIE2, SPR_BOSS_DIE3,

        //
        // schabbs
        //
        SPR_SCHABB_W1, SPR_SCHABB_W2, SPR_SCHABB_W3, SPR_SCHABB_W4,
        SPR_SCHABB_SHOOT1, SPR_SCHABB_SHOOT2,

        SPR_SCHABB_DIE1, SPR_SCHABB_DIE2, SPR_SCHABB_DIE3, SPR_SCHABB_DEAD,
        SPR_HYPO1, SPR_HYPO2, SPR_HYPO3, SPR_HYPO4,

        //
        // fake
        //
        SPR_FAKE_W1, SPR_FAKE_W2, SPR_FAKE_W3, SPR_FAKE_W4,
        SPR_FAKE_SHOOT, SPR_FIRE1, SPR_FIRE2,

        SPR_FAKE_DIE1, SPR_FAKE_DIE2, SPR_FAKE_DIE3, SPR_FAKE_DIE4,
        SPR_FAKE_DIE5, SPR_FAKE_DEAD,

        //
        // hitler
        //
        SPR_MECHA_W1, SPR_MECHA_W2, SPR_MECHA_W3, SPR_MECHA_W4,
        SPR_MECHA_SHOOT1, SPR_MECHA_SHOOT2, SPR_MECHA_SHOOT3, SPR_MECHA_DEAD,

        SPR_MECHA_DIE1, SPR_MECHA_DIE2, SPR_MECHA_DIE3,

        SPR_HITLER_W1, SPR_HITLER_W2, SPR_HITLER_W3, SPR_HITLER_W4,
        SPR_HITLER_SHOOT1, SPR_HITLER_SHOOT2, SPR_HITLER_SHOOT3, SPR_HITLER_DEAD,

        SPR_HITLER_DIE1, SPR_HITLER_DIE2, SPR_HITLER_DIE3, SPR_HITLER_DIE4,
        SPR_HITLER_DIE5, SPR_HITLER_DIE6, SPR_HITLER_DIE7,

        //
        // giftmacher
        //
        SPR_GIFT_W1, SPR_GIFT_W2, SPR_GIFT_W3, SPR_GIFT_W4,
        SPR_GIFT_SHOOT1, SPR_GIFT_SHOOT2,

        SPR_GIFT_DIE1, SPR_GIFT_DIE2, SPR_GIFT_DIE3, SPR_GIFT_DEAD,
        //
        // Rocket, smoke and small explosion
        //
        SPR_ROCKET_1, SPR_ROCKET_2, SPR_ROCKET_3, SPR_ROCKET_4,
        SPR_ROCKET_5, SPR_ROCKET_6, SPR_ROCKET_7, SPR_ROCKET_8,

        SPR_SMOKE_1, SPR_SMOKE_2, SPR_SMOKE_3, SPR_SMOKE_4,
        SPR_BOOM_1, SPR_BOOM_2, SPR_BOOM_3,

        //
        // gretel
        //
        SPR_GRETEL_W1, SPR_GRETEL_W2, SPR_GRETEL_W3, SPR_GRETEL_W4,
        SPR_GRETEL_SHOOT1, SPR_GRETEL_SHOOT2, SPR_GRETEL_SHOOT3, SPR_GRETEL_DEAD,

        SPR_GRETEL_DIE1, SPR_GRETEL_DIE2, SPR_GRETEL_DIE3,

        //
        // fat face
        //
        SPR_FAT_W1, SPR_FAT_W2, SPR_FAT_W3, SPR_FAT_W4,
        SPR_FAT_SHOOT1, SPR_FAT_SHOOT2, SPR_FAT_SHOOT3, SPR_FAT_SHOOT4,

        SPR_FAT_DIE1, SPR_FAT_DIE2, SPR_FAT_DIE3, SPR_FAT_DEAD,

        //
        // bj
        //
        SPR_BJ_W1, SPR_BJ_W2, SPR_BJ_W3, SPR_BJ_W4,
        SPR_BJ_JUMP1, SPR_BJ_JUMP2, SPR_BJ_JUMP3, SPR_BJ_JUMP4,

        //
        // player attack frames
        //
        SPR_KNIFEREADY, SPR_KNIFEATK1, SPR_KNIFEATK2, SPR_KNIFEATK3,
        SPR_KNIFEATK4,

        SPR_PISTOLREADY, SPR_PISTOLATK1, SPR_PISTOLATK2, SPR_PISTOLATK3,
        SPR_PISTOLATK4,

        SPR_MACHINEGUNREADY, SPR_MACHINEGUNATK1, SPR_MACHINEGUNATK2, MACHINEGUNATK3,
        SPR_MACHINEGUNATK4,

        SPR_CHAINREADY, SPR_CHAINATK1, SPR_CHAINATK2, SPR_CHAINATK3,
        SPR_CHAINATK4,
    }

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
