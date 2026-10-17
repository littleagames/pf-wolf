namespace PFWolf.Ui;

internal sealed class OptionsMenu(VideoManager video, HudMessageManager messages, AutomapManager automap,
    MenuDrawing draw, MenuLoop loop, MenuInput menuInput, MenuDefs defs)
{
    // Loaded from menudefs/ in CheckForEpisodes
    internal ControlPanelItem[] OptMenu = [];
    internal ControlPanelMenuInfo OptItems = null!;
    /// <summary>
    /// The Options submenu. Most items run their own screen (Sound, Control, Change View,
    /// Video), and this menu is drawn again when that screen is left; Messages and Automap Stats
    /// switch on and off in place.
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
            if (MenuDefs.SelectedId(OptMenu, which) == "messages")
            {
                // The player's own choice from now on, over the game pack's default (msg_enabled)
                messages.EnabledSetting = !messages.Enabled;
                if (!messages.Enabled)
                    messages.Clear();
                DrawOptionsMenu();
                loop.ShootSnd();
            }
            else if (MenuDefs.SelectedId(OptMenu, which) == "automapstats")
            {
                automap.ShowStats = !automap.ShowStats;
                DrawOptionsMenu();
                loop.ShootSnd();
            }
            else if (which >= 0)
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
        draw.DrawMenuCheckbox(OptItems, OptMenu, "messages", messages.Enabled);
        draw.DrawMenuCheckbox(OptItems, OptMenu, "automapstats", automap.ShowStats);
        draw.DrawMenuGun(OptItems);
        video.Update();
    }
}
