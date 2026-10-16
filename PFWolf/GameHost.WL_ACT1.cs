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


    /*
    =============================================================================

                                    PUSHABLE WALLS

    Gameplay/PushWalls.cs (made in InitServices); these forward to it, the renderer's pwall
    accessors included.

    =============================================================================
    */

    static PushWalls pushWalls = null!;

    // The pushwalls on the move, in the order they were pushed
    static List<MovingPushWall> pushwalls => pushWalls.pushwalls;

    // The moving pushwall the renderer is tracing, picked by SelectPushWall as a trace enters
    // one of its tiles
    static MovingPushWall pwall => pushWalls.pwall;
    static ushort pwallpos => pushWalls.pwall.Pos;
    static ushort pwallx => pushWalls.pwall.X;
    static ushort pwally => pushWalls.pwall.Y;
    static controldirs pwalldir => pushWalls.pwall.Dir;
    static byte pwalltile => pushWalls.pwall.Tile;

    static int[][] dirs => PushWalls.dirs;

    static void SelectPushWall(int x, int y) => pushWalls.SelectPushWall(x, y);
    internal static void ClearPushWalls() => pushWalls.ClearPushWalls();
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
    internal static bool OperateSmartSwitch(int x, int y, SmartSwitchOp op) => smartSwitches.OperateSmartSwitch(x, y, op);

    /// <summary>A dying actor's floors actor-code work: what it carried, and the tile its death switches</summary>
    internal static void CarriedDeathWork(Entities.Actors.Actor ob, int tilex, int tiley) => smartSwitches.CarriedDeathWork(ob, tilex, tiley);
}
