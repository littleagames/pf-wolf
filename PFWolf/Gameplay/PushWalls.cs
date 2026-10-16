using PFWolf.Constants;

namespace PFWolf.Gameplay;

/// <summary>
/// A pushwall on the move. Any number can move at once, each with its own progress; a
/// moving wall's two tiles (the one it's leaving and the one ahead) are bare GameConstants.BIT_WALL.
/// </summary>
internal sealed class MovingPushWall
{
    public ushort State;                // session.tics since it started: a tile per 128
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

/*
=============================================================================

                                PUSHABLE WALLS

A pushwall slides two tiles (unless something stops it sooner), a tile per 128 session.tics. Any
number can move at once; demos keep the original game's one at a time.

=============================================================================
*/

/// <param name="demoActive">Whether a demo is playing or recording</param>
internal sealed class PushWalls(MapManager map, AudioManager audio, GameSession session, Func<bool> demoActive)
{
    // The pushwalls on the move, in the order they were pushed
    internal readonly List<MovingPushWall> pushwalls = [];

    // The moving pushwall the renderer is tracing, picked by SelectPushWall as a trace enters
    // one of its tiles
    internal MovingPushWall pwall = new();

    internal static readonly int[][] dirs = [[0, -1], [1, 0], [0, 1], [-1, 0]];

    /// <summary>The moving pushwall on a tile, the one it's leaving or the one ahead, or null.</summary>
    internal MovingPushWall? PushWallAt(int x, int y)
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
    internal void SelectPushWall(int x, int y)
    {
        if (PushWallAt(x, y) is { } wall)
            pwall = wall;
    }

    /// <summary>Stops every pushwall where it is (a new level, or the player starting over).</summary>
    internal void ClearPushWalls() => pushwalls.Clear();

    Entities.Actors.PlayerPawn PusherOf(MovingPushWall wall) =>
        wall.Pusher is { } p && ReferenceEquals(p.State.Pawn, p) ? p : map.Players.First();

    // Whether a player's box (PLAYERSIZE each way) reaches into a tile
    internal static bool PlayerBoxCovers(Entities.Actors.PlayerPawn pawn, int tilex, int tiley) =>
        (int)((pawn.X - GameConstants.PLAYERSIZE) >> MapConstants.TILESHIFT) <= tilex && tilex <= (int)((pawn.X + GameConstants.PLAYERSIZE) >> MapConstants.TILESHIFT)
        && (int)((pawn.Y - GameConstants.PLAYERSIZE) >> MapConstants.TILESHIFT) <= tiley && tiley <= (int)((pawn.Y + GameConstants.PLAYERSIZE) >> MapConstants.TILESHIFT);

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
    internal bool PushWall(int checkx, int checky, controldirs dir, string? moveSound = null, string? blockedSound = null)
    {
        int oldtile, dx, dy;

        // demos keep the original game's one wall at a time, so they play out the same
        if (pushwalls.Count > 0 && demoActive())
            return false;

        oldtile = map.tilemap[checkx, checky];
        if (oldtile == 0 || oldtile == GameConstants.BIT_WALL)    // nothing there, or a wall already on the move
            return false;

        dx = dirs[(int)dir][0];
        dy = dirs[(int)dir][1];

        if (map.actorat[checkx + dx, checky + dy] != null
            || map.EnemiesAt(checkx + dx, checky + dy).Any())
        {
            if (!string.IsNullOrEmpty(blockedSound))
                audio.Play(blockedSound);
            return false;
        }

        map.tilemap[checkx + dx, checky + dy] = (byte)oldtile;
        map.actorat[checkx + dx, checky + dy] = new Wall(oldtile);

        pushwalls.Add(new MovingPushWall
        {
            X = (ushort)checkx,
            Y = (ushort)checky,
            Dir = dir,
            State = 1,
            Pos = 0,
            Tile = (byte)oldtile,
            Pusher = session.player,
        });
        map.tilemap[checkx, checky] = GameConstants.BIT_WALL;
        map.tilemap[checkx + dx, checky + dy] = GameConstants.BIT_WALL;
        map.SetMapSpot(checkx, checky, 1,  0);   // remove P tile info
        map.SetMapSpot(checkx, checky, 0, (ushort)map.MAPSPOT(session.player.TileX, session.player.TileY, 0)); // set correct floorcode (BrotherTank's fix) TODO: use a better method...

        if (!string.IsNullOrEmpty(moveSound))
            audio.Play(moveSound);
        return true;
    }

    /*
    =================
    =
    = MovePWalls
    =
    =================
    */

    internal void MovePWalls()
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

    // Moves one pushwall on by this frame's session.tics: false once it has stopped
    bool MovePWall(MovingPushWall wall)
    {
        int oldblock, oldtile;

        oldblock = wall.State / 128;

        wall.State += (ushort)session.tics;

        if (wall.State / 128 != oldblock)
        {
            // block crossed into a new block
            oldtile = wall.Tile;

            //
            // the tile can now be walked into
            //
            map.tilemap[wall.X, wall.Y] = 0;
            map.actorat[wall.X, wall.Y] = null;
            map.SetMapSpot(wall.X, wall.Y, 0, (ushort)(PusherOf(wall).AreaNumber + map.Floors.AreaTile));    // TODO: this is unnecessary, and makes a mess of mapsegs

            int dx = dirs[(byte)wall.Dir][0], dy = dirs[(byte)wall.Dir][1];
            //
            // see if it should be pushed farther
            //
            if (wall.State >= 256)            // only move two tiles fix
            {
                //
                // the block has been pushed two tiles
                //
                map.tilemap[wall.X + dx, wall.Y + dy] = (byte)oldtile;
                map.MoveWallStories(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                map.MoveTag(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                map.MovePatch(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                return false;
            }
            else
            {
                // the wall's height, tag and patch go with it, on the tile wall.X/wall.Y name
                map.MoveWallStories(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                map.MoveTag(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                map.MovePatch(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                wall.X += (ushort)dx;
                wall.Y += (ushort)dy;
                int aheadx = wall.X + dx, aheady = wall.Y + dy;

                // stopped by anything in the way, a player included (any of them), or another
                // moving wall's tile
                if (map.actorat[aheadx, aheady] != null
                    || map.tilemap[aheadx, aheady] == GameConstants.BIT_WALL
                    || map.EnemiesAt(aheadx, aheady).Any()
                    || map.Players.Any(pawn => PlayerBoxCovers(pawn, aheadx, aheady)))
                {
                    map.tilemap[wall.X, wall.Y] = (byte)oldtile;
                    return false;
                }

                map.actorat[aheadx, aheady] = new Wall(oldtile);
                map.tilemap[aheadx, aheady] = GameConstants.BIT_WALL;
            }
        }

        wall.Pos = (ushort)((wall.State / 2) & 63);
        return true;
    }
}
