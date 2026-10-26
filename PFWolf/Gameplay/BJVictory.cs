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
    /// <summary>
    /// A_VictoryTile("BJVictory"): the class runs for the exit (Wolf3D's BJ), and ends the level
    /// once it's done. With no class, or one there isn't, the level ends straight away.
    /// </summary>
    internal void VictoryTile(string? runnerClass)
    {
        var ran = !string.IsNullOrWhiteSpace(runnerClass) && SpawnVictoryRunner(runnerClass.Trim());
        session.GameState.VictoryFlag = true;
        if (!ran)
        {
            Console.WriteLine($"A_VictoryTile: no runner class \"{runnerClass}\"; give one, as A_VictoryTile(\"BJVictory\"). The level ends now.");
            session.PlayState = PlayState.Victorious;
        }
    }

    // Ends the game in victory (the Angel of Death's last death frame)
    internal void Victory(Entities.Actors.Actor ob) => session.PlayState = PlayState.Victorious;

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

        move = (int)(GameConstants.BjRunSpeed * session.Tics);

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

        move = (int)(GameConstants.BjJumpSpeed * session.Tics);
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
        session.PlayState = PlayState.Victorious;                            // exit castle tile
    }


    // Whether the runner came in
    internal bool SpawnVictoryRunner(string runnerClass)
    {
        var player = session.Player;

        // Same spot the legacy SpawnNewObj used: BJ's tile is the one south of the player's, but
        // he starts at the player's exact position (SpawnAtActor copies the player's x/y).
        var bj = map.SpawnAtActor(runnerClass, player);
        if (bj == null)
            return false;

        bj.TileY = (byte)(player.TileY + 1);
        bj.SyncPosition();
        bj.AreaNumber = map.SpawnArea(bj.TileX, bj.TileY);

        // SpawnNewObj started every actor a random number of tics into its first frame.
        bj.TicCount = map.SpawnTicCount(bj.CurrentState);

        bj.Dir = ObjDir.North;
        bj.Temp1 = 6;                      // tiles to run forward
        return true;
    }
}
