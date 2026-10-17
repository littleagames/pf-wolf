using PFWolf.Configuration;
using PFWolf.Extensions;
using PFWolf.Fonts;

namespace PFWolf.Ui;

/// <summary>
/// The Control menu (menudefs/control: the mouse and controller switched on or off, mouse look
/// and inverting it), the Controller Settings menu (menudefs/controller: dead zone, turn speed
/// and which stick turns) and the mouse sensitivity screen
/// </summary>
internal sealed class ControlMenus(AssetManager assets, InputManager input, AudioManager audio, VideoManager video,
    MenuDrawing draw, MenuLoop loop, MenuInput menuInput, MenuDefs defs, UiText ui, ControlSettings settings)
{
    // Loaded from menudefs/ in CheckForEpisodes
    internal ControlPanelItem[] CtlMenu = [];
    internal ControlPanelMenuInfo CtlItems = null!;
    internal ControlPanelItem[] JoyMenu = [];
    internal ControlPanelMenuInfo JoyItems = null!;
    internal int RunControl()
    {
        int which;

        DrawCtlScreen();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();

        do
        {
            which = loop.HandleMenu(CtlItems, CtlMenu, null);
            switch (MenuDefs.SelectedId(CtlMenu, which))
            {
                case "mouse-enabled":
                    settings.MouseEnabled ^= true;
                    input.CenterMouse();
                    DrawCtlScreen();
                    loop.ShootSnd();
                    break;

                case "mouse-look":
                    settings.MouseLook ^= true;
                    DrawCtlScreen();
                    loop.ShootSnd();
                    break;

                case "mouse-invert":
                    settings.MouseInvert ^= true;
                    DrawCtlScreen();
                    loop.ShootSnd();
                    break;

                case "joystick-enabled":
                    settings.JoystickEnabled ^= true;
                    DrawCtlScreen();
                    loop.ShootSnd();
                    break;

                case "mouse-sensitivity":
                case "controller-settings":
                case "customize":
                    DrawCtlScreen();
                    draw.MenuFadeIn();
                    menuInput.WaitKeyUp();
                    break;
            }
        }
        while (which >= 0);

        draw.MenuFadeOut();

        return 0;
    }

    internal void DrawCtlScreen()
    {
        int i;
        defs.DrawMenuComponents("control");

        var mouseEnabledItem = MenuDefs.FindMenuItem(CtlMenu, "mouse-enabled");
        var mouseSensItem = MenuDefs.FindMenuItem(CtlMenu, "mouse-sensitivity");

        if (input.JoyPresent())
            MenuDefs.FindMenuItem(CtlMenu, "joystick-enabled")?.Active = 1;

        if (input.IsMousePresent())
        {
            mouseEnabledItem?.Active = 1;
        }

        mouseSensItem?.Active = (short)(settings.MouseEnabled ? 1 : 0);

        // Inverting only means something while the mouse looks
        MenuDefs.FindMenuItem(CtlMenu, "mouse-look")?.Active = (short)(settings.MouseEnabled ? 1 : 0);
        MenuDefs.FindMenuItem(CtlMenu, "mouse-invert")?.Active = (short)(settings.MouseEnabled && settings.MouseLook ? 1 : 0);

        draw.DrawMenu(CtlItems, CtlMenu);

        draw.DrawMenuCheckbox(CtlItems, CtlMenu, "mouse-enabled", settings.MouseEnabled);
        draw.DrawMenuCheckbox(CtlItems, CtlMenu, "mouse-look", settings.MouseLook);
        draw.DrawMenuCheckbox(CtlItems, CtlMenu, "mouse-invert", settings.MouseInvert);
        draw.DrawMenuCheckbox(CtlItems, CtlMenu, "joystick-enabled", settings.JoystickEnabled);

        //
        // PICK FIRST AVAILABLE SPOT
        //
        if (CtlItems.CurPos < 0 || CtlMenu[CtlItems.CurPos].Active == 0)
        {
            for (i = 0; i < CtlItems.Amount; i++)
            {
                if (CtlMenu[i].Active != 0)
                {
                    CtlItems.CurPos = (short)i;
                    break;
                }
            }
        }

        draw.DrawMenuGun(CtlItems);
        video.Update();
    }

    ////////////////////////////////////////////////////////////////////
    //
    // CONTROLLER SETTINGS
    //
    ////////////////////////////////////////////////////////////////////

    // The dead zone slider moves in steps of this many percent
    private const int JoyDeadzoneStep = 5;
    private const int JoyDeadzoneMax = 50;

    internal int RunControllerSettings()
    {
        int which;

        DrawControllerMenu();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();

        do
        {
            which = loop.HandleMenu(JoyItems, JoyMenu, null, AdjustControllerSetting);

            // Enter steps the stick layout round too; the sliders only move with left/right
            if (MenuDefs.SelectedId(JoyMenu, which) == "joy-sticks")
                AdjustControllerSetting(which, 1);
        }
        while (which >= 0);

        draw.MenuFadeOut();

        return 0;
    }

    internal void DrawControllerMenu()
    {
        var language = assets.GetText("en-us");

        defs.DrawMenuComponents("controller");
        draw.DrawMenu(JoyItems, JoyMenu);
        DrawControllerValues();

        // Which controller the settings are for, under the hints
        ui.CenteredText(0, 320, 160, new TextStyle(UiFonts.SmallFont, "READCOLOR")).CPrint(ui.FitText(input.ControllerName is string name
            ? $"{"$STR_CTL_PAD".ToLanguageText(language)} {name}"
            : "$STR_CTL_NOPAD".ToLanguageText(language), 300, UiFonts.SmallFont));     // a controller's name can be long

        draw.DrawMenuGun(JoyItems);
        video.Update();
    }

    private void DrawControllerValues()
    {
        var language = assets.GetText("en-us");

        draw.DrawMenuSlider(JoyItems, JoyMenu, "joy-deadzone", Math.Clamp(settings.JoyDeadzone, 0, JoyDeadzoneMax) / JoyDeadzoneStep, JoyDeadzoneMax / JoyDeadzoneStep);
        draw.DrawMenuSlider(JoyItems, JoyMenu, "joy-turnspeed", Math.Clamp(settings.JoyTurnSpeed, 0, ControlSettings.JoyTurnSpeeds - 1), ControlSettings.JoyTurnSpeeds - 1);
        draw.DrawMenuChoice(JoyItems, JoyMenu, "joy-sticks", (settings.JoyClassicSticks ? "$STR_JOYCLASSIC" : "$STR_JOYMODERN").ToLanguageText(language));
    }

    // Left/right on a Controller Settings row
    private void AdjustControllerSetting(int which, int delta)
    {
        switch (MenuDefs.SelectedId(JoyMenu, which))
        {
            case "joy-deadzone":
                int deadzone = Math.Clamp((settings.JoyDeadzone / JoyDeadzoneStep + delta) * JoyDeadzoneStep, 0, JoyDeadzoneMax);
                if (deadzone == settings.JoyDeadzone)
                    return;
                settings.JoyDeadzone = deadzone;
                break;

            case "joy-turnspeed":
                int turnspeed = Math.Clamp(settings.JoyTurnSpeed + delta, 0, ControlSettings.JoyTurnSpeeds - 1);
                if (turnspeed == settings.JoyTurnSpeed)
                    return;
                settings.JoyTurnSpeed = turnspeed;
                break;

            case "joy-sticks":
                settings.JoyClassicSticks = !settings.JoyClassicSticks;
                break;

            default:
                return;
        }

        DrawControllerValues();
        video.Update();
        audio.Play("menu/move1");
    }

    internal int RunMouseSensitivity()
    {
        ControlInfo ci;
        int exit = 0, oldMA;


        oldMA = settings.MouseAdjustment;
        DrawMouseSens();
        do
        {
            GameEngineManager.DelayMs(5);
            menuInput.ReadAnyControl(out ci);
            switch (ci.Dir)
            {
                case Direction.North:
                case Direction.West:
                    if (settings.MouseAdjustment != 0)
                    {
                        settings.MouseAdjustment--;
                        video.Bar(60, 97, 200, 10, "TEXTCOLOR");
                        draw.DrawOutline(60, 97, 200, 10, "Black", "HIGHLIGHT");
                        draw.DrawOutline(60 + 20 * settings.MouseAdjustment, 97, 20, 10, "Black", "READCOLOR");
                        video.Bar(61 + 20 * settings.MouseAdjustment, 98, 19, 9, "READHCOLOR");
                        video.Update();
                        audio.Play("menu/move1");
                        menuInput.TicDelay(20);
                    }
                    break;

                case Direction.South:
                case Direction.East:
                    if (settings.MouseAdjustment < 9)
                    {
                        settings.MouseAdjustment++;
                        video.Bar(60, 97, 200, 10, "TEXTCOLOR");
                        draw.DrawOutline(60, 97, 200, 10, "Black", "HIGHLIGHT");
                        draw.DrawOutline(60 + 20 * settings.MouseAdjustment, 97, 20, 10, "Black", "READCOLOR");
                        video.Bar(61 + 20 * settings.MouseAdjustment, 98, 19, 9, "READHCOLOR");
                        video.Update();
                        audio.Play("menu/move1");
                        menuInput.TicDelay(20);
                    }
                    break;
            }

            if (ci.Button0 || input.IsKeyDown(ScanCodes.Space) || input.IsKeyDown(ScanCodes.Enter))
                exit = 1;
            else if (ci.Button1 || input.IsKeyDown(ScanCodes.Escape))
                exit = 2;

        }
        while (exit == 0);

        if (exit == 2)
        {
            settings.MouseAdjustment = oldMA;
            audio.Play("menu/escape");
        }
        else
            audio.Play("menu/activate");

        menuInput.WaitKeyUp();
        draw.MenuFadeOut();

        return 0;
    }

    internal void DrawMouseSens()
    {
        defs.DrawMenuComponents("mouse-sensitivity");

        video.Bar(60, 97, 200, 10, "TEXTCOLOR");
        draw.DrawOutline(60, 97, 200, 10, "Black", "HIGHLIGHT");
        draw.DrawOutline(60 + 20 * settings.MouseAdjustment, 97, 20, 10, "Black", "READCOLOR");
        video.Bar(61 + 20 * settings.MouseAdjustment, 98, 19, 9, "READHCOLOR");

        video.Update();
        draw.MenuFadeIn();
    }
}
