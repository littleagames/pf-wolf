using System.Runtime.InteropServices;
using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf;

internal class visobj_t
{
    public byte tilex, tiley;
    public short viewx;
    public short viewheight;
    public string shapenum;
    public objflags flags;
    public Entities.Actors.Actor? wallsprite;   // drawn as a panel (GameHost.WallSprites.cs), not a billboard
    public bool bright;                         // drawn unshaded (Lighting.IsBright)
    public bool cloaked;                        // drawn as a dark shimmer of what's behind it (Actor.Cloaked)
    public int worldx, worldy;                  // where it stands (global units), for its light
    public Assets.SpriteTranslation? translation;   // its colors remapped (Managers/TranslationManager.cs), or null
}

internal enum buttontypes
{
    bt_nobutton = -1,
    bt_attack = 0,
    bt_strafe,
    bt_run,
    bt_use,
    // Weapon slot keys: slot N picks a weapon with `weapon.slot: N`. Slots 1-4 sit where the old
    // ready-knife/pistol/machine gun/chaingun buttons were, so demos and the old config.cfg
    // layout (both indexed by this enum) still line up; 5-9 and 0 come after automap.
    bt_slot1,
    bt_slot2,
    bt_slot3,
    bt_slot4,
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
    bt_slot5,
    bt_slot6,
    bt_slot7,
    bt_slot8,
    bt_slot9,
    bt_slot0,
    // Looking up and down (y-shearing, GameHost.WL_DRAW.cs): the view's pitch only, not saved or in demos
    bt_lookup,
    bt_lookdown,
    bt_centerview,
    // The status bar radar's zoom (statusbar.yaml radar, Planet Strike's): not in demos
    bt_radarzoomin,
    bt_radarzoomout,
    // Reloads a weapon with a magazine (weapon.magazine; Midnight Assault's)
    bt_reload,
    // A scope's zoom (weapon.zoom; Midnight Assault's sniper rifle): held to zoom in, again to zoom out
    bt_zoom,

    NUMBUTTONS
};

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
    // The skill: its place in game-info's skills, 0 for the first (easiest)
    public short difficulty;
    public string mapon;

    // The player's own stats (score, lives, health, armor, weapons...) are on each player's
    // Entities.PlayerState now: GameSession.playerstate for the acting player

    public short cluster, secretcount, treasurecount, killcount,
                secrettotal, treasuretotal, killtotal;
    public int TimeCount;               // tics on this level
    public int PlayTime;                // tics since the game began, over every level
    public bool victoryflag;            // set during victory animations

    private static string? ReadWeapon(BinaryReader br)
    {
        var name = br.ReadString();
        return name.Length == 0 ? null : name;
    }

    /// <summary>Reads the game, and the one player's stats a save keeps in among it</summary>
    public static (gametype Game, Entities.PlayerState Player) Read(BinaryReader br)
    {
        var game = new gametype();
        var p = new Entities.PlayerState();
        game.difficulty = br.ReadInt16();
        p.playerclass = br.ReadString();
        game.mapon = br.ReadString();
        p.oldscore = br.ReadInt32();
        p.score = br.ReadInt32();
        p.nextextra = br.ReadInt32();
        p.lives = br.ReadInt16();
        p.health = br.ReadInt16();
        p.armor = br.ReadInt16();
        p.armorpercent = br.ReadInt16();
        p.weapon = ReadWeapon(br);
        p.chosenweapon = ReadWeapon(br);
        p.faceframe = br.ReadInt16();
        game.cluster = br.ReadInt16();
        game.secretcount = br.ReadInt16();
        game.treasurecount = br.ReadInt16();
        game.killcount = br.ReadInt16();
        game.secrettotal = br.ReadInt16();
        game.treasuretotal = br.ReadInt16();
        game.killtotal = br.ReadInt16();
        game.TimeCount = br.ReadInt32();
        game.PlayTime = br.ReadInt32();
        p.killx = br.ReadInt32();
        p.killy = br.ReadInt32();
        game.victoryflag = br.ReadBoolean();
        return (game, p);
    }

    /// <summary>Writes the game, with <paramref name="p"/>'s stats in among it</summary>
    public void Write(BinaryWriter bw, Entities.PlayerState p)
    {
        bw.Write((short)difficulty);
        bw.Write(p.playerclass);
        bw.Write(mapon);
        bw.Write(p.oldscore);
        bw.Write(p.score);
        bw.Write(p.nextextra);
        bw.Write(p.lives);
        bw.Write(p.health);
        bw.Write(p.armor);
        bw.Write(p.armorpercent);
        bw.Write(p.weapon ?? "");
        bw.Write(p.chosenweapon ?? "");
        bw.Write(p.faceframe);
        bw.Write(cluster);
        bw.Write(secretcount);
        bw.Write(treasurecount);
        bw.Write(killcount);
        bw.Write(secrettotal);
        bw.Write(treasuretotal);
        bw.Write(killtotal);
        bw.Write(TimeCount);
        bw.Write(PlayTime);
        bw.Write(p.killx);
        bw.Write(p.killy);
        bw.Write(victoryflag);
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
    public bool held;                  // opened by a switch to stay open, until something closes it
    public MapTextureTranslation xlat = MapTextureTranslation.None;
    public string maplock = "";        // the lock from the map: a door-locks object on its tile, else its doors.yaml lock
    public bool unlocked;              // a takes-key door the player has opened with its lock item

    /// <summary>The item the door needs to open, empty when it needs none</summary>
    public string Lock => unlocked ? "" : maplock;

    /// <summary>
    /// Whether the door has slid out of the way at along (0..0xffff across the tile). A sliding
    /// door is open below its leading edge; a split door (doors.yaml split) opens from the middle,
    /// each half sliding out by half of position.
    /// </summary>
    public bool OpenAt(int along)
    {
        along &= 0xffff;
        if (!xlat.Split)
            return along < position;
        int half = position >> 1;
        return along >= 0x8000 - half && along < 0x8000 + half;
    }

    /// <summary>Where the door's texture is at along: it slides with the door (or with its half)</summary>
    public int TextureAlong(int along)
    {
        if (!xlat.Split)
            return along - position;
        return (along & 0xffff) >= 0x8000 ? along - (position >> 1) : along + (position >> 1);
    }

    // Only the moving parts and whether it's been unlocked: position, orientation and lock come
    // from the map, which a load has already respawned the doors from.
    public void ReadState(BinaryReader br)
    {
        action = (dooractiontypes)br.ReadByte();
        ticcount = br.ReadInt16();
        position = br.ReadUInt16();
        held = br.ReadBoolean();
        unlocked = br.ReadBoolean();
    }

    public void WriteState(BinaryWriter bw)
    {
        bw.Write((byte)action);
        bw.Write(ticcount);
        bw.Write(position);
        bw.Write(held);
        bw.Write(unlocked);
    }
}

internal abstract class Actor
{
    // Marker base for the tiles actorat[,] holds: Wall, Door, and BlockingActor (a solid static).
}

internal class BlockingActor: Actor
{
}

// A wall sprite's tile (GameHost.WallSprites.cs): enemies keep out of all of it, as they do a
// solid static's, while the player and projectiles are only stopped by the panel itself.
internal class WallSpriteBlocker: BlockingActor
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

// An enemy standing on the tile, as the original kept them in actorat[] (MapManager.MarkActorTile)
internal class ActorMark: Actor
{
    public ActorMark(PFWolf.Entities.Actors.Monster who)
    {
        Who = who;
    }

    public readonly PFWolf.Entities.Actors.Monster Who;
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
    // Blake Stone's AI (Entities.Actors.BlakeMonster)
    FL_FRIENDLY = 0x00001000,       // doesn't go after the player until it hears or is told to (FRIENDLY actors)
    FL_INTERROGATED = 0x00002000,   // has been talked to: an informant has given its hint, a scientist turned mean
    FL_LOCKEDSTATE = 0x00004000,    // just hurt: no more pain or shooting back until it next chases
    FL_SHOOTMODE = 0x00008000,      // `monster.shootmode` actors: shooting rather than closing in
    FL_RUNAWAY = 0x00010000,        // a SMART actor running for supplies or a door
    FL_RUNTOSTATIC = 0x00020000,    // ... heading for a pickup or door (SeekX/SeekY) rather than a far corner
    FL_MUSTATTACK = 0x00040000,     // a scientist asked once already: the next asking makes it mean
    FL_HASAMMO = 0x00080000,        // an informant still has its charge pack to give
    FL_HASTOKENS = 0x00100000,      // ... and its food tokens
    FL_SLIDING = 0x00200000,        // a corpse sliding back from the shot that killed it (T_BlowBack)
    FL_FOLLOWS = 0x00400000,        // an ally that follows the players about (Monster.Follows: `monster.follow`, A_SetFaction "follow")
    FL_FOLLOWING = 0x00800000,      // ... walking after them now, or out of their way (Monster.ThinkInstead)
    // next free bit is   0x00400000
}

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
