using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>What the player's think needs from the game</summary>
/// <param name="UpdateRadar">Moves the status bar's radar on a tic (Planet Strike's)</param>
/// <param name="VictoryRunning">Whether A_VictoryRun's runner is on its way (the player watches it rather than turning round)</param>
/// <param name="InitAreas">Starts the level's area connections afresh, from where the players stand</param>
internal sealed record PlayerThinkHooks(Action UpdateRadar, Func<bool> VictoryRunning, Action InitAreas);

/// <summary>
/// The acting player's think each tic (T_Player): the status bar's face and gauges, changing
/// weapons, the use key, moving, the view's bob and the weapon in hand -- or, once the game is
/// won, turning to watch BJ. And putting the player at a start.
/// </summary>
internal sealed class PlayerThink(
    MapManager map,
    StatusBarDrawing statusBar,
    PlayerStats stats,
    PlayerWeapons weapons,
    WeaponInHand weaponInHand,
    PlayerUse use,
    PlayerMovement movement,
    WeaponBob weaponBob,
    LevelAI levelAI,
    GameSession session,
    PlayerThinkHooks hooks)
{
    /*
    ===============
    =
    = T_Player
    =
    ===============
    */
    internal void T_Player(Entities.Actors.Actor ob)
    {
        var gamestate = session.gamestate;
        var playerinput = session.playerinput;
        var player = session.player;

        if (gamestate.victoryflag)              // watching the BJ actor
        {
            if (!hooks.VictoryRunning())        // A_VictoryRun's runner goes the way the player faces
                VictorySpin();
            return;
        }

        stats.UpdateFace();
        statusBar.UpdateHeartMonitor();
        hooks.UpdateRadar();

        if (weaponInHand.IsWeaponReady())
        {
            weapons.CheckWeaponChange();

            if (playerinput.IsPressed(buttontypes.bt_use))
                use.Cmd_Use();
            else
            {
                levelAI.ResetTalkDelay();
                use.ResetUseHeld();
            }
        }
        else
        {
            // Mid-attack, a fresh press of use or fire is dropped rather than kept for later
            // (vanilla T_Attack); fire held down from before still counts for A_ReFire.
            if (playerinput.IsFreshPress(buttontypes.bt_use))
                playerinput.SetPressed(buttontypes.bt_use, false);

            if (playerinput.IsFreshPress(buttontypes.bt_attack))
                playerinput.SetPressed(buttontypes.bt_attack, false);
        }

        movement.ControlMovement(ob);
        if (session.ActingIsLocal)              // only the local player's view bobs
            weaponBob.Update(playerinput.ControlY, session.tics);
        if (gamestate.victoryflag)              // watching the BJ actor
            return;

        session.playerstate.PlUX = (ushort)(player.X >> GameConstants.UNSIGNEDSHIFT);    // scale to fit in unsigned
        session.playerstate.PlUY = (ushort)(player.Y >> GameConstants.UNSIGNEDSHIFT);
        player.TileX = (byte)(player.X >> (int)MapConstants.TILESHIFT);                // scale to tile values
        player.TileY = (byte)(player.Y >> (int)MapConstants.TILESHIFT);

        // The weapon's Ready/Fire states (Gameplay/WeaponInHand.cs): starts and plays attacks
        weaponInHand.TickWeapon();
    }

    internal void VictorySpin()
    {
        var player = session.player;
        var tics = session.tics;
        int desty;
        session.playerstate.Pitch = 0;            // watch BJ straight on
        if (player.Angle > 270)
        {
            player.Angle -= (short)(tics * 3);
            if (player.Angle < 270)
                player.Angle = 270;
        }
        else if (player.Angle < 270)
        {
            player.Angle += (short)(tics * 3);
            if (player.Angle > 270)
                player.Angle = 270;
        }

        desty = ((player.TileY - 5) << (int)MapConstants.TILESHIFT) - 0x3000;

        if (player.Y > desty)
        {
            player.Y -= (int)(tics * 4096);
            if (player.Y < desty)
                player.Y = desty;
        }
    }

    // angle: the mapdefs player start's facing, 0=east, 90=north, 180=west, 270=south
    internal void SpawnPlayer(int tilex, int tiley, int angle)
    {
        const int ANGLES = GameConstants.ANGLES;
        var player = session.player;
        player.Active = activetypes.ac_yes;
        player.SetPosition(tilex, tiley);       // tile, and the tile-centred world x/y
        player.AreaNumber = map.SpawnArea(tilex, tiley);
        player.SetState(PlayerPawn.SpawnState);
        player.Angle = (short)((angle % ANGLES + ANGLES) % ANGLES);
        player.RuntimeFlags = objflags.FL_NEVERMARK;
        movement.Thrust(0, 0);                  // set some variables

        hooks.InitAreas();
    }
}
