using PFWolf.Configuration;
using PFWolf.Extensions;

namespace PFWolf.Ui;

/// <summary>What the in-play keys need from the game</summary>
/// <param name="DemoPlayback">Whether a demo is playing (any key ends it, elsewhere)</param>
/// <param name="DemoRecord">Whether a demo is recording (no console, no binds)</param>
/// <param name="NetGame">Whether this is a game with others (no cheats, pausing or menus)</param>
/// <param name="NetChatting">Typing something to say to the others: the keys are the line's</param>
/// <param name="NetChatKeys">The chat line's keys (Ui/NetOverlay.cs)</param>
/// <param name="NetCheckKeys">The keys in a game with others: Esc asks to leave (Ui/NetOverlay.cs)</param>
/// <param name="ToggleAutomap">Opens or shuts the automap (Program.Automap.cs)</param>
/// <param name="HandleAutomapKey">The automap's own keys while it's open: true if it took one</param>
/// <param name="ViewSize">The view's size setting (a full screen view has no border to redraw)</param>
/// <param name="DrawPlayBorder">Redraws the border around the view</param>
/// <param name="DrawPlayBorderSides">Redraws the border's sides</param>
/// <param name="EnableDebug">Turns the cheat commands on (DebugOk)</param>
/// <param name="ControlPanel">The menu, opened on a key (Program.US_ControlPanel)</param>
/// <param name="DrawPlayScreen">Redraws the play screen after the menu</param>
/// <param name="StartGame">Whether the menu started a new game</param>
/// <param name="LoadedGame">Whether the menu loaded a game</param>
/// <param name="SetPlayState">Ends the level (a game was loaded)</param>
internal sealed record PlayKeysHooks(
    Func<bool> DemoPlayback,
    Func<bool> DemoRecord,
    Func<bool> NetGame,
    Func<bool> NetChatting,
    Action NetChatKeys,
    Action<ScanCodes> NetCheckKeys,
    Action ToggleAutomap,
    Func<InputCode, bool> HandleAutomapKey,
    Func<int> ViewSize,
    Action DrawPlayBorder,
    Action DrawPlayBorderSides,
    Action EnableDebug,
    Action<ScanCodes> ControlPanel,
    Action DrawPlayScreen,
    Func<bool> StartGame,
    Func<bool> LoadedGame,
    Action<playstatetypes> SetPlayState);

/// <summary>
/// The keys during play, once a frame (CheckKeys): the console, the automap, console binds, the
/// original's cheat combos, pause, and the F-keys and Esc that open the menu.
/// </summary>
internal sealed class PlayKeys(
    AssetManager assets,
    VideoManager video,
    InputManager inputManager,
    AudioManager audio,
    GraphicManager graphics,
    GameEngineManager engine,
    ConsoleManager console,
    AutomapManager automap,
    InventoryManager inventory,
    MenuDrawing draw,
    InputPolling polling,
    StatusBarDrawing statusBar,
    PlayerWeapons weapons,
    PlayerClassRules classes,
    LevelMusic music,
    FrameRefresh frame,
    GameSession session,
    PlayKeysHooks hooks)
{
    internal void CheckKeys()
    {
        var language = assets.GetText("en-us");
        ScanCodes scan;

        if (video.screenfaded || hooks.DemoPlayback())    // don't do anything with a faded screen
        {
            while (inputManager.TryTakePressedKey(out _)) { }     // nor fire binds for these keys later
            return;
        }

        //
        // command console: it only opens from here, so it's never up during a blocking menu or
        // screen with nothing drawing it. Once open, InputManager routes all keys to it.
        //
        if (console.IsOpen)
            return;

        // Typing something to say to the others: the keys are the line's (Ui/NetOverlay.cs)
        if (hooks.NetChatting())
        {
            hooks.NetChatKeys();
            return;
        }

        if (inputManager.IsKeyDown(ScanCodes.sc_Grave) && !hooks.DemoRecord())
        {
            inputManager.ClearKeysDown();
            inputManager.ClearTextInput();
            automap.Close();
            console.Open();
            return;
        }

        //
        // automap
        //
        if (inputManager.IsButtonPressed(buttontypes.bt_automap) && !inputManager.IsButtonHeld(buttontypes.bt_automap))
            hooks.ToggleAutomap();

        //
        // console binds: run the command bound to each key or button pressed since the last
        // frame (never while recording, or the demo wouldn't match what was played)
        //
        var pressedInputs = new List<InputCode>();
        while (inputManager.TryTakePressedKey(out var pressed))
            pressedInputs.Add(InputCode.FromKey(pressed));
        pressedInputs.AddRange(polling.TakeFreshPresses());

        foreach (var input in pressedInputs)
        {
            if (hooks.HandleAutomapKey(input))        // the automap's own keys, while it's open
                continue;

            if (!hooks.DemoRecord() && console.Binds.TryGetValue(input, out var boundCommand))
                console.Execute(boundCommand);
        }

        scan = inputManager.GetLastKeyPressed();

        // With others: no cheat keys, no pausing and no menus over the game; Esc asks to leave
        if (hooks.NetGame())
        {
            hooks.NetCheckKeys(scan);
            return;
        }

        //
        // SECRET CHEAT CODE: 'MLI'
        //
        if (inputManager.IsKeyDown(ScanCodes.sc_M) && inputManager.IsKeyDown(ScanCodes.sc_L) && inputManager.IsKeyDown(ScanCodes.sc_I))
        {
            session.playerstate.health = classes.MaxHealth;
            inventory.Give("GoldKey", 1);
            inventory.Give("SilverKey", 1);
            session.playerstate.score = 0;
            session.gamestate.TimeCount += (int)42000L;
            // the best weapon there is (the gatling gun)
            if (weapons.AllWeapons().OrderBy(weapons.WeaponSelectionOrder).FirstOrDefault() is { } bestWeapon)
                weapons.GiveWeapon(bestWeapon);
            weapons.GiveAllAmmo(99);
            weapons.DrawWeapon();
            statusBar.DrawHealth();
            statusBar.DrawKeys();
            weapons.DrawAmmo();
            statusBar.DrawScore();

            audio.StopAll();

            draw.Message("$STR_CHEATER1".ToLanguageText(language) + "\n" +
                    "$STR_CHEATER2".ToLanguageText(language) + "\n\n" +
                    "$STR_CHEATER3".ToLanguageText(language) + "\n" +
                    "$STR_CHEATER4".ToLanguageText(language) + "\n" +
                    "$STR_CHEATER5".ToLanguageText(language), GameConstants.MAXY);

            inputManager.ClearKeysDown();
            inputManager.Ack();

            if (hooks.ViewSize() < 17)
                hooks.DrawPlayBorder();
        }

        //
        // OPEN UP DEBUG KEYS
        //
        if (inputManager.IsKeyDown(ScanCodes.sc_BackSpace) && inputManager.IsKeyDown(ScanCodes.sc_LShift) && inputManager.IsKeyDown(ScanCodes.sc_Alt))
        {
            audio.StopAll();

            draw.Message("Cheat commands are\nnow available!\nPress ` for the console.", GameConstants.MAXY);
            inputManager.ClearKeysDown();
            inputManager.Ack();

            hooks.DrawPlayBorderSides();
            hooks.EnableDebug();
        }

        //
        // TRYING THE KEEN CHEAT CODE!
        //
        if (inputManager.IsKeyDown(ScanCodes.sc_B) && inputManager.IsKeyDown(ScanCodes.sc_A) && inputManager.IsKeyDown(ScanCodes.sc_T))
        {
            audio.StopAll();

            draw.Message("Commander Keen is also\n" +
                        "available from Apogee, but\n" +
                        "then, you already know\n" +
                        "that - right, Cheatmeister?!", GameConstants.MAXY);

            inputManager.ClearKeysDown();
            inputManager.Ack();

            if (hooks.ViewSize() < 18)
                hooks.DrawPlayBorder();
        }

        //
        // pause key weirdness can't be checked as a scan code
        //
        if (inputManager.IsButtonPressed(buttontypes.bt_pause))
            engine.SetPaused(true);
        if (engine.IsPaused())
        {
            int lastoffs = music.StopMusic();
            graphics.DrawPic("paused", 16 * 8, 80 - 2 * 8);
            video.Update();
            inputManager.Ack();
            engine.SetPaused(false);
            music.ContinueMusic(lastoffs);
            inputManager.CenterMouse();
            frame.lasttimecount = (int)GameEngineManager.GetTimeCount();
            return;
        }
        if (scan == ScanCodes.sc_F10 ||
            scan == ScanCodes.sc_F9 || scan == ScanCodes.sc_F7 || scan == ScanCodes.sc_F8)     // pop up quit dialog
        {
            automap.Close();
            audio.StopAll();
            hooks.ControlPanel(scan);

            hooks.DrawPlayBorderSides();

            inputManager.ClearKeysDown();
            return;
        }

        if ((scan >= ScanCodes.sc_F1 && scan <= ScanCodes.sc_F9) || scan == ScanCodes.sc_Escape || inputManager.IsButtonPressed(buttontypes.bt_esc))
        {
            int lastoffs = music.StopMusic();
            automap.Close();
            audio.StopAll();
            video.FadeOut();

            hooks.ControlPanel(inputManager.IsButtonPressed(buttontypes.bt_esc) ? ScanCodes.sc_Escape : scan);

            inputManager.ClearKeysDown();
            video.FadeOut();
            if (hooks.ViewSize() != 21)
                hooks.DrawPlayScreen();
            if (!hooks.StartGame() && !hooks.LoadedGame())
                music.ContinueMusic(lastoffs);
            if (hooks.LoadedGame())
                hooks.SetPlayState(playstatetypes.ex_abort);
            frame.lasttimecount = (int)GameEngineManager.GetTimeCount();
            inputManager.CenterMouse();
            return;
        }
    }
}
