using PFWolf.Assets;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                    TELEPORTER

    Planet Strike's way between floors (bstone's ps_input_floor): its teleporter wall runs
    A_FloorSelect as the elevator's does, and an elevator.yaml with `style: teleporter` shows
    the teleporter map instead of the panel. Up and down (or left and right) move through the
    cluster's floors by floor number; Enter goes, Esc leaves. A floor is open once the player
    has been on it or it's been unlocked (A_UnlockFloor, when a floor's security cube is
    destroyed); others show "teleport disabled". Each floor's overhead map is kept as it was
    when the player left it (Program.Hubs.cs floorOverheads).

    The fission detonator (an item with `inventory.useslot` and `inventory.use`) is dropped by
    its slot key: A_DropDetonator, beside the floor's security cube, with the next floor still
    locked. It goes off a few seconds later, breaking the cube (`monster.blastedby`), whose
    death unlocks the next floor.

    =============================================================================
    */

    private static void RegisterTeleporterActions()
    {
        // A_UnlockFloor: the cluster's next floor up (by floor number) opens in the teleporter
        Entities.MapTriggerRegistry.Register("A_UnlockFloor", (_, args) => UnlockNextFloor(args.ElementAtOrDefault(0)));
        Entities.Actors.ActorActionRegistry.Register("A_UnlockFloor", A_UnlockFloor);

        // A_DropDetonator("ArmedClass", "CubeClass"[, range]): see above
        Entities.MapTriggerRegistry.Register("A_DropDetonator", DropDetonatorAction);
    }

    static string? ClusterFloorMap(int floor) => hubs.ClusterFloorMap(floor);
    static int CurrentFloorNumber() => hubs.CurrentFloorNumber();
    static bool FloorOpen(string map) => hubs.FloorOpen(map);

    static void A_UnlockFloor(Entities.Actors.Actor ob, string[] args) => UnlockNextFloor(args.ElementAtOrDefault(0));

    static bool UnlockNextFloor(string? message)
    {
        if (ClusterFloorMap(CurrentFloorNumber() + 1) is not { } next || FloorOpen(next))
            return false;
        unlockedMaps.Add(next);
        _hudMessageManager.Show(HudMessageKind.Other, string.IsNullOrEmpty(message) ? "$PS_FLOORUNLOCKED" : message);
        _audioManager.Play("bs/roll_score");
        return true;
    }

    static bool DropDetonatorAction(Entities.TriggerActivation trigger, string[] args)
    {
        var armed = args.ElementAtOrDefault(0);
        var cubeClass = args.ElementAtOrDefault(1);
        int range = int.TryParse(args.ElementAtOrDefault(2), out var r) ? r : 2;
        if (string.IsNullOrEmpty(armed) || string.IsNullOrEmpty(cubeClass))
            return false;

        void Say(string text) => _hudMessageManager.Show(HudMessageKind.Other, text);

        var next = ClusterFloorMap(CurrentFloorNumber() + 1);
        if (next == null || FloorOpen(next))
        {
            Say("$PS_FLOORNOTLOCKED");
            return false;
        }

        var cube = _mapManager.GetActors().FirstOrDefault(a => !a.IsRemoved && a.Name.Equals(cubeClass, StringComparison.OrdinalIgnoreCase)
            && a.CurrentState?.StateName != "Death");
        if (cube == null)
        {
            Say("$PS_NOCOMPUTER");
            return false;
        }
        if (cube.AreaNumber != player.AreaNumber && SpawnAreaOf(cube) != _mapManager.SpawnArea(player.TileX, player.TileY))
        {
            Say("$PS_NOTNEAR");
            return false;
        }
        if (Math.Max(Math.Abs(player.TileX - cube.TileX), Math.Abs(player.TileY - cube.TileY)) > range)
        {
            Say("$PS_GETCLOSER");
            return false;
        }

        var dropped = _mapManager.SpawnAtActor(armed, player);
        if (dropped == null)
            return false;
        _inventoryManager.Take("PlasmaDetonator", 1);
        Say("$PS_DETONATORDROPPED");
        PlaySoundLocActor("bs/robot_servo", dropped);
        return true;
    }

    static byte SpawnAreaOf(Entities.Actors.Actor a) => _mapManager.SpawnArea(a.TileX, a.TileY);

    /// <summary>An empty weapon slot's key uses an item held for that slot (`inventory.useslot`, `inventory.use`)</summary>
    static void UseSlotItem(int slot)
    {
        if (HeldUsableItem(item => _inventoryManager.GetIntProperty(item, "inventory.useslot", -1) == slot) is { } action)
            UseItemAction(action);
    }

    // How long use has been held with nothing to use or talk to
    static int useHeldTics;

    /// <summary>
    /// Use held with nothing ahead to use or talk to: every `inventory.useholdtics` tics of it,
    /// an item held with that property is used (bstone drops the fission detonator this way,
    /// every 60 tics)
    /// </summary>
    static void UseHeldItem()
    {
        int hold = 0;
        if (HeldUsableItem(item => (hold = _inventoryManager.GetIntProperty(item, "inventory.useholdtics", 0)) > 0) is not { } action)
        {
            useHeldTics = 0;
            return;
        }
        useHeldTics += (int)tics;
        if (useHeldTics < hold)
            return;
        useHeldTics = 0;
        UseItemAction(action);
    }

    static void ResetUseHeld() => useHeldTics = 0;

    /// <summary>The `inventory.use` action of the first item held that matches</summary>
    static string? HeldUsableItem(Func<string, bool> match)
    {
        foreach (var (item, count) in _inventoryManager.Items.ToList())
            if (count > 0 && match(item) && _inventoryManager.GetStringProperty(item, "inventory.use") is { Length: > 0 } action)
                return action;
        return null;
    }

    static void UseItemAction(string action) =>
        Entities.MapTriggerRegistry.Invoke(action,
            new Entities.TriggerActivation(player.TileX, player.TileY, FacingDir(player.Angle), player, 0));

    // The level's overhead map, as the teleporter shows it (Ui/ElevatorScreens.cs)
    internal static byte[] CaptureOverhead() => elevatorScreens.CaptureOverhead();
}
