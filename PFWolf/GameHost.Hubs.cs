using System.Text;
using PFWolf.Assets;
using PFWolf.Extensions;
using PFWolf.Managers;

namespace PFWolf;

internal static partial class GameHost
{
    /*
    =============================================================================

                                    HUB CLUSTERS

    The hub state (the levels kept, the floors been on or unlocked, their stats) is
    Gameplay/Hubs.cs (made in InitServices); these forward to it. Entering and leaving a level,
    and the level start death goes back to, are Gameplay/LevelEntry.cs.

    =============================================================================
    */

    static Hubs hubs = null!;

    // The floor-select and teleporter screens (Ui/ElevatorScreens.cs, made in InitServices)
    static ElevatorScreens elevatorScreens = null!;

    // The level's overhead map, as the teleporter shows it
    internal static byte[] CaptureOverhead() => elevatorScreens.CaptureOverhead();

    static int FloorScore() => hubs.FloorScore();

    // Entering and leaving levels, and the level start death goes back to (Gameplay/LevelEntry.cs)
    static LevelEntry levelEntry = null!;       // made in InitServices

    static LevelEntry MakeLevelEntry() => new(_mapManager, _inventoryManager, hubs, levelSetup, playerMovement, session,
        new LevelEntryHooks(() => victoryRunning = false, CaptureOverhead,
            WriteLevelBody, ReadLevelBody, body => ApplyLevelBody(body)));

    internal static bool EnterLevel() => levelEntry.EnterLevel();
    internal static void LeaveLevel() => levelEntry.LeaveLevel();
    internal static void MarkLevelStart() => levelEntry.MarkLevelStart();
    internal static bool RestoreLevelStart() => levelEntry.RestoreLevelStart();
    static void AlignPlayerInElevator(int lastx, int lasty, int panelWall) => levelEntry.AlignPlayerInElevator(lastx, lasty, panelWall);

    // The hub clusters' trigger actions: elevator, teleporters, unlocking floors, the detonator
    // and the victory run (Gameplay/HubActions.cs)
    static HubActions hubActions = null!;       // made in InitServices (or by RegisterActorActions without it)

    static HubActions MakeHubActions() => new(_gameEngineManager, _mapManager, _audioManager, _inputManager, _inventoryManager,
        _hudMessageManager, hubs, elevatorScreens, playScreen, frameRefresh, playerMovement, session,
        new HubActionsHooks(change => pendingMapChange = change, PlaySoundLocActor));

    internal static bool victoryRunning { get => hubActions.victoryRunning; set => hubActions.victoryRunning = value; }
}
