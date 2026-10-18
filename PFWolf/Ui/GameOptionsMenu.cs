using PFWolf.Configuration;

namespace PFWolf.Ui;

/// <summary>
/// The Game Options menu (menudefs/game-options): a checkbox for each of the on/off settings a
/// game pack can give defaults for (Configuration/GameOptions.cs). Switching one makes it the
/// player's own choice.
/// </summary>
internal sealed class GameOptionsMenu(VideoManager video, HudMessageManager messages, GameOptions options,
    MenuDrawing draw, MenuLoop loop, MenuInput menuInput, MenuDefs defs)
{
    // Loaded from menudefs/ in CheckForEpisodes
    internal ControlPanelItem[] Items = [];
    internal ControlPanelMenuInfo Info = null!;

    /// <summary>Each menu item's setting, by its menudefs id</summary>
    internal static readonly (string Id, GameOption Option)[] ItemOptions =
    [
        ("freelook", GameOption.Look),
        ("messages", GameOption.Messages),
        ("automapstats", GameOption.AutomapStats),
        ("autosave", GameOption.Autosave),
        ("weaponbob", GameOption.WeaponBob),
        ("alwaysrun", GameOption.AlwaysRun),
        ("crosshair", GameOption.Crosshair),
    ];

    internal int Run()
    {
        int which;

        DrawGameOptionsMenu();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();

        do
        {
            which = loop.HandleMenu(Info, Items, null);
            var id = MenuDefs.SelectedId(Items, which);
            foreach (var (itemId, option) in ItemOptions)
            {
                if (itemId != id)
                    continue;

                options.Toggle(option);
                if (option == GameOption.Messages && !options.Messages)
                    messages.Clear();
                DrawGameOptionsMenu();
                loop.ShootSnd();
            }
        }
        while (which >= 0);

        draw.MenuFadeOut();

        return 0;
    }

    internal void DrawGameOptionsMenu()
    {
        defs.DrawMenuComponents("game-options");
        draw.DrawMenu(Info, Items);
        foreach (var (id, option) in ItemOptions)
            draw.DrawMenuCheckbox(Info, Items, id, options[option]);
        draw.DrawMenuGun(Info);
        video.Update();
    }
}
