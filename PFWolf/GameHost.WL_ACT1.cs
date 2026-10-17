using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Managers;

namespace PFWolf;

internal static partial class GameHost
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

    internal static void ConnectAreas() => doors.ConnectAreas();
    internal static void InitAreas() => session.areas.InitAreas(_mapManager.Players.Select(pawn => (int)pawn.AreaNumber), _mapManager.Floors.NumAreas);

    /*
    =============================================================================

                                    PUSHABLE WALLS

    Gameplay/PushWalls.cs (made in InitServices); these forward to it, the renderer's pwall
    accessors included.

    =============================================================================
    */

    static PushWalls pushWalls = null!;

    internal static bool PushWall(int checkx, int checky, controldirs dir, string? moveSound = null, string? blockedSound = null)
        => pushWalls.PushWall(checkx, checky, dir, moveSound, blockedSound);

    /*
    =============================================================================

                                    SWITCHES

    The switch and trigger actions and Blake Stone's smart switches are Gameplay/SwitchActions.cs
    and Gameplay/SmartSwitches.cs (made in InitServices); these forward to them.

    =============================================================================
    */

    static SwitchActions switchActions = null!;
    static SmartSwitches smartSwitches = null!;

    // Made in InitServices. Registering only binds the handlers, so a test that lists the action
    // names (ScriptApiTests) can register them before any services exist: then they're made here.
    static SwitchActions MakeSwitchActions() => new(_audioManager, _mapManager, _inventoryManager, _hudMessageManager, doors, pushWalls, session,
        new SwitchHooks(CouldTakeInventory, TryApplyInventory));
    static SmartSwitches MakeSmartSwitches() => new(_mapManager, doors, session, (wallSwitch, x, y) => UseSwitch(wallSwitch, x, y, FacingDir(player.Angle)));

    private static void RegisterSwitchActions() => (switchActions ??= MakeSwitchActions()).Register();
    private static void RegisterSmartSwitchActions() => (smartSwitches ??= MakeSmartSwitches()).Register();
}
