using System.Runtime.InteropServices;
using Wolf3D.Assets;
using Wolf3D.Constants;
using static Wolf3D.Program;

namespace Wolf3D;

internal struct Point
{
    public int x, y;

    public static Point Zero => new Point { x = 0, y = 0 };
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
    bt_automap,

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
internal enum activetypes
{
    ac_no,
    ac_yes,
    ac_allways,
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
    public weapontypes weapon, chosenweapon;

    public short faceframe;
    public short attackframe, attackcount, weaponframe;

    public short cluster, secretcount, treasurecount, killcount,
                secrettotal, treasuretotal, killtotal;
    public int TimeCount;
    public int killx, killy;
    public bool victoryflag;            // set during victory animations

    public static gametype Read(BinaryReader br) => new()
    {
        difficulty = (difficultytypes)br.ReadInt16(),
        mapon = br.ReadString(),
        oldscore = br.ReadInt32(),
        score = br.ReadInt32(),
        nextextra = br.ReadInt32(),
        lives = br.ReadInt16(),
        health = br.ReadInt16(),
        weapon = (weapontypes)br.ReadInt16(),
        chosenweapon = (weapontypes)br.ReadInt16(),
        faceframe = br.ReadInt16(),
        attackframe = br.ReadInt16(),
        attackcount = br.ReadInt16(),
        weaponframe = br.ReadInt16(),
        cluster = br.ReadInt16(),
        secretcount = br.ReadInt16(),
        treasurecount = br.ReadInt16(),
        killcount = br.ReadInt16(),
        secrettotal = br.ReadInt16(),
        treasuretotal = br.ReadInt16(),
        killtotal = br.ReadInt16(),
        TimeCount = br.ReadInt32(),
        killx = br.ReadInt32(),
        killy = br.ReadInt32(),
        victoryflag = br.ReadBoolean(),
    };

    public void Write(BinaryWriter bw)
    {
        bw.Write((short)difficulty);
        bw.Write(mapon);
        bw.Write(oldscore);
        bw.Write(score);
        bw.Write(nextextra);
        bw.Write(lives);
        bw.Write(health);
        bw.Write((short)weapon);
        bw.Write((short)chosenweapon);
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
        bw.Write(victoryflag);
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


//internal class statobj_t
//{
//    public byte tilex, tiley;
//    public string shapenum;           // if shapenum == -1 the obj has been removed
//    public objflags flags;
//    public string item_class;

//    public void Read(BinaryReader br)
//    {
//        tilex = br.ReadByte();
//        tiley = br.ReadByte();
//        shapenum = br.ReadString();
//        flags = (objflags)br.ReadUInt32();
//        //itemnumber = (wl_stat_types)br.ReadByte();
//    }

//    public byte[] AsBytes()
//    {
//        var ms = new MemoryStream();
//        var bw = new BinaryWriter(ms);
//        {
//            bw.Write(tilex);
//            bw.Write(tiley);
//            bw.Write(shapenum);
//            bw.Write((uint)flags);
//            //bw.Write((byte)itemnumber);
//            return ms.ToArray();
//        }
//    }
//}

internal enum dooractiontypes
{
    dr_open, dr_closed, dr_opening, dr_closing,
};

internal class doorobj_t
{
    public sbyte tilex, tiley;
    public bool vertical;
    public dooractiontypes action;
    public short ticcount;
    public ushort position;            // leading edge of door (0 = closed, 0xffff = fully open)
    public MapTextureTranslation xlat = MapTextureTranslation.None;

    // Only the moving parts: position, orientation and lock (xlat) come from the map, which a
    // load has already respawned the doors from.
    public void ReadState(BinaryReader br)
    {
        action = (dooractiontypes)br.ReadByte();
        ticcount = br.ReadInt16();
        position = br.ReadUInt16();
    }

    public void WriteState(BinaryWriter bw)
    {
        bw.Write((byte)action);
        bw.Write(ticcount);
        bw.Write(position);
    }
}

internal abstract class Actor
{
    // Marker base for the tiles actorat[,] holds: Wall, Door, and BlockingActor (a solid static).
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

    internal static void ClearMemory() => _audioManager.StopAll();

    // JAB
    // Positional sounds are placed in tile units; the listener follows the player (see UpdateSoundListener).
    internal static void PlaySoundLocTile(string s, int tx, int ty) => _audioManager.PlayAt(s, tx + 0.5f, ty + 0.5f);
    internal static void PlaySoundLocActor(string s, Entities.Actors.Actor ob) =>
        _audioManager.PlayAt(s, FixedToTiles(ob.X), FixedToTiles(ob.Y));

    internal static void UpdateSoundListener() =>
        _audioManager.SetListener(FixedToTiles(player.X), FixedToTiles(player.Y), player.Angle);

    private static float FixedToTiles(int value) => value / (float)MapConstants.TILEGLOBAL;


    /*
    =============================================================================

                                    WL_INTER

    =============================================================================
    */


    internal class LRstruct
    {
        public short kill, secret, treasure;
        public int time;

        public static LRstruct Read(BinaryReader br) => new()
        {
            kill = br.ReadInt16(),
            secret = br.ReadInt16(),
            treasure = br.ReadInt16(),
            time = br.ReadInt32(),
        };

        public void Write(BinaryWriter bw)
        {
            bw.Write(kill);
            bw.Write(secret);
            bw.Write(treasure);
            bw.Write(time);
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
