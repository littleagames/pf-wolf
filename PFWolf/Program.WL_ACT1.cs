using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    /*
    ===============
    =
    = PlaceItemType
    =
    = Called during game play to drop actors' items.  It finds the proper
    = item number based on the item type (bo_???).  If there are no free item
    = spots, nothing is done.
    =
    ===============
    */
    internal static void PlaceItemType(string item_class, int tilex, int tiley)
    {
        _mapManager.SpawnThing(tilex, tiley, item_class);
    }

    /*
    =============================================================================

                                    DOORS

    The doors and the areas they join are Gameplay/Doors.cs (made in InitServices) and
    Gameplay/AreaConnections.cs (on the session); these forward to them.

    =============================================================================
    */

    static Doors doors = null!;

    internal const int DOORWIDTH = Doors.DOORWIDTH;
    internal const int OPENTICS = Doors.OPENTICS;

    internal static doorobj_t[] doorobjlist => doors.doorobjlist;
    internal static int lastdoorobj => doors.lastdoorobj;
    internal static short doornum => doors.doornum;
    internal static byte[,] areaconnect { get => session.areas.areaconnect; set => session.areas.areaconnect = value; }
    internal static byte[] areabyplayer { get => session.areas.areabyplayer; set => session.areas.areabyplayer = value; }

    internal static void ConnectAreas() => doors.ConnectAreas();
    internal static void InitAreas() => session.areas.InitAreas(_mapManager.Players.Select(pawn => (int)pawn.AreaNumber), _mapManager.Floors.NumAreas);
    internal static void InitDoorList() => doors.InitDoorList();
    internal static void SpawnDoor(int tilex, int tiley, bool vertical, MapTextureTranslation doorXlat) => doors.SpawnDoor(tilex, tiley, vertical, doorXlat);
    internal static void OpenDoor(int door) => doors.OpenDoor(door);
    internal static void CloseDoor(int door) => doors.CloseDoor(door);
    internal static void OperateDoor(int door) => doors.OperateDoor(door);
    internal static string DoorFace(doorobj_t door, string face) => Doors.DoorFace(door, face);
    static void RefuseLocked(string lockItem, string lockedSound, string lockMessage, string lockMessageStyle, bool isSwitch = false)
        => doors.RefuseLocked(lockItem, lockedSound, lockMessage, lockMessageStyle, isSwitch);
    internal static void MoveDoors() => doors.MoveDoors();


    /*
    =============================================================================

                                    PUSHABLE WALLS

    =============================================================================
    */

    /// <summary>
    /// A pushwall on the move. Any number can move at once, each with its own progress; a
    /// moving wall's two tiles (the one it's leaving and the one ahead) are bare BIT_WALL.
    /// </summary>
    internal sealed class MovingPushWall
    {
        public ushort State;                // tics since it started: a tile per 128
        public ushort Pos;                  // amount it has moved into the tile ahead (0-63)
        public ushort X, Y;                 // the tile it's leaving
        public controldirs Dir;
        public byte Tile;                   // its wall id

        // Who pushed it: the tiles it leaves take their area (not saved: a loaded game's
        // walls go by the first player, the only one a save has)
        public Entities.Actors.PlayerPawn? Pusher;

        public void Write(BinaryWriter bw)
        {
            bw.Write(State);
            bw.Write(Pos);
            bw.Write(X);
            bw.Write(Y);
            bw.Write((byte)Dir);
            bw.Write(Tile);
        }

        public static MovingPushWall Read(BinaryReader br)
        {
            var wall = new MovingPushWall
            {
                State = br.ReadUInt16(),
                Pos = br.ReadUInt16(),
                X = br.ReadUInt16(),
                Y = br.ReadUInt16(),
                Dir = (controldirs)br.ReadByte(),
                Tile = br.ReadByte(),
            };
            if (wall.X >= MapManager.MAPSIZE || wall.Y >= MapManager.MAPSIZE || (int)wall.Dir > (int)controldirs.di_west)
                throw new InvalidDataException($"Bad moving pushwall at {wall.X},{wall.Y}.");
            return wall;
        }
    }

    // The pushwalls on the move, in the order they were pushed
    static readonly List<MovingPushWall> pushwalls = [];

    // The moving pushwall the renderer is tracing, picked by SelectPushWall as a trace enters
    // one of its tiles
    static MovingPushWall pwall = new();
    static ushort pwallpos => pwall.Pos;
    static ushort pwallx => pwall.X;
    static ushort pwally => pwall.Y;
    static controldirs pwalldir => pwall.Dir;
    static byte pwalltile => pwall.Tile;

    static int[][] dirs = [[0, -1], [1, 0], [0, 1], [-1, 0]];

    /// <summary>The moving pushwall on a tile, the one it's leaving or the one ahead, or null.</summary>
    static MovingPushWall? PushWallAt(int x, int y)
    {
        foreach (var wall in pushwalls)
        {
            if ((wall.X == x && wall.Y == y)
                || (wall.X + dirs[(int)wall.Dir][0] == x && wall.Y + dirs[(int)wall.Dir][1] == y))
                return wall;
        }
        return null;
    }

    /// <summary>Points pwall at the moving pushwall on a tile; keeps the last one if there's none.</summary>
    static void SelectPushWall(int x, int y)
    {
        if (PushWallAt(x, y) is { } wall)
            pwall = wall;
    }

    /// <summary>Stops every pushwall where it is (a new level, or the player starting over).</summary>
    internal static void ClearPushWalls() => pushwalls.Clear();

    static Entities.Actors.PlayerPawn PusherOf(MovingPushWall wall) =>
        wall.Pusher is { } p && ReferenceEquals(p.State.Pawn, p) ? p : _mapManager.Players.First();

    // Whether a player's box (PLAYERSIZE each way) reaches into a tile
    static bool PlayerBoxCovers(Entities.Actors.PlayerPawn pawn, int tilex, int tiley) =>
        (int)((pawn.X - PLAYERSIZE) >> MapConstants.TILESHIFT) <= tilex && tilex <= (int)((pawn.X + PLAYERSIZE) >> MapConstants.TILESHIFT)
        && (int)((pawn.Y - PLAYERSIZE) >> MapConstants.TILESHIFT) <= tiley && tiley <= (int)((pawn.Y + PLAYERSIZE) >> MapConstants.TILESHIFT);

    /*
    ===============
    =
    = PushWall
    =
    ===============
    */
    // A_PushWall, the mapdefs trigger action (registered in RegisterActorActions): true if the
    // wall started moving, which is when a secret trigger counts as found. moveSound plays as it
    // starts, blockedSound when something's in the way (either null for none).
    internal static bool PushWall(int checkx, int checky, controldirs dir, string? moveSound = null, string? blockedSound = null)
    {
        int oldtile, dx, dy;

        // demos keep the original game's one wall at a time, so they play out the same
        if (pushwalls.Count > 0 && (demoplayback || demorecord))
            return false;

        oldtile = _mapManager.tilemap[checkx, checky];
        if (oldtile == 0 || oldtile == BIT_WALL)    // nothing there, or a wall already on the move
            return false;

        dx = dirs[(int)dir][0];
        dy = dirs[(int)dir][1];

        if (_mapManager.actorat[checkx + dx, checky + dy] != null
            || _mapManager.EnemiesAt(checkx + dx, checky + dy).Any())
        {
            if (!string.IsNullOrEmpty(blockedSound))
                _audioManager.Play(blockedSound);
            return false;
        }

        _mapManager.tilemap[checkx + dx, checky + dy] = (byte)oldtile;
        _mapManager.actorat[checkx + dx, checky + dy] = new Wall(oldtile);

        pushwalls.Add(new MovingPushWall
        {
            X = (ushort)checkx,
            Y = (ushort)checky,
            Dir = dir,
            State = 1,
            Pos = 0,
            Tile = (byte)oldtile,
            Pusher = player,
        });
        _mapManager.tilemap[checkx, checky] = BIT_WALL;
        _mapManager.tilemap[checkx + dx, checky + dy] = BIT_WALL;
        _mapManager.SetMapSpot(checkx, checky, 1,  0);   // remove P tile info
        _mapManager.SetMapSpot(checkx, checky, 0, (ushort)_mapManager.MAPSPOT(player.TileX, player.TileY, 0)); // set correct floorcode (BrotherTank's fix) TODO: use a better method...

        if (!string.IsNullOrEmpty(moveSound))
            _audioManager.Play(moveSound);
        return true;
    }

    /*
    =================
    =
    = MovePWalls
    =
    =================
    */

    internal static void MovePWalls()
    {
        // in the order they were pushed, so every machine in a net game moves them the same
        for (int i = 0; i < pushwalls.Count;)
        {
            if (MovePWall(pushwalls[i]))
                i++;
            else
                pushwalls.RemoveAt(i);
        }
    }

    // Moves one pushwall on by this frame's tics: false once it has stopped
    static bool MovePWall(MovingPushWall wall)
    {
        int oldblock, oldtile;

        oldblock = wall.State / 128;

        wall.State += (ushort)tics;

        if (wall.State / 128 != oldblock)
        {
            // block crossed into a new block
            oldtile = wall.Tile;

            //
            // the tile can now be walked into
            //
            _mapManager.tilemap[wall.X, wall.Y] = 0;
            _mapManager.actorat[wall.X, wall.Y] = null;
            _mapManager.SetMapSpot(wall.X, wall.Y, 0, (ushort)(PusherOf(wall).AreaNumber + _mapManager.Floors.AreaTile));    // TODO: this is unnecessary, and makes a mess of mapsegs

            int dx = dirs[(byte)wall.Dir][0], dy = dirs[(byte)wall.Dir][1];
            //
            // see if it should be pushed farther
            //
            if (wall.State >= 256)            // only move two tiles fix
            {
                //
                // the block has been pushed two tiles
                //
                _mapManager.tilemap[wall.X + dx, wall.Y + dy] = (byte)oldtile;
                _mapManager.MoveWallStories(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                _mapManager.MoveTag(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                _mapManager.MovePatch(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                return false;
            }
            else
            {
                // the wall's height, tag and patch go with it, on the tile wall.X/wall.Y name
                _mapManager.MoveWallStories(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                _mapManager.MoveTag(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                _mapManager.MovePatch(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                wall.X += (ushort)dx;
                wall.Y += (ushort)dy;
                int aheadx = wall.X + dx, aheady = wall.Y + dy;

                // stopped by anything in the way, a player included (any of them), or another
                // moving wall's tile
                if (_mapManager.actorat[aheadx, aheady] != null
                    || _mapManager.tilemap[aheadx, aheady] == BIT_WALL
                    || _mapManager.EnemiesAt(aheadx, aheady).Any()
                    || _mapManager.Players.Any(pawn => PlayerBoxCovers(pawn, aheadx, aheady)))
                {
                    _mapManager.tilemap[wall.X, wall.Y] = (byte)oldtile;
                    return false;
                }

                _mapManager.actorat[aheadx, aheady] = new Wall(oldtile);
                _mapManager.tilemap[aheadx, aheady] = BIT_WALL;
            }
        }

        wall.Pos = (ushort)((wall.State / 2) & 63);
        return true;
    }
}
