using CommandLine;
using SDL2;
using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Configuration;
using PFWolf.Constants;
using PFWolf.Entities;
using PFWolf.Extensions;
using PFWolf.Fonts;
using PFWolf.Managers;

namespace PFWolf;

internal static partial class GameHost
{
    internal const int SM_X = 48;
    internal const int SM_W = 250;

    internal const int SM_Y1 = 20;
    internal const int SM_H1 = 4 * 13 - 7;
    internal const int SM_Y2 = SM_Y1 + 5 * 13;
    internal const int SM_H2 = 4 * 13 - 7;
    internal const int SM_Y3 = SM_Y2 + 5 * 13;
    internal const int SM_H3 = 3 * 13 - 7;

    internal const int CTL_X = 24;
    internal const int CTL_Y = 86;
    internal const int CTL_W = 284;
    internal const int CTL_H = 60;

    /*
    =============================================================================

                                THE CONTROL PANEL

    The main menu loop and its screens are Ui/ControlPanel.cs, Ui/NewGameMenu.cs,
    Ui/OptionsMenu.cs and Ui/ChangeViewScreen.cs (made in InitServices); the in-play F7 to F10
    (CP_CheckQuick) are here. The CP_ routines keep their names: menudefs name the actions.

    =============================================================================
    */

    static ControlPanel controlPanel = null!;
    static NewGameMenu newGameMenu = null!;
    static OptionsMenu optionsMenu = null!;
    static ChangeViewScreen changeViewScreen = null!;

    // Control panel song: the main menu's music in menudefs/main-menu
    internal static string MENUSONG => controlPanel.MENUSONG;
    internal static string INTROSONG => controlPanel.INTROSONG;

    internal static CP_itemtype[] MainMenu { get => controlPanel.MainMenu; set => controlPanel.MainMenu = value; }
    internal static CP_iteminfo MainItems { get => controlPanel.MainItems; set => controlPanel.MainItems = value; }
    internal static CP_itemtype[] OptMenu { get => optionsMenu.OptMenu; set => optionsMenu.OptMenu = value; }
    internal static CP_iteminfo OptItems { get => optionsMenu.OptItems; set => optionsMenu.OptItems = value; }
    internal static CP_itemtype[] NewEmenu { get => newGameMenu.NewEmenu; set => newGameMenu.NewEmenu = value; }
    internal static CP_iteminfo NewEitems { get => newGameMenu.NewEitems; set => newGameMenu.NewEitems = value; }
    internal static CP_itemtype[] NewMenu { get => newGameMenu.NewMenu; set => newGameMenu.NewMenu = value; }
    internal static CP_iteminfo NewItems { get => newGameMenu.NewItems; set => newGameMenu.NewItems = value; }
    internal static CP_itemtype[] ClassMenu { get => newGameMenu.ClassMenu; set => newGameMenu.ClassMenu = value; }
    internal static CP_iteminfo ClassItems { get => newGameMenu.ClassItems; set => newGameMenu.ClassItems = value; }

    internal static int StartGame { get => controlPanel.StartGame; set => controlPanel.StartGame = value; }

    // The menu from play, and F7-F10 (Ui/QuickKeys.cs)
    static QuickKeys quickKeys = null!;     // made in InitServices

    internal static void US_ControlPanel(ScanCodes scancode) => quickKeys.US_ControlPanel(scancode);

    internal static int CP_NewGame(int _) => newGameMenu.Run();
    internal static int CP_Options(int _) => optionsMenu.Run();
    internal static int CP_ChangeView(int _) => changeViewScreen.Run();
    internal static int CP_ReadThis(int _) => controlPanel.ReadThis();
    internal static int CP_ViewScores(int _) => controlPanel.ViewScores();
    internal static int CP_EndGame(int _) => controlPanel.EndGame();
    internal static int CP_Quit(int _) => controlPanel.Quit();

    static ControlPanelHooks MakeControlPanelHooks() => new(
        InGame: () => ingame,
        GameStarted: () => startgame || loadedgame,
        SaveGame: CP_SaveGame,
        LoadGame: CP_LoadGame,
        EndGame: () =>
        {
            playerstate.lives = 0;
            playstate = playstatetypes.ex_died;
            LastAttacker = null;
        },
        Random: UiRandom);

    /// <summary>Starts a game from New Game; the control panel then leaves, and "Read This!" goes back to its normal color</summary>
    static void StartNewGame(short difficulty, EpisodeInfo episode, MapInfo? mapInfo, string? playerClass)
    {
        NewGame(difficulty, episode, mapInfo!, playerClass);
        StartGame = 1;
        FindMenuItem(MainMenu, "readthis")?.active = 1;
    }

    // The jukebox (Ui/Jukebox.cs); its songs are loaded from menudefs/ in CheckForEpisodes
    static Jukebox jukebox = null!;     // made in InitServices
    internal static CP_itemtype[] MusicMenu { get => jukebox.MusicMenu; set => jukebox.MusicMenu = value; }
    internal const int JukeboxPageSize = Jukebox.JukeboxPageSize;
    internal static CP_iteminfo MusicItems { get => jukebox.MusicItems; set => jukebox.MusicItems = value; }
    /*
    =============================================================================

                            MENU INPUT, DRAWING AND LOOP

    In Ui/MenuInput.cs, Ui/MenuDrawing.cs and Ui/MenuLoop.cs (made in InitServices); these
    forward to them.

    =============================================================================
    */

    static MenuInput menuInput = null!;
    static MenuDrawing menuDrawing = null!;
    static MenuLoop menuLoop = null!;

    private const int MenuSliderWidth = MenuDrawing.MenuSliderWidth;
    private const int MenuSliderOffset = MenuDrawing.MenuSliderOffset;
    private const int MenuChoiceWidth = MenuDrawing.MenuChoiceWidth;

    internal static void BevelBox(int x, int y, int width, int height, int hi, int med, int lo) => menuDrawing.BevelBox(x, y, width, height, hi, med, lo);
    internal static void BevelBox(int x, int y, int width, int height, string hi, string med, string lo) => menuDrawing.BevelBox(x, y, width, height, hi, med, lo);
    private static void DrawMenuCheckbox(CP_iteminfo iteminfo, CP_itemtype[] items, string id, bool on) => menuDrawing.DrawMenuCheckbox(iteminfo, items, id, on);
    private static void DrawMenuCheckbox(CP_iteminfo iteminfo, int index, bool on, bool highlighted = false) => menuDrawing.DrawMenuCheckbox(iteminfo, index, on, highlighted);

    internal static int HandleMenu(CP_iteminfo item_i, CP_itemtype[] items, Action<int>? routine, Action<int, int>? adjust = null, Func<bool>? idle = null)
        => menuLoop.HandleMenu(item_i, items, routine, adjust, idle);
    internal static void DrawGun(CP_iteminfo item_i, CP_itemtype[] items, int x, ref int y, int which, int basey, Action<int>? routine)
        => menuLoop.DrawGun(item_i, items, x, ref y, which, basey, routine);

    internal static int StartCPMusic(string song) => quickKeys.StartCPMusic(song);

    /*
    =============================================================================

                                SETTINGS MENUS

    Ui/VideoMenu.cs, Ui/SoundMenu.cs and Ui/ControlMenus.cs (made in InitServices). The CP_
    routines keep their names: menudefs name the actions.

    =============================================================================
    */

    static VideoMenu videoMenu = null!;
    static SoundMenu soundMenu = null!;
    static ControlMenus controlMenus = null!;

    internal static CP_itemtype[] VidMenu { get => videoMenu.Items; set => videoMenu.Items = value; }
    internal static CP_iteminfo VidItems { get => videoMenu.Info; set => videoMenu.Info = value; }
    internal static CP_itemtype[] SndMenu { get => soundMenu.Items; set => soundMenu.Items = value; }
    internal static CP_iteminfo SndItems { get => soundMenu.Info; set => soundMenu.Info = value; }
    internal static CP_itemtype[] CtlMenu { get => controlMenus.CtlMenu; set => controlMenus.CtlMenu = value; }
    internal static CP_iteminfo CtlItems { get => controlMenus.CtlItems; set => controlMenus.CtlItems = value; }
    internal static CP_itemtype[] JoyMenu { get => controlMenus.JoyMenu; set => controlMenus.JoyMenu = value; }
    internal static CP_iteminfo JoyItems { get => controlMenus.JoyItems; set => controlMenus.JoyItems = value; }

    internal static int CP_Video(int _) => videoMenu.Run();
    internal static int CP_Sound(int _) => soundMenu.Run();
    internal static int CP_Control(int _) => controlMenus.RunControl();
    internal static int CP_ControllerSettings(int _) => controlMenus.RunControllerSettings();
    internal static int MouseSensitivity(int _) => controlMenus.RunMouseSensitivity();

    internal static void FreeMusic()
    {
        //UNCACHEAUDIOCHUNK(STARTMUSIC + chunk);
    }

    internal static void CheckForEpisodes()
    {
        /*if (configdir != string.Empty)
        {
            if (!Directory.Exists(configdir))
            {
                try
                {
                    DirectoryInfo di = Directory.CreateDirectory(configdir);
                }
                catch (IOException e)
                {
                    _gameEngineManager.Quit($"The configuration directory \"{configdir}\" could not be created.");
                }
            }
        }*/

        // The running release's data files were checked for when the assets were loaded
        // Build every menudef now so problems in any of them are reported at startup
        foreach (var menuName in _assetManager.GetMenuNames())
        {
            var menu = _assetManager.GetMenu(menuName);
            if (menu == null)
                continue;

            var music = _gameEngineManager.GetGameInfo().MenuMusic ?? menu.Music;
            if (!string.IsNullOrEmpty(music) && !_audioManager.HasMusic(music))
                Console.WriteLine($"Menu '{menuName}': unknown music '{music}'");

            var packLists = menu.MenuItems.Select(item => item.GamePacks)
                .Concat(menu.Components.Select(component => component.GamePacks));
            foreach (var pack in packLists.Where(list => list != null).SelectMany(list => list!).Distinct())
            {
                if (pack != GamePackList.AnyPack && !_gameEngineManager.KnownGamePackIds.Contains(GamePackList.PackName(pack), StringComparer.OrdinalIgnoreCase))
                    Console.WriteLine($"Menu '{menuName}': unknown game pack '{pack}' in game-packs");
            }
        }

        (MainMenu, MainItems) = LoadMenu("main-menu");
        (OptMenu, OptItems) = LoadMenu("options");
        (VidMenu, VidItems) = LoadMenu("video");
        (SndMenu, SndItems) = LoadMenu("sound");
        (CtlMenu, CtlItems) = LoadMenu("control", curpos: -1);
        (JoyMenu, JoyItems) = LoadMenu("controller");
        customizeScreen.Rows = customizeScreen.LoadCustomizeRows();
        (NewEmenu, NewEitems) = LoadMenu("new-episode");
        (NewMenu, NewItems) = LoadMenu("new-game");
        (ClassMenu, ClassItems) = LoadMenu("new-class");

        (MusicMenu, MusicItems) = LoadMenu("jukebox");
        // Only the songs the game has (the shareware has 10 of the 27)
        MusicMenu = MusicMenu.Where(item => item.data is not string song || _audioManager.HasMusic(song)).ToArray();
        MusicItems.amount = (short)Math.Min(JukeboxPageSize, MusicMenu.Length);
    }

    /*
    =============================================================================

                                    MENUDEFS

    The menus menudefs/ defines are loaded and drawn by Ui/MenuDefs.cs (made in InitServices);
    these forward to it, and RegisterMenuDefs gives it the actions and items-sources that
    need the game.

    =============================================================================
    */

    static MenuDefs menuDefs = null!;

    private static (CP_itemtype[] items, CP_iteminfo info) LoadMenu(string name, short curpos = 0) => menuDefs.LoadMenu(name, curpos);
    internal static CP_itemtype? FindMenuItem(CP_itemtype[] items, string id) => MenuDefs.FindMenuItem(items, id);

    /// <summary>What menudefs' items can do, and the menus built from the game's data</summary>
    private static void RegisterMenuDefs()
    {
        // This list will be built with attributes or scripting on the pk3
        Func<int, int>[] actions =
        [
            CP_NewGame,
            CP_Multiplayer,
            CP_Options,
            CP_Video,
            CP_Mods,
            CP_Sound,
            CP_Control,
            CP_ControllerSettings,
            CP_LoadGame,
            CP_SaveGame,
            CP_ChangeView,
            CP_ReadThis,
            CP_ViewScores,
            CP_EndGame,
            CP_Quit,
            MouseSensitivity,
            CustomControls
        ];
        foreach (var action in actions)
            menuDefs.RegisterAction(action.Method.Name, action);
        menuDefs.TextScreen = script => _ => CP_TextScreen(script);

        menuDefs.RegisterSource("player-classes", PlayerClassRows);
        menuDefs.RegisterSource("mods", BuildModsPage);
        menuDefs.RegisterSource("lan-games", BuildJoinRows);
    }

    // The Mods screen (Ui/ModsMenu.cs, made in InitServices). CP_Mods keeps its name: menudefs
    // name the action
    static ModsMenu modsMenu = null!;

    // Customize controls (Ui/CustomizeControlsScreen.cs); CustomControls keeps its name for menudefs
    static CustomizeControlsScreen customizeScreen = null!;
    internal static int CustomControls(int _) => customizeScreen.Run();
    internal static int CP_Mods(int _) => modsMenu.Run();
    private static CP_itemtype[] BuildModsPage() => modsMenu.BuildModsPage();

    private static CP_itemtype[] PlayerClassRows() => newGameMenu.PlayerClassRows();
}
