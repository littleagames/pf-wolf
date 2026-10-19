using System.Runtime.InteropServices;
using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf;

internal class VisibleObject
{
    public byte TileX, TileY;
    public short ViewX;
    public short ViewHeight;
    public string ShapeNum;
    public ObjFlags Flags;
    public Entities.Actors.Actor? WallSprite;   // drawn as a panel (Rendering/WallSprites.cs), not a billboard
    public bool Bright;                         // drawn unshaded (Lighting.IsBright)
    public bool Cloaked;                        // drawn as a dark shimmer of what's behind it (Actor.Cloaked)
    public int WorldX, WorldY;                  // where it stands (global units), for its light
    public Assets.SpriteTranslation? Translation;   // its colors remapped (Managers/TranslationManager.cs), or null
    public double Z;                            // drawn this many texels higher than its art puts it (Actor.Z)

    /// <summary>Back to as it was made, to be used again</summary>
    public void Reset()
    {
        TileX = TileY = 0;
        ViewX = ViewHeight = 0;
        ShapeNum = null!;
        Flags = default;
        WallSprite = null;
        Bright = Cloaked = false;
        WorldX = WorldY = 0;
        Translation = null;
        Z = 0;
    }
}

internal enum ButtonType
{
    NoButton = -1,
    Attack = 0,
    Strafe,
    Run,
    Use,
    // Weapon slot keys: slot N picks a weapon with `weapon.slot: N`. Slots 1-4 sit where the old
    // ready-knife/pistol/machine gun/chaingun buttons were, so demos and the old config.cfg
    // layout (both indexed by this enum) still line up; 5-9 and 0 come after automap.
    Slot1,
    Slot2,
    Slot3,
    Slot4,
    NextWeapon,
    PrevWeapon,
    Esc,
    Pause,
    StrafeLeft,
    StrafeRight,
    MoveForward,
    MoveBackward,
    TurnLeft,
    TurnRight,
    Automap,
    Slot5,
    Slot6,
    Slot7,
    Slot8,
    Slot9,
    Slot0,
    // Looking up and down (y-shearing, Rendering/FrameRefresh.cs): the view's pitch only, not saved or in demos
    LookUp,
    LookDown,
    CenterView,
    // The status bar radar's zoom (statusbar.yaml radar, Planet Strike's): not in demos
    RadarZoomIn,
    RadarZoomOut,
    // Reloads a weapon with a magazine (weapon.magazine; Midnight Assault's)
    Reload,
    // A scope's zoom (weapon.zoom; Midnight Assault's sniper rifle): held to zoom in, again to zoom out
    Zoom,

    Count
};

internal enum ActiveType
{
    No,
    Yes,
    Always,
}

enum ObjDir
{
    East,
    NorthEast,
    North,
    NorthWest,
    West,
    SouthWest,
    South,
    SouthEast,
    NoDir,
}

//---------------
//
// GameState structure
//
//---------------
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal class GameState
{
    // The skill: its place in game-info's skills, 0 for the first (easiest)
    public short Difficulty;
    public string MapOn;

    // The player's own stats (score, lives, health, armor, weapons...) are on each player's
    // Entities.PlayerState now: GameSession.PlayerState for the acting player

    public short Cluster, SecretCount, TreasureCount, KillCount,
                SecretTotal, TreasureTotal, KillTotal;
    public int TimeCount;               // tics on this level
    public int PlayTime;                // tics since the game began, over every level
    public bool VictoryFlag;            // set during victory animations

    private static string? ReadWeapon(BinaryReader br)
    {
        var name = br.ReadString();
        return name.Length == 0 ? null : name;
    }

    /// <summary>Reads the game, and the one player's stats a save keeps in among it</summary>
    public static (GameState Game, Entities.PlayerState Player) Read(BinaryReader br)
    {
        var game = new GameState();
        var p = new Entities.PlayerState();
        game.Difficulty = br.ReadInt16();
        p.PlayerClass = br.ReadString();
        game.MapOn = br.ReadString();
        p.OldScore = br.ReadInt32();
        p.Score = br.ReadInt32();
        p.NextExtra = br.ReadInt32();
        p.Lives = br.ReadInt16();
        p.Health = br.ReadInt16();
        p.Armor = br.ReadInt16();
        p.ArmorPercent = br.ReadInt16();
        p.Weapon = ReadWeapon(br);
        p.ChosenWeapon = ReadWeapon(br);
        p.FaceFrame = br.ReadInt16();
        game.Cluster = br.ReadInt16();
        game.SecretCount = br.ReadInt16();
        game.TreasureCount = br.ReadInt16();
        game.KillCount = br.ReadInt16();
        game.SecretTotal = br.ReadInt16();
        game.TreasureTotal = br.ReadInt16();
        game.KillTotal = br.ReadInt16();
        game.TimeCount = br.ReadInt32();
        game.PlayTime = br.ReadInt32();
        p.KillX = br.ReadInt32();
        p.KillY = br.ReadInt32();
        game.VictoryFlag = br.ReadBoolean();
        return (game, p);
    }

    /// <summary>Writes the game, with <paramref name="p"/>'s stats in among it</summary>
    public void Write(BinaryWriter bw, Entities.PlayerState p)
    {
        bw.Write((short)Difficulty);
        bw.Write(p.PlayerClass);
        bw.Write(MapOn);
        bw.Write(p.OldScore);
        bw.Write(p.Score);
        bw.Write(p.NextExtra);
        bw.Write(p.Lives);
        bw.Write(p.Health);
        bw.Write(p.Armor);
        bw.Write(p.ArmorPercent);
        bw.Write(p.Weapon ?? "");
        bw.Write(p.ChosenWeapon ?? "");
        bw.Write(p.FaceFrame);
        bw.Write(Cluster);
        bw.Write(SecretCount);
        bw.Write(TreasureCount);
        bw.Write(KillCount);
        bw.Write(SecretTotal);
        bw.Write(TreasureTotal);
        bw.Write(KillTotal);
        bw.Write(TimeCount);
        bw.Write(PlayTime);
        bw.Write(p.KillX);
        bw.Write(p.KillY);
        bw.Write(VictoryFlag);
    }
}

internal enum DoorAction
{
    Open, Closed, Opening, Closing,
};

internal class DoorObject
{
    public byte TileX, TileY;
    public bool Vertical;
    public DoorAction Action;
    public short TicCount;
    public ushort Position;            // leading edge of door (0 = closed, 0xffff = fully open)
    public bool Held;                  // opened by a switch to stay open, until something closes it
    public MapTextureTranslation Xlat = MapTextureTranslation.None;
    public string MapLock = "";        // the lock from the map: a door-locks object on its tile, else its doors.yaml lock
    public List<string> MapLocks = []; // the doors.yaml locks it needs several of (none with a door-locks object)
    public int LocksNeeded;            // how many of MapLocks it needs (all of them when 0)
    public bool Unlocked;              // a takes-key door the player has opened with its lock item

    /// <summary>The item the door needs to open, empty when it needs none (or only needs <see cref="MapLocks"/>)</summary>
    public string Lock => Unlocked ? "" : MapLock;

    /// <summary>Whether the door needs any item to open: its <see cref="Lock"/> or its <see cref="MapLocks"/></summary>
    public bool IsLocked => !Unlocked && (MapLock.Length > 0 || MapLocks.Count > 0);

    /// <summary>How many of <see cref="MapLocks"/> open the door: <see cref="LocksNeeded"/>, or all of them</summary>
    public int LocksRequired => LocksNeeded > 0 && LocksNeeded < MapLocks.Count ? LocksNeeded : MapLocks.Count;

    /// <summary>
    /// Whether the door has slid out of the way at along (0..0xffff across the tile). A sliding
    /// door is open below its leading edge; a split door (doors.yaml split) opens from the middle,
    /// each half sliding out by half of position.
    /// </summary>
    public bool OpenAt(int along)
    {
        along &= 0xffff;
        if (!Xlat.Split)
            return along < Position;
        int half = Position >> 1;
        return along >= 0x8000 - half && along < 0x8000 + half;
    }

    /// <summary>Where the door's texture is at along: it slides with the door (or with its half)</summary>
    public int TextureAlong(int along)
    {
        if (!Xlat.Split)
            return along - Position;
        return (along & 0xffff) >= 0x8000 ? along - (Position >> 1) : along + (Position >> 1);
    }

    // Only the moving parts and whether it's been unlocked: position, orientation and lock come
    // from the map, which a load has already respawned the doors from.
    public void ReadState(BinaryReader br)
    {
        Action = (DoorAction)br.ReadByte();
        TicCount = br.ReadInt16();
        Position = br.ReadUInt16();
        Held = br.ReadBoolean();
        Unlocked = br.ReadBoolean();
    }

    public void WriteState(BinaryWriter bw)
    {
        bw.Write((byte)Action);
        bw.Write(TicCount);
        bw.Write(Position);
        bw.Write(Held);
        bw.Write(Unlocked);
    }
}

internal abstract class Actor
{
    // Marker base for the tiles ActorAt[,] holds: Wall, Door, and BlockingActor (a solid static).
}

internal class BlockingActor: Actor
{
}

// A wall sprite's tile (Rendering/WallSprites.cs): enemies keep out of all of it, as they do a
// solid static's, while the player and projectiles are only stopped by the panel itself.
internal class WallSpriteBlocker: BlockingActor
{
}

internal class Wall: Actor
{
    public Wall()
    {
        WallId = -1;
    }
    public Wall(int wall)
    {
        this.WallId = wall;
    }

    public int WallId;
}

internal class Door: Actor
{
    public Door()
    {
        this.DoorNumber = -1;
    }

    public Door(int doornum)
    {
        this.DoorNumber = doornum;
    }

    public int DoorNumber;
}

// An enemy standing on the tile, as the original kept them in ActorAt[] (MapManager.MarkActorTile)
internal class ActorMark: Actor
{
    public ActorMark(PFWolf.Entities.Actors.Monster who)
    {
        Who = who;
    }

    public readonly PFWolf.Entities.Actors.Monster Who;
}

internal enum PlayState
{
    StillPlaying,
    Completed,
    Died,
    Warped,
    ResetGame,
    LoadedGame,
    Victorious,
    Abort,
    DemoDone,
    SecretLevel,
}

[Flags]
internal enum ObjFlags
{
    Shootable = 0x00000001,
    Bonus = 0x00000002,
    NeverMark = 0x00000004,
    Visible = 0x00000008,
    AttackMode = 0x00000010,
    FirstAttack = 0x00000020,
    Ambush = 0x00000040,
    NonMark = 0x00000080,
    FullBright = 0x00000100,
#if USE_DIR3DSPR
    // you can choose one of the following values in wl_act1.cpp
    // to make a static sprite a directional 3d sprite
    // (see example at the end of the statinfo array)
    DirHorizMid = 0x00000200,
    DirHorizFw = 0x00000400,
    DirHorizBw = 0x00000600,
    DirVertMid = 0x00000a00,
    DirVertFw = 0x00000c00,
    DirVertBw = 0x00000e00,

    // these values are just used to improve readability of code
    DirNone = 0x00000000,
    DirPosMid = 0x00000200,
    DirPosFw = 0x00000400,
    DirPosBw = 0x00000600,
    DirPosMask = 0x00000600,
    DirVertFlag = 0x00000800,
    DirMask = 0x00000e00,
#endif
    // Blake Stone's AI (Entities.Actors.BlakeMonster)
    Friendly = 0x00001000,       // doesn't go after the player until it hears or is told to (FRIENDLY actors)
    Interrogated = 0x00002000,   // has been talked to: an informant has given its hint, a scientist turned mean
    LockedState = 0x00004000,    // just hurt: no more pain or shooting back until it next chases
    ShootMode = 0x00008000,      // `monster.shootmode` actors: shooting rather than closing in
    RunAway = 0x00010000,        // a SMART actor running for supplies or a door
    RunToStatic = 0x00020000,    // ... heading for a pickup or door (SeekX/SeekY) rather than a far corner
    MustAttack = 0x00040000,     // a scientist asked once already: the next asking makes it mean
    HasAmmo = 0x00080000,        // an informant still has its charge pack to give
    HasTokens = 0x00100000,      // ... and its food tokens
    Sliding = 0x00200000,        // a corpse sliding back from the shot that killed it (T_BlowBack)
    Follows = 0x00400000,        // an ally that follows the players about (Monster.Follows: `monster.follow`, A_SetFaction "follow")
    Following = 0x00800000,      // ... walking after them now, or out of their way (Monster.ThinkInstead)
    Revived = 0x01000000,        // brought back from the dead by a reviver (Monster.Rise): no points killed again
    // next free bit is   0x02000000
}

internal static class ObjFlagsExtensions
{
    /// <summary>
    /// Whether every bit of <paramref name="flag"/> is set, as Enum.HasFlag. That boxes both values
    /// in code the JIT hasn't optimized yet, which every actor's think is for its first runs.
    /// </summary>
    internal static bool Has(this ObjFlags flags, ObjFlags flag) => (flags & flag) == flag;
}

internal class LevelRatio
{
    public short Kill, Secret, Treasure;
    public int Time;

    public static LevelRatio Read(BinaryReader br) => new()
    {
        Kill = br.ReadInt16(),
        Secret = br.ReadInt16(),
        Treasure = br.ReadInt16(),
        Time = br.ReadInt32(),
    };

    public void Write(BinaryWriter bw)
    {
        bw.Write(Kill);
        bw.Write(Secret);
        bw.Write(Treasure);
        bw.Write(Time);
    }
}
