using PFWolf.Extensions;

namespace PFWolf.Ui;

/// <summary>
/// A menu being used: the gun (or a highlight bar) moved up and down its rows, an item picked
/// by its first letter, left and right adjusting a row, and picking or backing out; and the
/// Y/N question (Confirm)
/// </summary>
internal sealed class MenuLoop(MenuDrawing draw, MenuInput menuInput, GraphicManager graphics, VideoManager video,
    InputManager input, AudioManager audio, AssetManager assets)
{
    internal int redrawitem = 1, lastitem = -1;

    /// <param name="adjust">
    /// Called with the highlighted item and -1 or +1 when left or right is pressed, for menus
    /// with rows that change in place (sliders, choices). Without it, left/right do nothing.
    /// </param>
    /// <param name="idle">
    /// Called again and again while the menu waits (a network game's screens take in what has
    /// arrived and redraw); returning true leaves the menu at once, with -2.
    /// </param>
    internal int HandleMenu(CP_iteminfo item_i, CP_itemtype[] items, Action<int>? routine, Action<int, int>? adjust = null, Func<bool>? idle = null)
    {
        char key;
        int i, x, y, basey, exit, which;
        string shape;
        int lastBlinkTime, timer;
        ControlInfo ci;

        which = item_i.curpos;
        x = item_i.x & -8;
        basey = item_i.y - 2;
        y = basey + which * item_i.rowHeight;
        // A highlight bar (Blake Stone's) flashes on and off, where the gun blinks its shape
        bool bar = item_i.cursor != null, barOn = true;

        if (bar)
            draw.DrawMenuBar(item_i, items, which, true);
        else
        {
            graphics.DrawPic("c_cursor1", x, y);
            if (redrawitem != 0)
                draw.PrintMenuItem(item_i, items, which, MenuDrawing.MenuItemColor(items[which], true));
        }
        //
        // CALL CUSTOM ROUTINE IF IT IS NEEDED
        //
        routine?.Invoke(which);

        video.Update();

        shape = "c_cursor1";
        timer = bar ? item_i.cursor!.FlashTics : 8;
        exit = 0;
        lastBlinkTime = (int)GameEngineManager.GetTimeCount();
        input.ClearKeysDown();
        input.ClearTextInput();

        do
        {
            //
            // CHANGE GUN SHAPE
            //
            if ((int)GameEngineManager.GetTimeCount() - lastBlinkTime > timer)
            {
                lastBlinkTime = (int)GameEngineManager.GetTimeCount();
                if (bar)
                {
                    barOn = !barOn;
                    draw.DrawMenuBar(item_i, items, which, barOn);
                }
                else
                {
                    if (shape == "c_cursor1")
                    {
                        shape = "c_cursor2";
                        timer = 8;
                    }
                    else
                    {
                        shape = "c_cursor1";
                        timer = 70;
                    }

                    graphics.DrawPic(shape, x, y);
                }
                routine?.Invoke(which);
                video.Update();
            }
            else
                GameEngineManager.DelayMs(5);

            menuInput.CheckPause();

            if (idle?.Invoke() == true)
            {
                exit = 3;
                break;
            }

            //
            // SEE IF ANY KEYS ARE PRESSED FOR INITIAL CHAR FINDING
            //

            key = input.GetTextInput()[0];

            input.ClearTextInput();

            if (key != 0)
            {
                int ok = 0;

                if (key >= 'a')
                    key -= (char)('a' - 'A');

                for (i = which + 1; i < item_i.amount; i++)
                {
                    if (items[i].active != 0 && ItemKey(items[i]) == key)
                    {
                        EraseGun(item_i, items, x, y, which);
                        which = i;
                        DrawGun(item_i, items, x, ref y, which, basey, routine);
                        ok = 1;
                        input.ClearKeysDown();
                        break;
                    }
                }

                //
                // DIDN'T FIND A MATCH FIRST TIME THRU. CHECK AGAIN.
                //
                if (ok == 0)
                {
                    for (i = 0; i < which; i++)
                        if (items[i].active != 0 && ItemKey(items[i]) == key)
                        {
                            EraseGun(item_i, items, x, y, which);
                            which = i;
                            DrawGun(item_i, items, x, ref y, which, basey, routine);
                            input.ClearKeysDown();
                            break;
                        }
                }
            }

            //
            // GET INPUT
            //
            menuInput.ReadAnyControl(out ci);
            switch (ci.dir)
            {
                ////////////////////////////////////////////////
                //
                // MOVE UP
                //
                case Direction.North:

                    EraseGun(item_i, items, x, y, which);

                    //
                    // ANIMATE HALF-STEP
                    //
                    if (!bar && which != 0 && items[which - 1].active != 0)
                    {
                        y -= 6;
                        DrawHalfStep(x, y);
                    }

                    //
                    // MOVE TO NEXT AVAILABLE SPOT
                    //
                    do
                    {
                        if (which == 0)
                            which = item_i.amount - 1;
                        else
                            which--;
                    }
                    while (items[which].active == 0);

                    DrawGun(item_i, items, x, ref y, which, basey, routine);
                    //
                    // WAIT FOR BUTTON-UP OR DELAY NEXT MOVE
                    //
                    menuInput.TicDelay(20);
                    break;

                ////////////////////////////////////////////////
                //
                // MOVE DOWN
                //
                case Direction.South:

                    EraseGun(item_i, items, x, y, which);
                    //
                    // ANIMATE HALF-STEP
                    //
                    if (!bar && which != item_i.amount - 1 && items[which + 1].active != 0)
                    {
                        y += 6;
                        DrawHalfStep(x, y);
                    }

                    do
                    {
                        if (which == item_i.amount - 1)
                            which = 0;
                        else
                            which++;
                    }
                    while (items[which].active == 0);

                    DrawGun(item_i, items, x, ref y, which, basey, routine);

                    //
                    // WAIT FOR BUTTON-UP OR DELAY NEXT MOVE
                    //
                    menuInput.TicDelay(20);
                    break;

                ////////////////////////////////////////////////
                //
                // ADJUST THE HIGHLIGHTED ITEM
                //
                case Direction.West when adjust != null:
                    adjust(which, -1);
                    menuInput.TicDelay(20);
                    break;

                case Direction.East when adjust != null:
                    adjust(which, 1);
                    menuInput.TicDelay(20);
                    break;
            }

            if (ci.button0 || input.IsKeyDown(ScanCodes.sc_Space) || input.IsKeyDown(ScanCodes.sc_Enter))
                exit = 1;

            if (ci.button1 && !input.IsKeyDown(ScanCodes.sc_Alt) || input.IsKeyDown(ScanCodes.sc_Escape))
                exit = 2;

        }
        while (exit == 0);
        input.ClearKeysDown();
        menuInput.WaitKeyUp();        // or a held button picks in, or backs out of, the next menu too

        //
        // ERASE EVERYTHING
        //
        if (bar)
            draw.DrawMenuBar(item_i, items, which, false);
        else if (lastitem != which)
        {
            video.Bar(x - 1, y, 25, 16, "BKGDCOLOR");
            draw.PrintMenuItem(item_i, items, which, MenuDrawing.MenuItemColor(items[which], true));
            redrawitem = 1;
        }
        else
            redrawitem = 0;

        routine?.Invoke(which);
        video.Update();

        item_i.curpos = (short)which;

        lastitem = which;
        switch (exit)
        {
            case 1:
                //
                // CALL THE ROUTINE
                //
                if (items[which].routine != null)
                {
                    ShootSnd();
                    draw.MenuFadeOut();
                    items[which].routine!.Invoke(0);
                }
                return which;

            case 2:
                audio.Play("menu/escape");
                return -1;

            case 3:
                return -2;      // idle asked to leave
        }

        return 0; // JUST TO SHUT UP THE ERROR MESSAGES!
    }

    internal static char ItemKey(CP_itemtype item)
    {
        if (item.shortKey != '\0')
            return char.ToUpperInvariant(item.shortKey);

        return string.IsNullOrWhiteSpace(item.text) ? '\0' : item.text[0];
    }

    internal void EraseGun(CP_iteminfo item_i, CP_itemtype[] items, int x, int y, int which)
    {
        if (item_i.cursor != null)
            draw.DrawMenuBar(item_i, items, which, false);
        else
        {
            video.Bar(x - 1, y, 25, 16, "BKGDCOLOR");
            draw.PrintMenuItem(item_i, items, which, MenuDrawing.MenuItemColor(items[which], false));
        }
        video.Update();
    }

    //
    // DRAW HALF STEP OF GUN TO NEXT POSITION
    //
    internal void DrawHalfStep(int x, int y)
    {
        graphics.DrawPic("c_cursor1", x, y);
        video.Update();
        audio.Play("menu/move1");
        GameEngineManager.DelayMs(8 * 100 / 7);
    }

    internal void DrawGun(CP_iteminfo item_i, CP_itemtype[] items, int x, ref int y, int which, int basey, Action<int>? routine)
    {
        if (item_i.cursor != null)
        {
            y = basey + which * item_i.rowHeight;
            draw.DrawMenuBar(item_i, items, which, true);
        }
        else
        {
            video.Bar(x - 1, y, 25, 16, "BKGDCOLOR");
            y = basey + which * item_i.rowHeight;
            graphics.DrawPic("c_cursor1", x, y);
            draw.PrintMenuItem(item_i, items, which, MenuDrawing.MenuItemColor(items[which], true));
        }

        //
        // CALL CUSTOM ROUTINE IF IT IS NEEDED
        //
        routine?.Invoke(which);
        video.Update();
        audio.Play("menu/move2");
    }

    internal void ShootSnd()
    {
        audio.Play("menu/activate"); // TODO: "shoot" is something else now
    }

    /// <param name="areaHeight">Height of the screen area the question is centered in: the play view (MAXY) in a game</param>
    internal int Confirm(string text, int areaHeight = 200)
    {
        var language = assets.GetText("en-us");
        int xit = 0, x, y, tick = 0, lastBlinkTime;
        string[] whichsnd = ["menu/escape", "menu/activate"];
        ControlInfo ci;

        var message = draw.Message(text.ToLanguageText(language), areaHeight);
        input.ClearKeysDown();
        menuInput.WaitKeyUp();

        //
        // BLINK CURSOR
        //
        x = message.PrintX;
        y = message.PrintY;
        lastBlinkTime = (int)GameEngineManager.GetTimeCount();

        do
        {
            menuInput.ReadAnyControl(out ci);

            if (GameEngineManager.GetTimeCount() - lastBlinkTime >= 10)
            {
                switch (tick)
                {
                    case 0:
                        video.Bar(x, y, 8, 13, message.Style.Background);
                        break;
                    case 1:
                        graphics.DrawText(x, y, "_", message.Style);
                        break;
                }
                video.Update();
                tick ^= 1;
                lastBlinkTime = (int)GameEngineManager.GetTimeCount();
            }
            else GameEngineManager.DelayMs(5);
        }
        while (!input.IsKeyDown(ScanCodes.sc_Y) && !input.IsKeyDown(ScanCodes.sc_N) && !input.IsKeyDown(ScanCodes.sc_Escape) && !ci.button0 && !ci.button1);

        if (input.IsKeyDown(ScanCodes.sc_Y) || ci.button0)
        {
            xit = 1;
            ShootSnd();
        }
        input.ClearKeysDown();
        menuInput.WaitKeyUp();

        audio.Play(whichsnd[xit]);

        return xit;
    }
}