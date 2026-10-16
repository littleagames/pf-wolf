using PFWolf.Configuration;
using PFWolf.Constants;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Ui;

/// <summary>What the automap's keys need from the game</summary>
/// <param name="DemoActive">Whether a demo is playing or recording (the map isn't part of one)</param>
/// <param name="MapReveal">Whether the automap shows the whole level (am_reveal)</param>
/// <param name="SetMapReveal">Turns that on or off</param>
internal sealed record AutomapInputHooks(
    Func<bool> DemoActive,
    Func<bool> MapReveal,
    Action<bool> SetMapReveal);

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
    GameSession session,
    AutomapInputHooks hooks)
{
    // Map panning speed at the default zoom: 8 tiles a second at walking pace, in virtual pixels a tic
    internal const float AUTOMAP_PANSPEED = 8 * AutomapManager.DefaultZoom / 70f;

    // Zoom steps a second while a zoom key is held
    internal const float AUTOMAP_KEYZOOMRATE = 5f;

    internal bool IsAutomapKeyDown(automapkeys key) =>
        controls.Get(ControlAction.Of(key)).Any(code => code.Device != InputDevice.MouseWheel && polling.IsInputActive(code));

    internal int AutomapWheelNotches(automapkeys key) =>
        controlSettings.mouseenabled ? controls.Get(ControlAction.Of(key)).Sum(polling.WheelNotches) : 0;


    /// <summary>
    /// Opens or closes the automap from play (the automap button, or the `automap` command).
    /// Never during a demo: the map isn't part of what a demo records.
    /// </summary>
    internal void ToggleAutomap()
    {
        if (hooks.DemoActive())
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
            float zoom = AutomapWheelNotches(automapkeys.am_zoomin) - AutomapWheelNotches(automapkeys.am_zoomout);
            if (IsAutomapKeyDown(automapkeys.am_zoomin) || input.IsKeyDown(ScanCodes.sc_KeyPadPlus))
                zoom += AUTOMAP_KEYZOOMRATE * session.tics / 70f;
            if (IsAutomapKeyDown(automapkeys.am_zoomout) || input.IsKeyDown(ScanCodes.sc_KeyPadMinus))
                zoom -= AUTOMAP_KEYZOOMRATE * session.tics / 70f;
            if (zoom != 0)
                automap.ZoomBy(zoom);

            float pan = AUTOMAP_PANSPEED * session.tics * (input.IsButtonPressed(buttontypes.bt_run) ? 2 : 1);
            float panx = 0, pany = 0;
            if (IsAutomapKeyDown(automapkeys.am_panup)) pany -= pan;
            if (IsAutomapKeyDown(automapkeys.am_pandown)) pany += pan;
            if (IsAutomapKeyDown(automapkeys.am_panleft)) panx -= pan;
            if (IsAutomapKeyDown(automapkeys.am_panright)) panx += pan;
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

        bool Bound(automapkeys automapKey) => controls.IsBound(ControlAction.Of(automapKey), code);

        if (Bound(automapkeys.am_center))
            automap.SnapToPlayer();
        else if (Bound(automapkeys.am_follow))
            automap.ToggleFollow();
        else if (Bound(automapkeys.am_rotate))
            automap.ToggleRotate();
        else if (Bound(automapkeys.am_style))
            automap.ToggleStyle();
        else if (Bound(automapkeys.am_overlay))
            automap.ToggleOverlay();
        else if (Bound(automapkeys.am_grid))
            automap.ToggleGrid();
        else if (Bound(automapkeys.am_stats))
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
        int strafe = (input.IsButtonPressed(buttontypes.bt_run) ? GameConstants.RUNMOVE : GameConstants.BASEMOVE) * (int)session.tics;
        if (input.IsButtonPressed(buttontypes.bt_strafeleft))
            dx -= strafe;
        if (input.IsButtonPressed(buttontypes.bt_straferight))
            dx += strafe;

        automap.Pan(dx * AUTOMAP_PANSPEED / GameConstants.BASEMOVE, dy * AUTOMAP_PANSPEED / GameConstants.BASEMOVE);

        polling.controlx = polling.controly = polling.controlstrafe = 0;
        input.SetButtonPressed(buttontypes.bt_strafeleft, false);
        input.SetButtonPressed(buttontypes.bt_straferight, false);
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
        hooks.SetMapReveal(Toggle(args, hooks.MapReveal()));
        console.Print(hooks.MapReveal() ? "Automap reveal ON" : "Automap reveal OFF");
    }
}
