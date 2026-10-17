namespace PFWolf;

internal static partial class GameHost
{
    /*
    =============================================================================

                                    STATUS BAR

    The layout (statusbar.yaml) and its parts drawn from the player's state are in
    Ui/StatusBarDrawing.cs, and the weapon's ammo and charge light in Gameplay/PlayerWeapons.cs.

    =============================================================================
    */

    static StatusBarDrawing statusBarDrawing = null!;     // made in InitServices

    static StatusBarDrawing MakeStatusBarDrawing() =>
        new(_gameEngineManager, _assetManager, _videoManager, _graphicManager, _inventoryManager, _audioManager,
            _hudMessageManager, ui, session, settings, new StatusBarHooks(GetMapDisplayName, WrapText));

    /// <summary>The game pack's status bar layout (statusbar.yaml); empty if it has none</summary>
    internal static Assets.StatusBarAsset StatusBar => statusBarDrawing?.StatusBar ?? StatusBarDrawing.NoStatusBar;

    static void DrawKeys() => statusBarDrawing.DrawKeys();
    static void DrawScore() => statusBarDrawing.DrawScore();
}
