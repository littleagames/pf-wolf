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
                    settings.mouseenabled ^= true;
                    input.CenterMouse();
                    DrawCtlScreen();
                    loop.ShootSnd();
                    break;

                case "mouse-look":
                    settings.mouselook ^= true;
                    DrawCtlScreen();
                    loop.ShootSnd();
                    break;

                case "mouse-invert":
                    settings.mouseinvert ^= true;
                    DrawCtlScreen();
                    loop.ShootSnd();
                    break;

                case "joystick-enabled":
                    settings.joystickenabled ^= true;
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
            MenuDefs.FindMenuItem(CtlMenu, "joystick-enabled")?.active = 1;

        if (input.IsMousePresent())
        {
            mouseEnabledItem?.active = 1;
        }

        mouseSensItem?.active = (short)(settings.mouseenabled ? 1 : 0);

        // Inverting only means something while the mouse looks
        MenuDefs.FindMenuItem(CtlMenu, "mouse-look")?.active = (short)(settings.mouseenabled ? 1 : 0);
        MenuDefs.FindMenuItem(CtlMenu, "mouse-invert")?.active = (short)(settings.mouseenabled && settings.mouselook ? 1 : 0);

        draw.DrawMenu(CtlItems, CtlMenu);

        draw.DrawMenuCheckbox(CtlItems, CtlMenu, "mouse-enabled", settings.mouseenabled);
        draw.DrawMenuCheckbox(CtlItems, CtlMenu, "mouse-look", settings.mouselook);
        draw.DrawMenuCheckbox(CtlItems, CtlMenu, "mouse-invert", settings.mouseinvert);
        draw.DrawMenuCheckbox(CtlItems, CtlMenu, "joystick-enabled", settings.joystickenabled);

        //
        // PICK FIRST AVAILABLE SPOT
        //
        if (CtlItems.curpos < 0 || CtlMenu[CtlItems.curpos].active == 0)
        {
            for (i = 0; i < CtlItems.amount; i++)
            {
                if (CtlMenu[i].active != 0)
                {
                    CtlItems.curpos = (short)i;
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

        draw.DrawMenuSlider(JoyItems, JoyMenu, "joy-deadzone", Math.Clamp(settings.joydeadzone, 0, JoyDeadzoneMax) / JoyDeadzoneStep, JoyDeadzoneMax / JoyDeadzoneStep);
        draw.DrawMenuSlider(JoyItems, JoyMenu, "joy-turnspeed", Math.Clamp(settings.joyturnspeed, 0, ControlSettings.JoyTurnSpeeds - 1), ControlSettings.JoyTurnSpeeds - 1);
        draw.DrawMenuChoice(JoyItems, JoyMenu, "joy-sticks", (settings.joyclassicsticks ? "$STR_JOYCLASSIC" : "$STR_JOYMODERN").ToLanguageText(language));
    }

    // Left/right on a Controller Settings row
    private void AdjustControllerSetting(int which, int delta)
    {
        switch (MenuDefs.SelectedId(JoyMenu, which))
        {
            case "joy-deadzone":
                int deadzone = Math.Clamp((settings.joydeadzone / JoyDeadzoneStep + delta) * JoyDeadzoneStep, 0, JoyDeadzoneMax);
                if (deadzone == settings.joydeadzone)
                    return;
                settings.joydeadzone = deadzone;
                break;

            case "joy-turnspeed":
                int turnspeed = Math.Clamp(settings.joyturnspeed + delta, 0, ControlSettings.JoyTurnSpeeds - 1);
                if (turnspeed == settings.joyturnspeed)
                    return;
                settings.joyturnspeed = turnspeed;
                break;

            case "joy-sticks":
                settings.joyclassicsticks = !settings.joyclassicsticks;
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


        oldMA = settings.mouseadjustment;
        DrawMouseSens();
        do
        {
            GameEngineManager.DelayMs(5);
            menuInput.ReadAnyControl(out ci);
            switch (ci.dir)
            {
                case Direction.North:
                case Direction.West:
                    if (settings.mouseadjustment != 0)
                    {
                        settings.mouseadjustment--;
                        video.Bar(60, 97, 200, 10, "TEXTCOLOR");
                        draw.DrawOutline(60, 97, 200, 10, "Black", "HIGHLIGHT");
                        draw.DrawOutline(60 + 20 * settings.mouseadjustment, 97, 20, 10, "Black", "READCOLOR");
                        video.Bar(61 + 20 * settings.mouseadjustment, 98, 19, 9, "READHCOLOR");
                        video.Update();
                        audio.Play("menu/move1");
                        menuInput.TicDelay(20);
                    }
                    break;

                case Direction.South:
                case Direction.East:
                    if (settings.mouseadjustment < 9)
                    {
                        settings.mouseadjustment++;
                        video.Bar(60, 97, 200, 10, "TEXTCOLOR");
                        draw.DrawOutline(60, 97, 200, 10, "Black", "HIGHLIGHT");
                        draw.DrawOutline(60 + 20 * settings.mouseadjustment, 97, 20, 10, "Black", "READCOLOR");
                        video.Bar(61 + 20 * settings.mouseadjustment, 98, 19, 9, "READHCOLOR");
                        video.Update();
                        audio.Play("menu/move1");
                        menuInput.TicDelay(20);
                    }
                    break;
            }

            if (ci.button0 || input.IsKeyDown(ScanCodes.Space) || input.IsKeyDown(ScanCodes.Enter))
                exit = 1;
            else if (ci.button1 || input.IsKeyDown(ScanCodes.Escape))
                exit = 2;

        }
        while (exit == 0);

        if (exit == 2)
        {
            settings.mouseadjustment = oldMA;
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
        draw.DrawOutline(60 + 20 * settings.mouseadjustment, 97, 20, 10, "Black", "READCOLOR");
        video.Bar(61 + 20 * settings.mouseadjustment, 98, 19, 9, "READHCOLOR");

        video.Update();
        draw.MenuFadeIn();
    }
}
