using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>What the player's think needs from the game</summary>
/// <param name="UpdateRadar">Moves the status bar's radar on a tic (Planet Strike's)</param>
/// <param name="VictoryRunning">Whether A_VictoryRun's runner is on its way (the player watches it rather than turning round)</param>
/// <param name="InitAreas">Starts the level's area connections afresh, from where the players stand</param>
/// <param name="UpdateZoom">Moves a scope's zoom on (Gameplay/WeaponZoom.cs), given whether the weapon's reloading</param>
/// <param name="Waiting">Whether the match mode keeps the acting player still for now (Prop Hunt's hunters while the props hide)</param>
internal sealed record PlayerThinkHooks(Action UpdateRadar, Func<bool> VictoryRunning, Action InitAreas, Action<bool> UpdateZoom,
    Func<bool>? Waiting = null);

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
    internal void ThinkPlayer(Entities.Actors.Actor ob)
    {
        var gamestate = session.GameState;
        var playerinput = session.PlayerInput;
        var player = session.Player;

        if (gamestate.VictoryFlag)              // watching the BJ actor
        {
            if (!hooks.VictoryRunning())        // A_VictoryRun's runner goes the way the player faces
                VictorySpin();
            return;
        }

        // Kept waiting by the match mode: their controls this frame count for nothing
        if (hooks.Waiting?.Invoke() == true)
            playerinput.Drop();

        stats.UpdateFace();
        stats.TickPowerups();
        statusBar.UpdateHeartMonitor();
        hooks.UpdateRadar();

        // Reloading is like being ready, as the original Midnight Assault's T_Reload: use works,
        // and fire is kept for cutting the reload short (WeaponInHand.TickWeapon); only the
        // weapon can't be changed (PlayerWeapons.CheckWeaponChange)
        if (weaponInHand.IsWeaponReady() || WeaponInHand.IsReloading(session.PlayerState.WeaponSprite))
        {
            weapons.CheckWeaponChange();
            hooks.UpdateZoom(WeaponInHand.IsReloading(session.PlayerState.WeaponSprite));

            if (playerinput.IsPressed(ButtonType.Use))
                use.PressUse();
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
            if (playerinput.IsFreshPress(ButtonType.Use))
                playerinput.SetPressed(ButtonType.Use, false);

            if (playerinput.IsFreshPress(ButtonType.Attack))
                playerinput.SetPressed(ButtonType.Attack, false);
        }

        movement.ControlMovement(ob);
        if (session.ActingIsLocal)              // only the local player's view bobs
            weaponBob.Update(playerinput.ControlY, session.Tics);
        if (gamestate.VictoryFlag)              // watching the BJ actor
            return;

        session.PlayerState.PlUX = (ushort)(player.X >> GameConstants.UnsignedShift);    // scale to fit in unsigned
        session.PlayerState.PlUY = (ushort)(player.Y >> GameConstants.UnsignedShift);
        player.TileX = (byte)(player.X >> (int)MapConstants.TileShift);                // scale to tile values
        player.TileY = (byte)(player.Y >> (int)MapConstants.TileShift);

        // The weapon's Ready/Fire states (Gameplay/WeaponInHand.cs): starts and plays attacks
        weaponInHand.TickWeapon();
    }

    internal void VictorySpin()
    {
        var player = session.Player;
        var tics = session.Tics;
        int desty;
        session.PlayerState.Pitch = 0;            // watch BJ straight on
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

        desty = ((player.TileY - 5) << (int)MapConstants.TileShift) - 0x3000;

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
        const int ANGLES = GameConstants.Angles;
        var player = session.Player;
        player.Active = ActiveType.Yes;
        player.SetPosition(tilex, tiley);       // tile, and the tile-centred world x/y
        player.AreaNumber = map.SpawnArea(tilex, tiley);
        player.SetState(PlayerPawn.SpawnState);
        player.Angle = (short)((angle % ANGLES + ANGLES) % ANGLES);
        player.RuntimeFlags = ObjFlags.NeverMark;
        movement.Thrust(0, 0);                  // set some variables

        hooks.InitAreas();
    }
}
