namespace PFWolf.Ui;

internal sealed class OptionsMenu(VideoManager video, MenuDrawing draw, MenuLoop loop, MenuInput menuInput, MenuDefs defs)
{
    // Loaded from menudefs/ in CheckForEpisodes
    internal ControlPanelItem[] OptMenu = [];
    internal ControlPanelMenuInfo OptItems = null!;
    /// <summary>
    /// The Options submenu. Each item runs its own screen (Sound, Control, Change View, Video,
    /// Game Options, Mods), and this menu is drawn again when that screen is left.
    /// </summary>
    internal int Run()
    {
        int which;

        DrawOptionsMenu();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();

        do
        {
            which = loop.HandleMenu(OptItems, OptMenu, null);
            if (which >= 0)
            {
                DrawOptionsMenu();
                draw.MenuFadeIn();
                menuInput.WaitKeyUp();
            }
        }
        while (which >= 0);

        draw.MenuFadeOut();

        return 0;
    }

    internal void DrawOptionsMenu()
    {
        defs.DrawMenuComponents("options");
        draw.DrawMenu(OptItems, OptMenu);
        draw.DrawMenuGun(OptItems);
        video.Update();
    }
}
