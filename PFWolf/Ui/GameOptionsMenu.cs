using PFWolf.Configuration;
using PFWolf.Menus;

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
        ("weaponbobsway", GameOption.WeaponBobSway),
        ("alwaysrun", GameOption.AlwaysRun),
        ("crosshair", GameOption.Crosshair),
        ("lighting", GameOption.Lighting),
        ("attackinfo", GameOption.AttackInfo),
        ("ceilings", GameOption.ShowCeilings),
        ("floors", GameOption.ShowFloors),
        ("heartbeat", GameOption.HeartbeatSound),
        ("objectivemarkers", GameOption.ObjectiveMarkers),
        ("soundmuffling", GameOption.SoundMuffling),
    ];

    // Where the marks go while the rows scroll (visible-rows): inside the window's right edge, clear
    // of the longest names (as the episode and Mods menus have them), or well past a highlight bar's
    // end, in the terminal still (Blake Stone's: REBA ATTACK INFO comes near the bar's end)
    const int ScrollMarkX = 300, BarScrollMarkGap = 51;
    static int MarkX(ControlPanelMenuInfo info) =>
        info.ScrollMarkX ?? (info.Cursor is { } bar ? bar.X + bar.Width + BarScrollMarkGap : ScrollMarkX);

    internal int Run()
    {
        int which;

        DrawGameOptionsMenu();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();

        do
        {
            which = loop.HandleMenu(Info, Items, null);
            if (which == -2)
            {
                DrawGameOptionsMenu();      // scrolled (visible-rows): drawn again where it's got to
                continue;
            }
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
        while (which >= 0 || which == -2);

        draw.MenuFadeOut();

        return 0;
    }

    internal void DrawGameOptionsMenu()
    {
        defs.DrawMenuComponents("game-options");
        draw.DrawMenu(Info, Items);
        foreach (var (id, option) in ItemOptions)
            draw.DrawMenuCheckbox(Info, Items, id, options[option]);
        draw.DrawMenuScrollMarks(Info, Items, MarkX(Info));
        draw.DrawMenuGun(Info);
        video.Update();
    }
}
