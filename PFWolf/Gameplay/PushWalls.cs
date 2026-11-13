using PFWolf.Constants;

namespace PFWolf.Gameplay;

/// <summary>
/// A pushwall on the move. Any number can move at once, each with its own progress; a
/// moving wall's two tiles (the one it's leaving and the one ahead) are bare GameConstants.BitWall.
/// </summary>
internal sealed class MovingPushWall
{
    public ushort State;                // session.tics since it started: a tile per 128
    public ushort Pos;                  // amount it has moved into the tile ahead (0-63)
    public ushort X, Y;                 // the tile it's leaving
    public ControlDir Dir;
    public ushort Tile;                 // its wall id

    // The stories that move with it: a block from Start to Stop and maybe a second above it (a
    // Stop2 of 0: none). A Stop of 0 is the whole of a wall with no block of its own: story 1 to
    // the level's wall height, which it keeps where it stops.
    public byte Start, Stop, Start2, Stop2;

    // Who pushed it: the tiles it leaves take their area (not saved: a loaded game's
    // walls go by the first player, the only one a save has)
    public Entities.Actors.PlayerPawn? Pusher;

    /// <summary>The stories it covers, lowest to highest (a gap between two parts drawn filled)</summary>
    public (int Start, int Stop) Span(int wallStories) =>
        Stop == 0 ? (1, wallStories) : (Start, Stop2 != 0 ? Stop2 : Stop);

    public void Write(BinaryWriter bw)
    {
        bw.Write(State);
        bw.Write(Pos);
        bw.Write(X);
        bw.Write(Y);
        bw.Write((byte)Dir);
        bw.Write(Tile);
        bw.Write(Start);
        bw.Write(Stop);
        bw.Write(Start2);
        bw.Write(Stop2);
    }

    public static MovingPushWall Read(BinaryReader br)
    {
        var wall = new MovingPushWall
        {
            State = br.ReadUInt16(),
            Pos = br.ReadUInt16(),
            X = br.ReadUInt16(),
            Y = br.ReadUInt16(),
            Dir = (ControlDir)br.ReadByte(),
            Tile = br.ReadUInt16(),
            Start = br.ReadByte(),
            Stop = br.ReadByte(),
            Start2 = br.ReadByte(),
            Stop2 = br.ReadByte(),
        };
        if (wall.X >= MapConstants.MaxMapSize || wall.Y >= MapConstants.MaxMapSize || (int)wall.Dir > (int)ControlDir.West
            || wall.Stop > GameConstants.MaxWallStories || wall.Start > wall.Stop || wall.Stop2 > GameConstants.MaxWallStories || wall.Start2 > wall.Stop2)
            throw new InvalidDataException($"Bad moving pushwall at {wall.X},{wall.Y}.");
        return wall;
    }
}

/// <summary>Which of a pushwall's stories move when it's pushed (A_PushWall, A_MoveWall, game-info pushwall-stories)</summary>
internal enum PushwallStories
{
    /// <summary>The whole column</summary>
    All,
    /// <summary>The player's story and everything above it; what's below stays (a floor)</summary>
    Above,
    /// <summary>Only the player's story; what's below and above stay (a hole with a lintel)</summary>
    Story,
}

/*
=============================================================================

                                PUSHABLE WALLS

A pushwall slides two tiles (game-info pushwall-distance; unless something stops it sooner), a
tile per 128 session.tics. Any number can move at once; demos keep the original game's one at a
time.

=============================================================================
*/

/// <param name="distance">How many tiles a pushwall slides (game-info pushwall-distance); 2 when left out</param>
internal sealed class PushWalls(MapManager map, AudioManager audio, GameSession session, Func<int?>? distance = null)
{
    // Tiles a pushwall slides: read as it moves, so a loaded game's walls go as far as the game says
    internal int Distance => Math.Clamp(distance?.Invoke() ?? 2, 1, 255);

    /// <summary>Whether the scripts let a pushwall slide from a tile the way given (game.onpushwall)</summary>
    internal Func<int, int, ControlDir, bool>? MayPush { get; init; }

    // The MovingWalls on the move, in the order they were pushed
    internal readonly List<MovingPushWall> MovingWalls = [];

    // The moving pushwall the renderer is tracing, picked by SelectPushWall as a trace enters
    // one of its tiles
    internal MovingPushWall PWall = new();

    internal static readonly int[][] Dirs = [[0, -1], [1, 0], [0, 1], [-1, 0]];

    /// <summary>The moving pushwall on a tile, the one it's leaving or the one ahead, or null.</summary>
    internal MovingPushWall? PushWallAt(int x, int y)
    {
        foreach (var wall in MovingWalls)
        {
            if ((wall.X == x && wall.Y == y)
                || (wall.X + Dirs[(int)wall.Dir][0] == x && wall.Y + Dirs[(int)wall.Dir][1] == y))
                return wall;
        }
        return null;
    }

    /// <summary>Points PWall at the moving pushwall on a tile; keeps the last one if there's none.</summary>
    internal void SelectPushWall(int x, int y)
    {
        if (PushWallAt(x, y) is { } wall)
            PWall = wall;
    }

    /// <summary>Stops every pushwall where it is (a new level, or the player starting over).</summary>
    internal void ClearPushWalls() => MovingWalls.Clear();

    Entities.Actors.PlayerPawn PusherOf(MovingPushWall wall) =>
        wall.Pusher is { } p && ReferenceEquals(p.State.Pawn, p) ? p : map.Players.First();

    // Whether a player's box (PlayerSize each way) reaches into a tile
    internal static bool PlayerBoxCovers(Entities.Actors.PlayerPawn pawn, int tilex, int tiley) =>
        (int)((pawn.X - GameConstants.PlayerSize) >> MapConstants.TileShift) <= tilex && tilex <= (int)((pawn.X + GameConstants.PlayerSize) >> MapConstants.TileShift)
        && (int)((pawn.Y - GameConstants.PlayerSize) >> MapConstants.TileShift) <= tiley && tiley <= (int)((pawn.Y + GameConstants.PlayerSize) >> MapConstants.TileShift);

    /*
    ===============
    =
    = PushWall
    =
    ===============
    */
    /// <summary>
    /// An A_PushWall or A_MoveWall stories argument (all, above or story) or game-info
    /// pushwall-stories; null when left out or unknown (said on the console, as from who)
    /// </summary>
    internal static PushwallStories? ParseStories(string? text, string who)
    {
        switch (text?.Trim().ToLowerInvariant())
        {
            case null or "": return null;
            case "all": return PushwallStories.All;
            case "above": return PushwallStories.Above;
            case "story": return PushwallStories.Story;
            default:
                Console.WriteLine($"{who}: unknown stories '{text}' (all, above or story).");
                return null;
        }
    }

    /// <summary>
    /// Which of the blocks on a pushwall's tile go with it (piece) and which stay (left), from the
    /// player's story: false if what's left, or where it could stop with the piece joined to that
    /// tile's own blocks, would need more than two blocks. whole: it's a wall with no block of its
    /// own, all of it moving (it keeps the level's wall height where it stops).
    /// </summary>
    internal bool SplitStories(int x, int y, int dx, int dy, PushwallStories mode,
        out List<(int Start, int Stop)> piece, out List<(int Start, int Stop)> left, out bool whole)
    {
        var blocks = map.TileBlocks(x, y);
        int story = map.Story;
        piece = [];
        left = [];
        whole = false;
        if (mode == PushwallStories.All || blocks.Count == 0)
        {
            piece = blocks.Count > 0 ? blocks : [(1, map.LevelWallStories)];
            whole = map.BlockStops[x, y] == 0 && map.UpperStops[x, y] == 0;
        }
        else
        {
            foreach (var (start, stop) in blocks)
            {
                if (stop < story)
                    left.Add((start, stop));                // below the player's story: the floor stays
                else if (start > story)
                    (mode == PushwallStories.Above ? piece : left).Add((start, stop));
                else
                {
                    // the block the player pushed
                    if (start < story)
                        left.Add((start, story - 1));
                    if (mode == PushwallStories.Above)
                        piece.Add((story, stop));
                    else
                    {
                        piece.Add((story, story));
                        if (stop > story)
                            left.Add((story + 1, stop));
                    }
                }
            }
            if (piece.Count == 0)
                piece.Add((story, story));
        }
        if (left.Count > 2)
            return false;

        // each tile it could stop on, as far as it goes
        for (int d = 1; d <= Distance; d++)
        {
            int tx = x + dx * d, ty = y + dy * d;
            if (!map.InMap(tx, ty) || (map.TileMap[tx, ty] != 0 && !map.IsSealedEdge(tx, ty)))
                break;
            if (MapManager.JoinBlocks(map.TileBlocks(tx, ty).Concat(piece)).Count > 2)
                return false;
        }
        return true;
    }

    // The blocks a moving pushwall carries
    List<(int Start, int Stop)> PieceOf(MovingPushWall wall)
    {
        if (wall.Stop == 0)
            return [(1, map.LevelWallStories)];
        var piece = new List<(int Start, int Stop)> { (wall.Start, wall.Stop) };
        if (wall.Stop2 != 0)
            piece.Add((wall.Start2, wall.Stop2));
        return piece;
    }

    // A moving pushwall stops on (x, y): its blocks join the tile's own (a wall with no block of its
    // own onto a tile with none stays as it was), and plane 0 holds the wall, as a wall placed
    // there would (MapManager.ApplyStory)
    void PlacePiece(MovingPushWall wall, int x, int y, int oldtile)
    {
        var own = map.TileBlocks(x, y);
        map.SetMapSpot(x, y, 0, (ushort)(oldtile & ~GameConstants.BitWall));
        if (wall.Stop == 0 && own.Count == 0)
            map.SetBlock(x, y, 0, 0);
        else
            map.SetTileBlocks(x, y, [.. own, .. PieceOf(wall)]);
        if (own.Count > 0)
            map.ApplyStory();           // the walkable wall it was on is solid now: the areas round it change
    }

    // A_PushWall, the mapdefs trigger action (registered in RegisterActorActions): true if the
    // wall started moving, which is when a secret trigger counts as found. moveSound plays as it
    // starts, blockedSound when something's in the way (either null for none). stories: which of
    // its stories move (null: the level's game-info pushwall-stories).
    internal bool PushWall(int checkx, int checky, ControlDir dir, string? moveSound = null, string? blockedSound = null,
        PushwallStories? stories = null)
    {
        int oldtile, dx, dy;

        // demos keep the original game's one wall at a time, so they play out the same
        if (MovingWalls.Count > 0 && (session.DemoPlayback || session.DemoRecord))
            return false;

        oldtile = map.TileMap[checkx, checky];
        if (oldtile == 0 || oldtile == GameConstants.BitWall)    // nothing there, or a wall already on the move
            return false;

        dx = Dirs[(int)dir][0];
        dy = Dirs[(int)dir][1];

        // an open edge tile (sealed so nothing leaves the map) is floor to a pushwall
        if (!map.InMap(checkx + dx, checky + dy)
            || (map.ActorAt[checkx + dx, checky + dy] != null && !map.IsSealedEdge(checkx + dx, checky + dy))
            || map.EnemiesAt(checkx + dx, checky + dy).Any())
        {
            if (!string.IsNullOrEmpty(blockedSound))
                audio.Play(blockedSound);
            return false;
        }
        // which of its stories go, and what's left: refused if they won't fit in two blocks
        if (!SplitStories(checkx, checky, dx, dy, stories ?? map.PushwallStories, out var piece, out var left, out bool whole))
        {
            if (!string.IsNullOrEmpty(blockedSound))
                audio.Play(blockedSound);
            return false;
        }
        if (MayPush?.Invoke(checkx, checky, dir) == false)
            return false;           // a script keeps it where it is

        map.TileMap[checkx + dx, checky + dy] = (ushort)oldtile;
        map.ActorAt[checkx + dx, checky + dy] = new Wall(oldtile);

        MovingWalls.Add(new MovingPushWall
        {
            X = (ushort)checkx,
            Y = (ushort)checky,
            Dir = dir,
            State = 1,
            Pos = 0,
            Tile = (ushort)oldtile,
            Pusher = session.Player,
            Start = whole ? (byte)0 : (byte)piece[0].Start,
            Stop = whole ? (byte)0 : (byte)piece[0].Stop,
            Start2 = !whole && piece.Count > 1 ? (byte)piece[1].Start : (byte)0,
            Stop2 = !whole && piece.Count > 1 ? (byte)piece[1].Stop : (byte)0,
        });
        map.TileMap[checkx, checky] = GameConstants.BitWall;
        map.TileMap[checkx + dx, checky + dy] = GameConstants.BitWall;
        map.SetMapSpot(checkx, checky, 1,  0);   // remove P tile info
        if (left.Count > 0)
            map.SetTileBlocks(checkx, checky, left);     // what's left keeps the wall id on plane 0, walked on or under
        else
        {
            map.SetMapSpot(checkx, checky, 0, (ushort)map.FloorCode(session.Player.TileX, session.Player.TileY)); // set correct floorcode (BrotherTank's fix) TODO: use a better method...
            map.SetBlock(checkx, checky, 0, 0);
        }

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
        for (int i = 0; i < MovingWalls.Count;)
        {
            if (MovePWall(MovingWalls[i]))
                i++;
            else
                MovingWalls.RemoveAt(i);
        }
    }

    // Moves one pushwall on by this frame's session.tics: false once it has stopped
    bool MovePWall(MovingPushWall wall)
    {
        int oldblock, oldtile;

        oldblock = wall.State / 128;

        wall.State += (ushort)session.Tics;

        if (wall.State / 128 != oldblock)
        {
            // block crossed into a new block
            oldtile = wall.Tile;

            //
            // the tile can now be walked into
            //
            map.TileMap[wall.X, wall.Y] = 0;
            map.ActorAt[wall.X, wall.Y] = null;
            map.ResealEdge(wall.X, wall.Y);         // an edge tile left open would let things off the map
            // a tile with blocks of its own (what the pushwall left, or ones it slid past) keeps
            // its plane 0: a wall there is walked on or under, its area from its neighbours
            bool blocksleft = map.TileBlocks(wall.X, wall.Y).Count > 0;
            if (!blocksleft)
                map.SetMapSpot(wall.X, wall.Y, 0, (ushort)(PusherOf(wall).AreaNumber + map.Floors.AreaTile));    // TODO: this is unnecessary, and makes a mess of mapsegs
            else if (map.IsWallCode(wall.X, wall.Y))
                map.ApplyStory();

            int dx = Dirs[(byte)wall.Dir][0], dy = Dirs[(byte)wall.Dir][1];
            //
            // see if it should be pushed farther
            //
            if (wall.State >= Distance * 128)    // only move two tiles fix (pushwall-distance)
            {
                //
                // the block has been pushed as far as it goes
                //
                map.TileMap[wall.X + dx, wall.Y + dy] = (ushort)oldtile;
                PlacePiece(wall, wall.X + dx, wall.Y + dy, oldtile);
                map.MoveTag(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                map.MovePatch(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                return false;
            }
            else
            {
                // the wall's tag and patch go with it, on the tile wall.X/wall.Y name (its
                // stories are its own: the tiles it crosses keep theirs)
                map.MoveTag(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                map.MovePatch(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                wall.X += (ushort)dx;
                wall.Y += (ushort)dy;
                int aheadx = wall.X + dx, aheady = wall.Y + dy;

                // stopped by anything in the way, a player included (any of them), or another
                // moving wall's tile, or the map's edge; an open edge tile (sealed so nothing
                // leaves the map) is floor to it, as in the original
                if (!map.InMap(aheadx, aheady)
                    || (map.ActorAt[aheadx, aheady] != null && !map.IsSealedEdge(aheadx, aheady))
                    || map.TileMap[aheadx, aheady] == GameConstants.BitWall
                    || map.EnemiesAt(aheadx, aheady).Any()
                    || map.Players.Any(pawn => PlayerBoxCovers(pawn, aheadx, aheady)))
                {
                    map.TileMap[wall.X, wall.Y] = (ushort)oldtile;
                    PlacePiece(wall, wall.X, wall.Y, oldtile);
                    return false;
                }

                map.ActorAt[aheadx, aheady] = new Wall(oldtile);
                map.TileMap[aheadx, aheady] = GameConstants.BitWall;
            }
        }

        wall.Pos = (ushort)((wall.State / 2) & 63);
        return true;
    }
}
