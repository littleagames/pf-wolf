using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>
/// The end of an episode: the BJVictory actor (actordefs/wolf3d/victory.yaml) runs along the
/// map's arrow icons for a few tiles, jumps, and then ends the level; and the Angel of Death's
/// last frame (A_Victory) ends the game.
/// </summary>
internal sealed class BJVictory(MapManager map, GameSession session)
{
    internal void VictoryTile()
    {
        SpawnBJVictory();
        session.gamestate.victoryflag = true;
    }

    // Ends the game in victory (the Angel of Death's last death frame)
    internal void Victory(Entities.Actors.Actor ob) => session.playstate = PlayState.Victorious;

    /*
    ===============
    =
    = T_BJRun
    =
    ===============
    */

    internal void ThinkBjRun(Entities.Actors.Monster ob)
    {
        int move;

        move = (int)(GameConstants.BJRUNSPEED * session.tics);

        while (move != 0)
        {
            if (move < ob.Distance)
            {
                ob.MoveObj(move);
                break;
            }

            ob.RecenterOnTile();
            move -= ob.Distance;

            ob.SelectPathDir();

            if ((--ob.Temp1) == 0)
            {
                ob.SetState("Jump");
                return;
            }
        }
    }

    /*
    ===============
    =
    = T_BJJump
    =
    ===============
    */

    internal void ThinkBjJump(Entities.Actors.Monster ob)
    {
        int move;

        move = (int)(GameConstants.BJJUMPSPEED * session.tics);
        ob.MoveObj(move);
    }


    /*
    ===============
    =
    = T_BJDone
    =
    ===============
    */

    internal void ThinkBjDone(Entities.Actors.Actor ob)
    {
        session.playstate = PlayState.Victorious;                            // exit castle tile
    }


    internal void SpawnBJVictory()
    {
        var player = session.player;

        // Same spot the legacy SpawnNewObj used: BJ's tile is the one south of the player's, but
        // he starts at the player's exact position (SpawnAtActor copies the player's x/y).
        var bj = map.SpawnAtActor("BJVictory", player);
        if (bj == null)
            return;

        bj.TileY = (byte)(player.TileY + 1);
        bj.SyncPosition();
        bj.AreaNumber = (byte)(map.MAPSPOT(bj.TileX, bj.TileY, 0) - map.Floors.AreaTile);

        // SpawnNewObj started every actor a random number of tics into its first frame.
        bj.TicCount = map.SpawnTicCount(bj.CurrentState);

        bj.Dir = ObjDir.North;
        bj.Temp1 = 6;                      // tiles to run forward
    }
}
