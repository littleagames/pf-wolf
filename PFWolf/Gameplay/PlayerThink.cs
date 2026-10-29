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
/// <param name="EnteredTile">The acting player's stepped onto another tile (game.onenter)</param>
/// <param name="WeaponChanged">The weapon in the acting player's hand has changed, from and to (game.onweaponswitch)</param>
internal sealed record PlayerThinkHooks(Action UpdateRadar, Func<bool> VictoryRunning, Action InitAreas, Action<bool> UpdateZoom,
    Func<bool>? Waiting = null, Action<int, int>? EnteredTile = null, Action<string, string>? WeaponChanged = null);

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
        int fromX = player.TileX, fromY = player.TileY;

        stats.UpdateFace();
        stats.TickPowerups();
        statusBar.UpdateHeartMonitor();
        hooks.UpdateRadar();

        // Reloading is like being ready, as the original Midnight Assault's T_Reload: use works,
        // and fire is kept for cutting the reload short (WeaponInHand.TickWeapon); only the
        // weapon can't be changed (PlayerWeapons.CheckWeaponChange). So is putting a weapon away
        // or bringing one up (Deselect, Select), but there fire does nothing and the change waits.
        bool ready = weaponInHand.IsWeaponReady();      // first: it may start a weapon going down
        bool switching = WeaponInHand.IsSwitching(session.PlayerState.WeaponSprite);
        if (ready || WeaponInHand.IsReloading(session.PlayerState.WeaponSprite) || switching)
        {
            if (switching)
            {
                if (playerinput.IsFreshPress(ButtonType.Attack))
                    playerinput.SetPressed(ButtonType.Attack, false);
            }
            else
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

        int movedFromX = player.X, movedFromY = player.Y;
        movement.ControlMovement(ob);
        if (session.ActingIsLocal)              // only the local player's view bobs
        {
            double moved = Math.Sqrt((double)(player.X - movedFromX) * (player.X - movedFromX)
                + (double)(player.Y - movedFromY) * (player.Y - movedFromY));
            weaponBob.Update(playerinput.ControlY, session.Tics, moved, ready);
        }
        if (gamestate.VictoryFlag)              // watching the BJ actor
            return;

        session.PlayerState.PlUX = (ushort)(player.X >> GameConstants.UnsignedShift);    // scale to fit in unsigned
        session.PlayerState.PlUY = (ushort)(player.Y >> GameConstants.UnsignedShift);
        player.TileX = (byte)(player.X >> (int)MapConstants.TileShift);                // scale to tile values
        player.TileY = (byte)(player.Y >> (int)MapConstants.TileShift);

        // The weapon's Ready/Fire states (Gameplay/WeaponInHand.cs): starts and plays attacks
        weaponInHand.TickWeapon();

        // The scripts hear of a step onto another tile (or a teleport), and of a weapon changed
        if (player.TileX != fromX || player.TileY != fromY)
            hooks.EnteredTile?.Invoke(player.TileX, player.TileY);
        if (session.PlayerState.Weapon is { } weapon)
        {
            var told = session.PlayerState.WeaponToldScripts;
            session.PlayerState.WeaponToldScripts = weapon;
            if (told != null && !told.Equals(weapon, StringComparison.OrdinalIgnoreCase))
                hooks.WeaponChanged?.Invoke(told, weapon);
        }
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
