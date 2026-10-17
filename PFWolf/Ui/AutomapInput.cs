using PFWolf.Configuration;
using PFWolf.Constants;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Ui;

/// <summary>
/// The automap's keys: opening it (ToggleAutomap), zooming and panning while they're held
/// (UpdateAutomap), its toggles (HandleAutomapKey), the movement controls moving the map in pan
/// mode (RouteMovementToAutomap), and the am_* console commands. Drawing it is Ui/AutomapDrawing.cs.
/// </summary>
internal sealed class AutomapInput(
    InputManager input,
    ConsoleManager console,
    AutomapManager automap,
    MapManager map,
    ControlBindings controls,
    ControlSettings controlSettings,
    InputPolling polling,
    GameSession session)
{
    // Map panning speed at the default zoom: 8 tiles a second at walking pace, in virtual pixels a tic
    internal const float AUTOMAP_PANSPEED = 8 * AutomapManager.DefaultZoom / 70f;

    // Zoom steps a second while a zoom key is held
    internal const float AUTOMAP_KEYZOOMRATE = 5f;

    internal bool IsAutomapKeyDown(AutomapAction key) =>
        controls.Get(ControlAction.Of(key)).Any(code => code.Device != InputDevice.MouseWheel && polling.IsInputActive(code));

    internal int AutomapWheelNotches(AutomapAction key) =>
        controlSettings.mouseenabled ? controls.Get(ControlAction.Of(key)).Sum(polling.WheelNotches) : 0;


    /// <summary>
    /// Opens or closes the automap from play (the automap button, or the `automap` command).
    /// Never during a demo: the map isn't part of what a demo records.
    /// </summary>
    internal void ToggleAutomap()
    {
        if ((session.demoplayback || session.demorecord))
            return;

        automap.Toggle();       // the play loop's next UpdateAutomap centers it before it's drawn
    }

    /// <summary>
    /// Applies the held automap keys and the mouse wheel, then keeps the view on the player.
    /// Called every frame from the play loop.
    /// </summary>
    internal void UpdateAutomap()
    {
        if (map.Player == null)
            return;

        if (automap.IsOpen)
        {
            // A wheel bound to a zoom key steps once a notch; keys zoom smoothly while held
            float zoom = AutomapWheelNotches(AutomapAction.ZoomIn) - AutomapWheelNotches(AutomapAction.ZoomOut);
            if (IsAutomapKeyDown(AutomapAction.ZoomIn) || input.IsKeyDown(ScanCodes.KeyPadPlus))
                zoom += AUTOMAP_KEYZOOMRATE * session.tics / 70f;
            if (IsAutomapKeyDown(AutomapAction.ZoomOut) || input.IsKeyDown(ScanCodes.KeyPadMinus))
                zoom -= AUTOMAP_KEYZOOMRATE * session.tics / 70f;
            if (zoom != 0)
                automap.ZoomBy(zoom);

            float pan = AUTOMAP_PANSPEED * session.tics * (input.IsButtonPressed(ButtonType.Run) ? 2 : 1);
            float panx = 0, pany = 0;
            if (IsAutomapKeyDown(AutomapAction.PanUp)) pany -= pan;
            if (IsAutomapKeyDown(AutomapAction.PanDown)) pany += pan;
            if (IsAutomapKeyDown(AutomapAction.PanLeft)) panx -= pan;
            if (IsAutomapKeyDown(AutomapAction.PanRight)) panx += pan;
            automap.Pan(panx, pany);
        }

        automap.Update(session.player.X / (float)MapConstants.TILEGLOBAL, session.player.Y / (float)MapConstants.TILEGLOBAL, session.player.Angle);
    }

    /// <summary>
    /// Handles a fresh press of a key or button (from CheckKeys) if it's one of the automap's
    /// toggles and the map is open. Returns true if it's one of the automap's keys, so it doesn't
    /// also run a console bind.
    /// </summary>
    internal bool HandleAutomapKey(InputCode code)
    {
        if (!automap.IsOpen)
            return false;

        bool Bound(AutomapAction automapKey) => controls.IsBound(ControlAction.Of(automapKey), code);

        if (Bound(AutomapAction.Center))
            automap.SnapToPlayer();
        else if (Bound(AutomapAction.Follow))
            automap.ToggleFollow();
        else if (Bound(AutomapAction.Rotate))
            automap.ToggleRotate();
        else if (Bound(AutomapAction.Style))
            automap.ToggleStyle();
        else if (Bound(AutomapAction.Overlay))
            automap.ToggleOverlay();
        else if (Bound(AutomapAction.Grid))
            automap.ToggleGrid();
        else if (Bound(AutomapAction.Stats))
            automap.ToggleStats();
        else    // held keys: used in UpdateAutomap, not binds
            return polling.IsAutomapInput(code);

        return true;
    }

    /// <summary>
    /// In pan mode the player's movement controls move the map instead: called from PollControls
    /// once the movement is gathered, it hands the forward/back, turn and strafe input to the map
    /// and clears it so the player stands still. Fire, use and weapon buttons are left alone.
    /// </summary>
    internal void RouteMovementToAutomap()
    {
        if (!automap.IsOpen || automap.Follow)
            return;

        // controlx/controly run BASEMOVE a tic at walking pace (RUNMOVE running)
        float dx = polling.controlx + polling.controlstrafe, dy = polling.controly;
        int strafe = (input.IsButtonPressed(ButtonType.Run) ? GameConstants.RUNMOVE : GameConstants.BASEMOVE) * (int)session.tics;
        if (input.IsButtonPressed(ButtonType.StrafeLeft))
            dx -= strafe;
        if (input.IsButtonPressed(ButtonType.StrafeRight))
            dx += strafe;

        automap.Pan(dx * AUTOMAP_PANSPEED / GameConstants.BASEMOVE, dy * AUTOMAP_PANSPEED / GameConstants.BASEMOVE);

        polling.controlx = polling.controly = polling.controlstrafe = 0;
        input.SetButtonPressed(ButtonType.StrafeLeft, false);
        input.SetButtonPressed(ButtonType.StrafeRight, false);
    }



    internal void Cmd_AmStyle(string[] args)
    {
        if (args.Length > 0)
        {
            automap.Style = args[0].ToLowerInvariant() switch
            {
                "graphic" or "0" => AutomapStyle.Graphic,
                "color" or "1" => AutomapStyle.Color,
                _ => throw new ArgumentException($"expected graphic or color, got \"{args[0]}\""),
            };
        }

        console.Print($"am_style is {automap.Style.ToString().ToLowerInvariant()}");
    }

    internal void Cmd_AmOverlay(string[] args)
    {
        if (args.Length > 0)
            automap.Overlay = ParseBool(args[0]);

        console.Print($"am_overlay is {(automap.Overlay ? 1 : 0)}");
    }

    internal void Cmd_AmGrid(string[] args)
    {
        if (args.Length > 0)
            automap.ShowGrid = ParseBool(args[0]);

        console.Print($"am_grid is {(automap.ShowGrid ? 1 : 0)}");
    }

    internal void Cmd_AmStats(string[] args)
    {
        if (args.Length > 0)
            automap.ShowStats = ParseBool(args[0]);

        console.Print($"am_stats is {(automap.ShowStats ? 1 : 0)}");
    }

    internal void Cmd_AmReveal(string[] args)
    {
        session.mapreveal = (byte)(Toggle(args, session.mapreveal != 0) ? 1 : 0);
        console.Print(session.mapreveal != 0 ? "Automap reveal ON" : "Automap reveal OFF");
    }
}
