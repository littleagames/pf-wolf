using System.Runtime.InteropServices;
using static Wolf3D.Program;

namespace Wolf3D;

internal struct Point
{
    public int x, y;
}

internal struct Rect
{
    public Point ul, lr;
}

internal class visobj_t
{
    public byte tilex, tiley;
    public short viewx;
    public short viewheight;
    public string shapenum;
    public objflags flags;
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
    wp_none = -1,
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
internal class gametype
{
    public difficultytypes difficulty;
    public string mapon;
    public int oldscore, score, nextextra;
    public short lives;
    public short health;
    public short ammo;
    public short keys;
    public weapontypes bestweapon, weapon, chosenweapon;

    public short faceframe;
    public short attackframe, attackcount, weaponframe;

    public short cluster, secretcount, treasurecount, killcount,
                secrettotal, treasuretotal, killtotal;
    public int TimeCount;
    public int killx, killy;
    public bool victoryflag;            // set during victory animations

    public void Read(BinaryReader br)
    {
        difficulty = (difficultytypes)br.ReadInt16();
        mapon = br.ReadString();
        oldscore = br.ReadInt32();
        score = br.ReadInt32();
        nextextra = br.ReadInt32();
        lives = br.ReadInt16();
        health = br.ReadInt16();
        ammo = br.ReadInt16();
        keys = br.ReadInt16();
        bestweapon = (weapontypes)br.ReadInt16();
        weapon = (weapontypes)br.ReadInt16();
        chosenweapon = (weapontypes)br.ReadInt16();
        faceframe = br.ReadInt16();
        attackframe = br.ReadInt16();
        attackcount = br.ReadInt16();
        weaponframe = br.ReadInt16();
        cluster = br.ReadInt16();
        secretcount = br.ReadInt16();
        treasurecount = br.ReadInt16();
        killcount = br.ReadInt16();
        secrettotal = br.ReadInt16();
        treasuretotal = br.ReadInt16();
        killtotal = br.ReadInt16();
        TimeCount = br.ReadInt32();
        killx = br.ReadInt32();
        killy = br.ReadInt32();
        victoryflag = br.ReadByte() > 0;
    }

    public byte[] AsBytes()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        {
            bw.Write((byte)difficulty);
            bw.Write(mapon);
            bw.Write(oldscore);
            bw.Write(score);
            bw.Write(nextextra);
            bw.Write(lives);
            bw.Write(health);
            bw.Write(ammo);
            bw.Write(keys);
            bw.Write((byte)bestweapon);
            bw.Write((byte)weapon);
            bw.Write((byte)chosenweapon);
            bw.Write(faceframe);
            bw.Write(attackframe);
            bw.Write(attackcount);
            bw.Write(weaponframe);
            bw.Write(cluster);
            bw.Write(secretcount);
            bw.Write(treasurecount);
            bw.Write(killcount);
            bw.Write(secrettotal);
            bw.Write(treasuretotal);
            bw.Write(killtotal);
            bw.Write(TimeCount);
            bw.Write(killx);
            bw.Write(killy);
            bw.Write((byte)(victoryflag ? 1 : 0));
            return ms.ToArray();
        }
    }
}

internal struct compshape_t
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
    public string shapenum;           // if shapenum == -1 the obj has been removed
    public objflags flags;
    public wl_stat_types itemnumber;

    public void Read(BinaryReader br)
    {
        tilex = br.ReadByte();
        tiley = br.ReadByte();
        shapenum = br.ReadString();
        flags = (objflags)br.ReadUInt32();
        itemnumber = (wl_stat_types)br.ReadByte();
    }

    public byte[] AsBytes()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        {
            bw.Write(tilex);
            bw.Write(tiley);
            bw.Write(shapenum);
            bw.Write((uint)flags);
            bw.Write((byte)itemnumber);
            return ms.ToArray();
        }
    }
}

internal enum dooractiontypes
{
    dr_open, dr_closed, dr_opening, dr_closing,
};

internal class doorobj_t
{
    public sbyte tilex, tiley;
    public bool vertical;
    public sbyte locknum;
    public dooractiontypes action;
    public short ticcount;
    public ushort position;            // leading edge of door (0 = closed, 0xffff = fully open)

    public void Read(BinaryReader br)
    {
        tilex = br.ReadSByte();
        tiley = br.ReadSByte();
        vertical = Convert.ToBoolean(br.ReadByte());
        locknum = br.ReadSByte();
        action = (dooractiontypes)br.ReadSByte();
        ticcount = br.ReadInt16();
        position = br.ReadUInt16();
    }

    public byte[] AsBytes()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        {
            bw.Write(tilex);
            bw.Write(tiley);
            bw.Write((byte)(vertical ? 1 : 0));
            bw.Write(locknum);
            bw.Write((sbyte)action);
            bw.Write(ticcount);
            bw.Write(position);
            return ms.ToArray();
        }
    }
}

internal abstract class Actor
{
    // TBD: Use for actorat
    public int x, y;
}

internal class BlockingActor: Actor
{
}

internal class Wall: Actor
{
    public Wall()
    {
        wall = -1;
    }
    public Wall(int wall)
    {
        this.wall = wall;
    }

    public int wall;
}

internal class Door: Actor
{
    public Door()
    {
        this.door = -1;
    }

    public Door(int doornum)
    {
        this.door = doornum;
    }

    public int door;
}

internal class objstruct : Actor
{
    public activetypes active;
    public short ticcount;
    public classtypes obclass;
    public statestruct? state;

    public objflags flags;              // FL_SHOOTABLE, etc

    public int distance;           // if negative, wait for that door to open
    public objdirtypes dir;


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
    public int? next,prev;

    public objstruct()
    {
        active = (byte)activetypes.ac_no;
    }

    public int Read(BinaryReader br)
    {
        active = (activetypes)br.ReadSByte();
        ticcount = br.ReadInt16();
        obclass = (classtypes)br.ReadInt16();
        int stateOffset = br.ReadInt32();
        flags = (objflags)br.ReadUInt32();
        distance = br.ReadInt32();
        dir = (objdirtypes)br.ReadByte();
        x = br.ReadInt32();
        y = br.ReadInt32();
        tilex = br.ReadByte();
        tiley = br.ReadByte();
        areanumber = br.ReadByte();
        viewx = br.ReadInt16();
        viewheight = br.ReadUInt16();
        transx = br.ReadInt32();
        angle = br.ReadInt16();
        hitpoints = br.ReadInt16();
        speed = br.ReadInt32();
        temp1 = br.ReadInt16();
        temp2 = br.ReadInt16();
        hidden = br.ReadByte() != 0;
        int nextVal = br.ReadInt32();
        next = nextVal != 0 ? nextVal : null;
        int prevVal = br.ReadInt32();
        prev = prevVal != 0 ? prevVal : null;

        return stateOffset;
    }

    public void Copy(objstruct source)
    {
        active = source.active;
        ticcount = source.ticcount;
        obclass = source.obclass;
        flags = source.flags;
        distance = source.distance;
        dir = source.dir;
        x = source.x;
        y = source.y;
        tilex = source.tilex;
        tiley = source.tiley;
        areanumber = source.areanumber;
        viewx = source.viewx;
        viewheight = source.viewheight;
        transx = source.transx;
        angle = source.angle;
        hitpoints = source.hitpoints;
        speed = source.speed;
        temp1 = source.temp1;
        temp2 = source.temp2;
        hidden = source.hidden;
    }

    public byte[] AsBytes(int stateOffset)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        {
            bw.Write((sbyte)active);
            bw.Write(ticcount);
            bw.Write((short)obclass);
            bw.Write(stateOffset);
            bw.Write((uint)flags);
            bw.Write(distance);
            bw.Write((byte)dir);
            bw.Write(x);
            bw.Write(y);
            bw.Write(tilex);
            bw.Write(tiley);
            bw.Write(areanumber);
            bw.Write(viewx);
            bw.Write(viewheight);
            bw.Write(transx);
            bw.Write(angle);
            bw.Write(hitpoints);
            bw.Write(speed);
            bw.Write(temp1);
            bw.Write(temp2);
            bw.Write(hidden);
            bw.Write(next ?? 0);
            bw.Write(prev ?? 0);
            return ms.ToArray();
        }
    }
}

internal class statestruct
{
    public byte rotate; // boolean
    public string shapenum;           // a shapenum of -1 means get from ob->temp1
    public short tictime;
    public Action<objstruct>? think;
    public Action<objstruct>? action;
    public string? next;

    public statestruct(
        byte rotate,
        string shapenum, 
        short tictime,
        Action<objstruct>? think,
        Action<objstruct>? action,
        string? next)
    {
        this.rotate = rotate;
        this.shapenum = shapenum;
        this.tictime = tictime;
        this.think = think;
        this.action = action;
        this.next = next;
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
    /*
    =============================================================================

                                MACROS

    =============================================================================
    */

    internal const string YESBUTTONNAME = "Y";
    internal const string NOBUTTONNAME = "N";

    internal const int DEFAULT_AUDIO_BUFFER_SIZE = 2048;

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

    internal const int NUMBERCHARS = 9;


    //----------------

    internal const int EXTRAPOINTS = 40000;

    internal const int RUNSPEED = 6000;

    internal const float HEIGHTRATIO = 0.50f;

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

    [Flags]
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
        FL_FULLBRIGHT = 0x00000100,
#if USE_DIR3DSPR
        // you can choose one of the following values in wl_act1.cpp
        // to make a static sprite a directional 3d sprite
        // (see example at the end of the statinfo array)
        FL_DIR_HORIZ_MID = 0x00000200,
        FL_DIR_HORIZ_FW = 0x00000400,
        FL_DIR_HORIZ_BW = 0x00000600,
        FL_DIR_VERT_MID = 0x00000a00,
        FL_DIR_VERT_FW = 0x00000c00,
        FL_DIR_VERT_BW = 0x00000e00,

        // these values are just used to improve readability of code
        FL_DIR_NONE = 0x00000000,
        FL_DIR_POS_MID = 0x00000200,
        FL_DIR_POS_FW = 0x00000400,
        FL_DIR_POS_BW = 0x00000600,
        FL_DIR_POS_MASK = 0x00000600,
        FL_DIR_VERT_FLAG = 0x00000800,
        FL_DIR_MASK = 0x00000e00,
#endif
        // next free bit is   0x00001000
    }

    internal static void ClearMemory() => SD_StopDigitized();

    // JAB
    internal static void PlaySoundLocTile(int s, int tx, int ty) => PlaySoundLocGlobal(s, (int)((tx << TILESHIFT) + (TILEGLOBAL / 2)), (int)((ty << TILESHIFT) + (TILEGLOBAL / 2)));
    internal static void PlaySoundLocActor(int s, objstruct ob) => PlaySoundLocGlobal(s, ob.x, ob.y);


    /*
    =============================================================================

                                    WL_INTER

    =============================================================================
    */


    internal class LRstruct
    {
        public short kill, secret, treasure;
        public int time;

        public void Read(BinaryReader br)
        {
            kill = br.ReadInt16();
            secret = br.ReadInt16();
            treasure = br.ReadInt16();
            time = br.ReadInt32();
        }

        public byte[] AsBytes()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            {
                bw.Write(kill);
                bw.Write(secret);
                bw.Write(treasure);
                bw.Write(time);
                return ms.ToArray();
            }
        }
    }

    /*
    =============================================================================

                                WL_PLAY DEFINITIONS

    =============================================================================
    */

    internal const int BASEMOVE  = 35;
    internal const int RUNMOVE   = 70;
    internal const int BASETURN  = 35;
    internal const int RUNTURN = 70;

    internal const int JOYSCALE = 2;





    /*
    =============================================================================

                                 WL_STATE DEFINITIONS

    =============================================================================
    */
    internal const int TURNTICS = 10;
    internal const int SPDPATROL = 512;
    internal const int SPDDOG = 1500;
}

/*
=============================================================================

                           FEATURE DEFINITIONS

=============================================================================
*/

#if USE_FEATUREFLAGS
    // The currently available feature flags
    internal const int FF_STARSKY      = 0x0001;
    internal const int FF_PARALLAXSKY  = 0x0002;
    internal const int FF_CLOUDSKY     = 0x0004;
    internal const int FF_RAIN         = 0x0010;
        internal const int FF_SNOW         = 0x0020;
    static ushort GetFeatureFlags ()
    {
        return ffDataTopRight;
    }
#endif