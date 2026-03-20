using System.Numerics;
using Wolf3D.Managers;

namespace Wolf3D.Entities;

internal class MenuMetadata
{
    public Vector2 Position { get; set; } = Vector2.Zero;
    public int Indent { get; set; } = 24;

    /// <summary>
    /// The type of prefab menu this instance can build from
    /// </summary>
    public string? Type { get; set; } = null;

    /// <summary>
    /// Music track asset name to play when entering this menu.
    /// (If the track name is the same as a previous menu transition,
    /// it will continue to play, unless you use the "MusicForceRestart"
    /// </summary>
    public string? Music { get; set; } = null;

    /// <summary>
    /// List of drawable items on the menu, list them in order of
    /// drawing bottom to top
    /// </summary>
    public List<MenuComponent> Components { get; set; } = new();

    public List<MenuItem> MenuItems { get; set; } = new();

    // TODO: This shouldn't be in the metadata, but in a menu/menuitem engine-only model
    public int CursorPosition { get; private set; } = 0;

    private bool redrawItem = true;
    private int lastItem = -1;
    public int Handle(GraphicManager graphicManager, VideoManager videoManager, InputManager inputManager)
    {
        char key;
        int i, x, y, basey, exit, which;
        string shape;
        int lastBlinkTime, timer;
        ControlInfo ci;

        which = CursorPosition;
        x = (int)Position.X & -8;
        basey = (int)Position.Y - 2;
        y = basey + which * 13;

        graphicManager.DrawPic("c_cursor1", x, y);
        MenuItems.ForEach(mi => mi.SetHighlighted(false));
        MenuItems[which].SetHighlighted(true);
        if (redrawItem)
            Draw(graphicManager);


        //
        // CALL CUSTOM ROUTINE IF IT IS NEEDED
        //
        //routine?.Invoke(which); // TODO:

        videoManager.Update();

        shape = "c_cursor1";
        timer = 8;
        exit = 0;
        lastBlinkTime = (int)GameEngineManager.GetTimeCount();

        inputManager.ClearKeysDown();
        inputManager.ClearTextInput();


        do
        {
            //
            // CHANGE GUN SHAPE
            //
            if ((int)GameEngineManager.GetTimeCount() - lastBlinkTime > timer)
            {
                lastBlinkTime = (int)GameEngineManager.GetTimeCount();
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

                graphicManager.DrawPic(shape, x, y);
                // routine?.Invoke(which); // TODO:
                videoManager.Update();
            }
            else
                GameEngineManager.DelayMs(5);

            //CheckPause();

            //
            // SEE IF ANY KEYS ARE PRESSED FOR INITIAL CHAR FINDING
            //

            inputManager.ReadControl(out ci);
            key = inputManager.GetTextInput()[0];

            inputManager.ClearTextInput();
            if (key != 0)
            {
                int ok = 0;

                if (key >= 'a')
                    key -= (char)('a' - 'A');

                for (i = which + 1; i < MenuItems.Count; i++)
                    if (MenuItems[i].IsEnabled && MenuItems[i].Key == key)
                    {
                        //EraseGun(item_i, items, x, y, which);
                        which = i;
                        //DrawGun(item_i, items, x, ref y, which, basey, routine);
                        ok = 1;
                        inputManager.ClearKeysDown();
                        break;
                    }

                //
                // DIDN'T FIND A MATCH FIRST TIME THRU. CHECK AGAIN.
                //
                if (ok == 0)
                {
                    for (i = 0; i < which; i++)
                        if (MenuItems[i].IsEnabled && MenuItems[i].Key == key)
                        {
                            //EraseGun(item_i, items, x, y, which);
                            which = i;
                            //DrawGun(item_i, items, x, ref y, which, basey, routine);
                            inputManager.ClearKeysDown();
                            break;
                        }
                }
            }

            //
            // GET INPUT
            //
            switch (ci.dir)
            {
                ////////////////////////////////////////////////
                //
                // MOVE UP
                //
                case Direction.dir_North:

                    //EraseGun(item_i, items, x, y, which);

                    //
                    // ANIMATE HALF-STEP
                    //
                    if (which != 0 && MenuItems[which - 1].IsEnabled)
                    {
                        y -= 6;
                        //DrawHalfStep(x, y);
                    }

                    //
                    // MOVE TO NEXT AVAILABLE SPOT
                    //
                    do
                    {
                        if (which == 0)
                            which = MenuItems.Count - 1;
                        else
                            which--;
                    }
                    while (!MenuItems[which].IsEnabled);

                    //DrawGun(item_i, items, x, ref y, which, basey, routine);
                    //
                    // WAIT FOR BUTTON-UP OR DELAY NEXT MOVE
                    //
                    TicDelay(20, inputManager);
                    break;

                ////////////////////////////////////////////////
                //
                // MOVE DOWN
                //
                case Direction.dir_South:

                    //EraseGun(item_i, items, x, y, which);
                    //
                    // ANIMATE HALF-STEP
                    //
                    if (which != MenuItems.Count - 1 && MenuItems[which + 1].IsEnabled)
                    {
                        y += 6;
                        //DrawHalfStep(x, y);
                    }

                    do
                    {
                        if (which == MenuItems.Count - 1)
                            which = 0;
                        else
                            which++;
                    }
                    while (!MenuItems[which].IsEnabled);

                    //DrawGun(item_i, items, x, ref y, which, basey, routine);

                    //
                    // WAIT FOR BUTTON-UP OR DELAY NEXT MOVE
                    //
                    //TicDelay(20);
                    break;
            }

            if (/*ci.button0 ||*/ inputManager.IsKeyDown(ScanCodes.sc_Space) || inputManager.IsKeyDown(ScanCodes.sc_Enter))
                exit = 1;

            if (/*ci.button1 && inputManager.IsKeyDown(ScanCodes.sc_Alt)  ||*/ inputManager.IsKeyDown(ScanCodes.sc_Escape))
                exit = 2;

        }
        while (exit == 0);
        inputManager.ClearKeysDown();

        //
        // ERASE EVERYTHING
        //
        if (lastItem != which)
        {
            videoManager.Bar(x - 1, y, 25, 16, 0x29);
            //PrintX = (ushort)((int)Position.X + Indent);
            //PrintY = (ushort)((int)Position.Y + which * 13);
            //US_Print(items[which].text);
            videoManager.Update();
            redrawItem = true;
        }
        else
            redrawItem = false;

        //routine?.Invoke(which);
        videoManager.Update();

        CursorPosition = (short)which;

        lastItem = which;
        switch (exit)
        {
            case 1:
                //
                // CALL THE ROUTINE
                //
                //if (items[which].routine != null)
                //{
                //    ShootSnd();
                //    MenuFadeOut();
                //    items[which].routine!.Invoke(0);
                //}
                return which;

            case 2:
                //SD_PlaySound((int)soundnames.ESCPRESSEDSND);
                return -1;
        }

        return 0; // JUST TO SHUT UP THE ERROR MESSAGES!
    }

    internal void Draw(GraphicManager graphicManager)
    {
        foreach (var menuComponent in Components)
        {
            graphicManager.DrawComponent(menuComponent);
        }

        int i = 0;
        foreach (var menuItem in MenuItems)
        {
            // picture (button, or episode)
            var textColor = menuItem.IsHighlighted ? (byte)0x13 : (byte)0x17;
            // TODO: This is missing newlines, and string measurements
            // In the future, the "Font As Graphic" would benefit greatly here
            graphicManager.DrawPropString((int)Position.X + Indent, (int)Position.Y + (i*13), menuItem.Text, textColor, 1);
            i++;
        }
    }
    private static void TicDelay(int count, InputManager inputManager)
    {
        ControlInfo ci;

        int startTime = (int)GameEngineManager.GetTimeCount();

        do
        {
            GameEngineManager.DelayMs(5);
            inputManager.ReadControl(out ci);
        }
        while ((int)GameEngineManager.GetTimeCount() - startTime < count && ci.dir != Direction.dir_None);
    }
}

internal abstract record MenuComponent
{

}

internal record Background : MenuComponent
{
    public byte Color { get; set; }

    public Background(byte color)
    {
        Color = color;    
    }
}

internal record Window : MenuComponent
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public Window(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public Window (int x, int y, int width, int height, string theme)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;

        // TODO: Theme is another asset that stores color information
    }
}

internal record Graphic : MenuComponent
{
    public string Asset { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public HorizontalOrientation OrientationX { get; set; } = HorizontalOrientation.Left;
    public VerticalOrientation OrientationY { get; set; } = VerticalOrientation.Top;

    public Graphic(string asset, int x, int y)
    {
        Asset = asset;
        X = x;
        Y = y;
    }

    public Graphic(string asset, int x, VerticalOrientation y)
    {
        Asset = asset;
        X = x;
        OrientationY = y;
    }

    public Graphic(string asset, HorizontalOrientation x, int y)
    {
        Asset = asset;
        OrientationX = x;
        Y = y;
    }

    public Graphic(string asset, HorizontalOrientation x, VerticalOrientation y)
    {
        Asset = asset;
        OrientationX = x;
        OrientationY = y;
    }
}

internal record Stripe : MenuComponent
{
    public int Y { get; set; }
    public byte BackingColor { get; set; }
    public byte LineColor { get; set; }
    public Stripe(int y)
    {
        Y = y;
        BackingColor = 0x00;
        LineColor = 0x2c;
    }

    public Stripe(int y, byte backingColor, byte lineColor)
    {
        Y = y;
        BackingColor = backingColor;
        LineColor = lineColor;
    }
}

internal enum HorizontalOrientation
{
    Left,
    Center,
    Right
}

internal enum VerticalOrientation
{
    Top,
    Center,
    Bottom
}

internal abstract record MenuItem
{
    /// <summary>
    /// Shortcut key press to jump to this menu item
    /// </summary>
    public char Key { get; set; }
    public string Text { get; set; } = null!;
    public bool IsEnabled { get; set; } = false;
    public bool IsHighlighted { get; private set; } = false;

    internal void SetHighlighted(bool isHighlighted)
    {
        IsHighlighted = isHighlighted;
    }
}

internal record MenuSwitcher : MenuItem
{
    public MenuSwitcher(string text, bool isEnabled, string action)
    {
        Key = text[0];
        Text = text;
        IsEnabled = isEnabled;
        Action = action;
    }

    public string? Action { get; init; } = null;
}