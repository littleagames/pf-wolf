using PFWolf.Configuration;

namespace PFWolf;

/// <summary>The keys that work while the automap is open; bound in <see cref="GameHost.controls"/>.</summary>
internal enum automapkeys
{
    am_zoomin,
    am_zoomout,
    am_panup,
    am_pandown,
    am_panleft,
    am_panright,
    am_center,
    am_follow,
    am_rotate,
    am_style,
    am_overlay,
    am_grid,
    am_stats,

    NUMAUTOMAPKEYS
}

internal static partial class GameHost
{
    // Drawing the automap (Ui/AutomapDrawing.cs)
    static AutomapDrawing automapDrawing = null!;       // made in InitServices

    static AutomapDrawing MakeAutomapDrawing() =>
        new(_assetManager, _videoManager, _graphicManager, _fontManager, _automapManager, _mapManager, _factionManager,
            _translationManager, doors, pushWalls, session, new AutomapHooks(
                () => (viewscreenx, viewscreeny, viewwidth, viewheight), () => mapreveal != 0, GetMapDisplayName, FormatPlayTime));

    internal static void DrawAutomap() => automapDrawing.DrawAutomap();

    // The automap's keys and console commands (Ui/AutomapInput.cs)
    static AutomapInput automapInput = null!;       // made in InitServices

    static AutomapInput MakeAutomapInput() => new(_inputManager, _consoleManager, _automapManager, _mapManager, controls, controlSettings,
        inputPolling, session, new AutomapInputHooks(() => demoplayback || demorecord, () => mapreveal != 0, on => mapreveal = (byte)(on ? 1 : 0)));

    internal static void ToggleAutomap() => automapInput.ToggleAutomap();
    internal static void UpdateAutomap() => automapInput.UpdateAutomap();
    internal static bool HandleAutomapKey(InputCode code) => automapInput.HandleAutomapKey(code);
    internal static void RouteMovementToAutomap() => automapInput.RouteMovementToAutomap();

    private static void Cmd_AmStyle(string[] args) => automapInput.Cmd_AmStyle(args);
    private static void Cmd_AmOverlay(string[] args) => automapInput.Cmd_AmOverlay(args);
    private static void Cmd_AmGrid(string[] args) => automapInput.Cmd_AmGrid(args);
    private static void Cmd_AmStats(string[] args) => automapInput.Cmd_AmStats(args);
    private static void Cmd_AmReveal(string[] args) => automapInput.Cmd_AmReveal(args);
}