namespace PFWolf.Ui;

/// <summary>
/// A menudefs menu run as a submenu (CP_Menu, MENU HOOKS): a mod's own menu, or any menu by
/// name. Its components are drawn, then its items; an item's action runs (another screen, a
/// text, a script), and the menu is drawn again when it's done. Escape leaves it.
/// </summary>
internal sealed class ModMenus(VideoManager video, MenuDrawing draw, MenuLoop loop, MenuInput menuInput, MenuDefs defs)
{
    internal int Run(string name)
    {
        CP_itemtype[] items;
        CP_iteminfo info;
        try
        {
            (items, info) = defs.LoadMenu(name);
        }
        catch (InvalidOperationException e)
        {
            Console.WriteLine($"CP_Menu: {e.Message}");
            return 0;
        }

        void Draw()
        {
            defs.DrawMenuComponents(name);
            draw.DrawMenu(info, items);
            draw.DrawMenuGun(info);
            video.Update();
        }

        Draw();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();

        int which;
        do
        {
            which = loop.HandleMenu(info, items, null);
            if (which >= 0)
            {
                Draw();
                draw.MenuFadeIn();
                menuInput.WaitKeyUp();
            }
        }
        while (which >= 0);

        draw.MenuFadeOut();
        return 0;
    }
}
