using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;
using PFWolf.Extensions;
using PFWolf.Managers;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf;

internal partial class Program
{
    // Drawing the automap (Ui/AutomapDrawing.cs); its keys and console commands are here
    static AutomapDrawing automapDrawing = null!;       // made in InitServices

    static AutomapDrawing MakeAutomapDrawing() =>
        new(_assetManager, _videoManager, _graphicManager, _fontManager, _automapManager, _mapManager, _factionManager,
            _translationManager, doors, pushWalls, session, new AutomapHooks(
                () => (viewscreenx, viewscreeny, viewwidth, viewheight), () => mapreveal != 0, GetMapDisplayName, FormatPlayTime));

    internal static void DrawAutomap() => automapDrawing.DrawAutomap();
    /// <summary>The keys that work while the automap is open; bound in <see cref="controls"/>.</summary>
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



    // Map panning speed at the default zoom: 8 tiles a second at walking pace, in virtual pixels a tic
    const float AUTOMAP_PANSPEED = 8 * AutomapManager.DefaultZoom / 70f;

    // Zoom steps a second while a zoom key is held
    const float AUTOMAP_KEYZOOMRATE = 5f;

    static bool IsAutomapKeyDown(automapkeys key) =>
        controls.Get(ControlAction.Of(key)).Any(code => code.Device != InputDevice.MouseWheel && IsInputActive(code));

    static int AutomapWheelNotches(automapkeys key) =>
        mouseenabled ? controls.Get(ControlAction.Of(key)).Sum(WheelNotches) : 0;


    /// <summary>
    /// Opens or closes the automap from play (the automap button, or the `automap` command).
    /// Never during a demo: the map isn't part of what a demo records.
    /// </summary>
    internal static void ToggleAutomap()
    {
        if (demoplayback || demorecord)
            return;

        _automapManager.Toggle();       // the play loop's next UpdateAutomap centers it before it's drawn
    }

    /// <summary>
    /// Applies the held automap keys and the mouse wheel, then keeps the view on the player.
    /// Called every frame from the play loop.
    /// </summary>
    internal static void UpdateAutomap()
    {
        if (_mapManager.Player == null)
            return;

        if (_automapManager.IsOpen)
        {
            // A wheel bound to a zoom key steps once a notch; keys zoom smoothly while held
            float zoom = AutomapWheelNotches(automapkeys.am_zoomin) - AutomapWheelNotches(automapkeys.am_zoomout);
            if (IsAutomapKeyDown(automapkeys.am_zoomin) || _inputManager.IsKeyDown(ScanCodes.sc_KeyPadPlus))
                zoom += AUTOMAP_KEYZOOMRATE * tics / 70f;
            if (IsAutomapKeyDown(automapkeys.am_zoomout) || _inputManager.IsKeyDown(ScanCodes.sc_KeyPadMinus))
                zoom -= AUTOMAP_KEYZOOMRATE * tics / 70f;
            if (zoom != 0)
                _automapManager.ZoomBy(zoom);

            float pan = AUTOMAP_PANSPEED * tics * (_inputManager.IsButtonPressed(buttontypes.bt_run) ? 2 : 1);
            float panx = 0, pany = 0;
            if (IsAutomapKeyDown(automapkeys.am_panup)) pany -= pan;
            if (IsAutomapKeyDown(automapkeys.am_pandown)) pany += pan;
            if (IsAutomapKeyDown(automapkeys.am_panleft)) panx -= pan;
            if (IsAutomapKeyDown(automapkeys.am_panright)) panx += pan;
            _automapManager.Pan(panx, pany);
        }

        _automapManager.Update(player.X / (float)MapConstants.TILEGLOBAL, player.Y / (float)MapConstants.TILEGLOBAL, player.Angle);
    }

    /// <summary>
    /// Handles a fresh press of a key or button (from CheckKeys) if it's one of the automap's
    /// toggles and the map is open. Returns true if it's one of the automap's keys, so it doesn't
    /// also run a console bind.
    /// </summary>
    internal static bool HandleAutomapKey(InputCode code)
    {
        if (!_automapManager.IsOpen)
            return false;

        bool Bound(automapkeys automapKey) => controls.IsBound(ControlAction.Of(automapKey), code);

        if (Bound(automapkeys.am_center))
            _automapManager.SnapToPlayer();
        else if (Bound(automapkeys.am_follow))
            _automapManager.ToggleFollow();
        else if (Bound(automapkeys.am_rotate))
            _automapManager.ToggleRotate();
        else if (Bound(automapkeys.am_style))
            _automapManager.ToggleStyle();
        else if (Bound(automapkeys.am_overlay))
            _automapManager.ToggleOverlay();
        else if (Bound(automapkeys.am_grid))
            _automapManager.ToggleGrid();
        else if (Bound(automapkeys.am_stats))
            _automapManager.ToggleStats();
        else    // held keys: used in UpdateAutomap, not binds
            return IsAutomapInput(code);

        return true;
    }

    /// <summary>
    /// In pan mode the player's movement controls move the map instead: called from PollControls
    /// once the movement is gathered, it hands the forward/back, turn and strafe input to the map
    /// and clears it so the player stands still. Fire, use and weapon buttons are left alone.
    /// </summary>
    internal static void RouteMovementToAutomap()
    {
        if (!_automapManager.IsOpen || _automapManager.Follow)
            return;

        // controlx/controly run BASEMOVE a tic at walking pace (RUNMOVE running)
        float dx = controlx + controlstrafe, dy = controly;
        int strafe = (_inputManager.IsButtonPressed(buttontypes.bt_run) ? RUNMOVE : BASEMOVE) * (int)tics;
        if (_inputManager.IsButtonPressed(buttontypes.bt_strafeleft))
            dx -= strafe;
        if (_inputManager.IsButtonPressed(buttontypes.bt_straferight))
            dx += strafe;

        _automapManager.Pan(dx * AUTOMAP_PANSPEED / BASEMOVE, dy * AUTOMAP_PANSPEED / BASEMOVE);

        controlx = controly = controlstrafe = 0;
        _inputManager.SetButtonPressed(buttontypes.bt_strafeleft, false);
        _inputManager.SetButtonPressed(buttontypes.bt_straferight, false);
    }



    private static void Cmd_AmStyle(string[] args)
    {
        if (args.Length > 0)
        {
            _automapManager.Style = args[0].ToLowerInvariant() switch
            {
                "graphic" or "0" => AutomapStyle.Graphic,
                "color" or "1" => AutomapStyle.Color,
                _ => throw new ArgumentException($"expected graphic or color, got \"{args[0]}\""),
            };
        }

        _consoleManager.Print($"am_style is {_automapManager.Style.ToString().ToLowerInvariant()}");
    }

    private static void Cmd_AmOverlay(string[] args)
    {
        if (args.Length > 0)
            _automapManager.Overlay = ParseBool(args[0]);

        _consoleManager.Print($"am_overlay is {(_automapManager.Overlay ? 1 : 0)}");
    }

    private static void Cmd_AmGrid(string[] args)
    {
        if (args.Length > 0)
            _automapManager.ShowGrid = ParseBool(args[0]);

        _consoleManager.Print($"am_grid is {(_automapManager.ShowGrid ? 1 : 0)}");
    }

    private static void Cmd_AmStats(string[] args)
    {
        if (args.Length > 0)
            _automapManager.ShowStats = ParseBool(args[0]);

        _consoleManager.Print($"am_stats is {(_automapManager.ShowStats ? 1 : 0)}");
    }

    private static void Cmd_AmReveal(string[] args)
    {
        mapreveal = (byte)(Toggle(args, mapreveal != 0) ? 1 : 0);
        _consoleManager.Print(mapreveal != 0 ? "Automap reveal ON" : "Automap reveal OFF");
    }
}
