using PFWolf.Assets;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                    ELEVATOR

    A_FloorSelect (a switch action, on Blake Stone's elevator panel wall) shows the floor-select
    screen from gamepacks/{pack}/elevator.yaml (bstone's InputFloor). Its buttons are the floors
    of the level's hub cluster by game-info floor-number. Arrows (or turning) move the selector,
    space, enter or fire press its button, a number key presses that floor's (0 is floor 10),
    and Esc leaves. A floor the player has been on is open; the next floor up past the highest
    open one opens with the unlock item, used up as it does. Choosing a floor leaves the level
    (ex_warped); the player comes out in the same elevator on the new floor.

    =============================================================================
    */

    // The floor-select and teleporter screens (Ui/ElevatorScreens.cs, made in InitServices)
    static ElevatorScreens elevatorScreens = null!;
    static ElevatorAsset? Elevator => elevatorScreens.Elevator;
    static string? FloorSelectScreen(ElevatorAsset elevator) => elevatorScreens.FloorSelectScreen(elevator);
    static string? TeleporterScreen(ElevatorAsset elevator, TeleporterLayout layout) => elevatorScreens.TeleporterScreen(elevator, layout);

    static bool FloorSelectAction(Entities.TriggerActivation trigger, string[] args)
    {
        if (Elevator is not { } elevator)
            return false;

        int panelWall = _mapManager.MAPSPOT(trigger.TileX, trigger.TileY, 0);

        // Planet Strike's teleporter map (Ui/ElevatorScreens.cs): the player comes out at the
        // new floor's start
        bool teleporter = elevator.Style.Equals("teleporter", StringComparison.OrdinalIgnoreCase) && elevator.Teleporter != null;
        var map = teleporter ? TeleporterScreen(elevator, elevator.Teleporter!) : FloorSelectScreen(elevator);
        if (map == null)
        {
            DrawPlayScreen();
            ThreeDRefresh();
            _inputManager.ClearKeysDown();
            return true;
        }

        pendingMapChange = teleporter
            ? new PendingMapChange(map, false, 0, 0, 0, WaitTics: 0)
            : new PendingMapChange(map, true, player.X, player.Y, player.Angle, WaitTics: 0, ElevatorWall: panelWall);
        gamestate.mapon = map;
        playstate = playstatetypes.ex_warped;
        return true;
    }
}
