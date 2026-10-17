using PFWolf.Configuration;
using PFWolf.Extensions;

namespace PFWolf.Ui;

/// <summary>What the in-play keys need from the game</summary>
/// <param name="NetChatting">Typing something to say to the others: the keys are the line's</param>
/// <param name="NetChatKeys">The chat line's keys (Ui/NetOverlay.cs)</param>
/// <param name="NetCheckKeys">The keys in a game with others: Esc asks to leave (Ui/NetOverlay.cs)</param>
/// <param name="ToggleAutomap">Opens or shuts the automap (Ui/AutomapInput.cs)</param>
/// <param name="HandleAutomapKey">The automap's own keys while it's open: true if it took one</param>
/// <param name="DrawPlayBorder">Redraws the border around the view</param>
/// <param name="DrawPlayBorderSides">Redraws the border's sides</param>
/// <param name="ControlPanel">The menu, opened on a key (GameHost.OpenControlPanel)</param>
/// <param name="DrawPlayScreen">Redraws the play screen after the menu</param>
internal sealed record PlayKeysHooks(
    Func<bool> NetChatting,
    Action NetChatKeys,
    Action<ScanCodes> NetCheckKeys,
    Action ToggleAutomap,
    Func<InputCode, bool> HandleAutomapKey,
    Action DrawPlayBorder,
    Action DrawPlayBorderSides,
    Action<ScanCodes> ControlPanel,
    Action DrawPlayScreen);

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
    Configuration.GameSettings settings,
    PlayKeysHooks hooks)
{
    internal void CheckKeys()
    {
        var language = assets.GetText("en-us");
        ScanCodes scan;

        if (video.screenfaded || session.demoplayback)    // don't do anything with a faded screen
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

        if (inputManager.IsKeyDown(ScanCodes.Grave) && !session.demorecord)
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
        if (inputManager.IsButtonPressed(ButtonType.Automap) && !inputManager.IsButtonHeld(ButtonType.Automap))
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

            if (!session.demorecord && console.Binds.TryGetValue(input, out var boundCommand))
                console.Execute(boundCommand);
        }

        scan = inputManager.GetLastKeyPressed();

        // With others: no cheat keys, no pausing and no menus over the game; Esc asks to leave
        if (session.netgame)
        {
            hooks.NetCheckKeys(scan);
            return;
        }

        //
        // SECRET CHEAT CODE: 'MLI'
        //
        if (inputManager.IsKeyDown(ScanCodes.M) && inputManager.IsKeyDown(ScanCodes.L) && inputManager.IsKeyDown(ScanCodes.I))
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
                    "$STR_CHEATER5".ToLanguageText(language), GameConstants.MaxY);

            inputManager.ClearKeysDown();
            inputManager.Ack();

            if (settings.viewsize < 17)
                hooks.DrawPlayBorder();
        }

        //
        // OPEN UP DEBUG KEYS
        //
        if (inputManager.IsKeyDown(ScanCodes.BackSpace) && inputManager.IsKeyDown(ScanCodes.LShift) && inputManager.IsKeyDown(ScanCodes.Alt))
        {
            audio.StopAll();

            draw.Message("Cheat commands are\nnow available!\nPress ` for the console.", GameConstants.MaxY);
            inputManager.ClearKeysDown();
            inputManager.Ack();

            hooks.DrawPlayBorderSides();
            session.DebugOk = 1;
        }

        //
        // TRYING THE KEEN CHEAT CODE!
        //
        if (inputManager.IsKeyDown(ScanCodes.B) && inputManager.IsKeyDown(ScanCodes.A) && inputManager.IsKeyDown(ScanCodes.T))
        {
            audio.StopAll();

            draw.Message("Commander Keen is also\n" +
                        "available from Apogee, but\n" +
                        "then, you already know\n" +
                        "that - right, Cheatmeister?!", GameConstants.MaxY);

            inputManager.ClearKeysDown();
            inputManager.Ack();

            if (settings.viewsize < 18)
                hooks.DrawPlayBorder();
        }

        //
        // pause key weirdness can't be checked as a scan code
        //
        if (inputManager.IsButtonPressed(ButtonType.Pause))
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
        if (scan == ScanCodes.F10 ||
            scan == ScanCodes.F9 || scan == ScanCodes.F7 || scan == ScanCodes.F8)     // pop up quit dialog
        {
            automap.Close();
            audio.StopAll();
            hooks.ControlPanel(scan);

            hooks.DrawPlayBorderSides();

            inputManager.ClearKeysDown();
            return;
        }

        if ((scan >= ScanCodes.F1 && scan <= ScanCodes.F9) || scan == ScanCodes.Escape || inputManager.IsButtonPressed(ButtonType.Esc))
        {
            int lastoffs = music.StopMusic();
            automap.Close();
            audio.StopAll();
            video.FadeOut();

            hooks.ControlPanel(inputManager.IsButtonPressed(ButtonType.Esc) ? ScanCodes.Escape : scan);

            inputManager.ClearKeysDown();
            video.FadeOut();
            if (settings.viewsize != 21)
                hooks.DrawPlayScreen();
            if (!session.startgame && !session.loadedgame)
                music.ContinueMusic(lastoffs);
            if (session.loadedgame)
                session.playstate = PlayState.Abort;
            frame.lasttimecount = (int)GameEngineManager.GetTimeCount();
            inputManager.CenterMouse();
            return;
        }
    }
}
