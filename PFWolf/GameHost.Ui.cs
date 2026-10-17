using CommandLine;
using PFWolf.Assets.Sounds;
using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.Constants;
using PFWolf.Entities;
using PFWolf.Extensions;
using PFWolf.Fonts;
using PFWolf.Managers;
using SDL2;

namespace PFWolf;

// GameHost's screen wiring: the menus, the automap, the status bar and radar, HUD messages, the console's drawing, debug screens
internal static partial class GameHost
{
    // ==== WL_MENU ================================================================================

    /*
    =============================================================================

                                THE CONTROL PANEL

    The main menu loop and its screens are Ui/ControlPanel.cs, Ui/NewGameMenu.cs,
    Ui/OptionsMenu.cs and Ui/ChangeViewScreen.cs (made in InitServices); the in-play F7 to F10
    are QuickKeys' (Ui/QuickKeys.cs). menudefs name the menu actions by the original's CP_ names
    (RegisterMenuDefs).

    =============================================================================
    */

    static ControlPanel controlPanel = null!;
    static ModMenus? modMenus;      // a mod's own menus, run as submenus (CP_Menu)
    static NewGameMenu newGameMenu = null!;
    static OptionsMenu optionsMenu = null!;
    static ChangeViewScreen changeViewScreen = null!;

    // Control panel song: the main menu's music in menudefs/main-menu
    internal static string MENUSONG => controlPanel.MENUSONG;
    internal static string INTROSONG => controlPanel.INTROSONG;

    internal static ControlPanelItem[] MainMenu { get => controlPanel.MainMenu; set => controlPanel.MainMenu = value; }
    internal static ControlPanelMenuInfo MainItems { get => controlPanel.MainItems; set => controlPanel.MainItems = value; }
    internal static ControlPanelItem[] OptMenu { get => optionsMenu.OptMenu; set => optionsMenu.OptMenu = value; }
    internal static ControlPanelMenuInfo OptItems { get => optionsMenu.OptItems; set => optionsMenu.OptItems = value; }
    internal static ControlPanelItem[] NewEmenu { get => newGameMenu.NewEmenu; set => newGameMenu.NewEmenu = value; }
    internal static ControlPanelMenuInfo NewEitems { get => newGameMenu.NewEitems; set => newGameMenu.NewEitems = value; }
    internal static ControlPanelItem[] NewMenu { get => newGameMenu.NewMenu; set => newGameMenu.NewMenu = value; }
    internal static ControlPanelMenuInfo NewItems { get => newGameMenu.NewItems; set => newGameMenu.NewItems = value; }
    internal static ControlPanelItem[] ClassMenu { get => newGameMenu.ClassMenu; set => newGameMenu.ClassMenu = value; }
    internal static ControlPanelMenuInfo ClassItems { get => newGameMenu.ClassItems; set => newGameMenu.ClassItems = value; }

    internal static int StartGame { get => controlPanel.StartGame; set => controlPanel.StartGame = value; }

    // The menu from play, and F7-F10 (Ui/QuickKeys.cs)
    static QuickKeys quickKeys = null!;     // made in InitServices

    internal static void OpenControlPanel(ScanCodes scancode) => quickKeys.OpenControlPanel(scancode);

    internal static int RunNewGameMenu(int _) => newGameMenu.Run();
    internal static int RunOptionsMenu(int _) => optionsMenu.Run();
    internal static int RunChangeView(int _) => changeViewScreen.Run();
    internal static int RunReadThis(int _) => controlPanel.ReadThis();
    internal static int RunViewScores(int _) => controlPanel.ViewScores();
    internal static int RunEndGame(int _) => controlPanel.EndGame();
    internal static int RunQuit(int _) => controlPanel.Quit();

    static ControlPanelHooks MakeControlPanelHooks() => new(
        SaveGame: RunSaveGame,
        LoadGame: RunLoadGame,
        EndGame: () =>
        {
            playerstate.lives = 0;
            playstate = PlayState.Died;
            LastAttacker = null;
        },
        Random: UiRandom);

    /// <summary>Starts a game from New Game; the control panel then leaves, and "Read This!" goes back to its normal color</summary>
    static void StartNewGame(short difficulty, EpisodeInfo episode, MapInfo? mapInfo, string? playerClass)
    {
        NewGame(difficulty, episode, mapInfo!, playerClass);
        StartGame = 1;
        unlocks.ForgetNews();       // what was unlocked before isn't told on this game's first tally
        FindMenuItem(MainMenu, "readthis")?.active = 1;
    }

    // The jukebox (Ui/Jukebox.cs); its songs are loaded from menudefs/ in CheckForEpisodes
    static Jukebox jukebox = null!;     // made in InitServices
    internal static ControlPanelItem[] MusicMenu { get => jukebox.MusicMenu; set => jukebox.MusicMenu = value; }
    internal const int JukeboxPageSize = Jukebox.JukeboxPageSize;
    internal static ControlPanelMenuInfo MusicItems { get => jukebox.MusicItems; set => jukebox.MusicItems = value; }
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

    internal static ControlPanelItem[] VidMenu { get => videoMenu.Items; set => videoMenu.Items = value; }
    internal static ControlPanelMenuInfo VidItems { get => videoMenu.Info; set => videoMenu.Info = value; }
    internal static ControlPanelItem[] SndMenu { get => soundMenu.Items; set => soundMenu.Items = value; }
    internal static ControlPanelMenuInfo SndItems { get => soundMenu.Info; set => soundMenu.Info = value; }
    internal static ControlPanelItem[] CtlMenu { get => controlMenus.CtlMenu; set => controlMenus.CtlMenu = value; }
    internal static ControlPanelMenuInfo CtlItems { get => controlMenus.CtlItems; set => controlMenus.CtlItems = value; }
    internal static ControlPanelItem[] JoyMenu { get => controlMenus.JoyMenu; set => controlMenus.JoyMenu = value; }
    internal static ControlPanelMenuInfo JoyItems { get => controlMenus.JoyItems; set => controlMenus.JoyItems = value; }

    internal static int RunVideoMenu(int _) => videoMenu.Run();
    internal static int RunSoundMenu(int _) => soundMenu.Run();
    internal static int RunControlMenu(int _) => controlMenus.RunControl();
    internal static int RunControllerSettings(int _) => controlMenus.RunControllerSettings();
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

    private static (ControlPanelItem[] items, ControlPanelMenuInfo info) LoadMenu(string name, short curpos = 0) => menuDefs.LoadMenu(name, curpos);
    internal static ControlPanelItem? FindMenuItem(ControlPanelItem[] items, string id) => MenuDefs.FindMenuItem(items, id);

    /// <summary>What menudefs' items can do, and the menus built from the game's data</summary>
    private static void RegisterMenuDefs()
    {
        // By the names menudefs' ction: uses (the original's CP_ functions): they're a file
        // format, kept whatever the methods are called
        (string Name, Func<int, int> Action)[] actions =
        [
            ("CP_NewGame", RunNewGameMenu),
            ("CP_Multiplayer", RunMultiplayerMenu),
            ("CP_Options", RunOptionsMenu),
            ("CP_Video", RunVideoMenu),
            ("CP_Mods", RunModsMenu),
            ("CP_Sound", RunSoundMenu),
            ("CP_Control", RunControlMenu),
            ("CP_ControllerSettings", RunControllerSettings),
            ("CP_LoadGame", RunLoadGame),
            ("CP_SaveGame", RunSaveGame),
            ("CP_ChangeView", RunChangeView),
            ("CP_ReadThis", RunReadThis),
            ("CP_ViewScores", RunViewScores),
            ("CP_EndGame", RunEndGame),
            ("CP_Quit", RunQuit),
            ("MouseSensitivity", MouseSensitivity),
            ("CustomControls", CustomControls),
        ];
        foreach (var (name, action) in actions)
            menuDefs.RegisterAction(name, action);
        menuDefs.TextScreen = script => _ => ShowTextScreen(script);

        // MENU HOOKS: a mod's items opening a menu of theirs, showing a text, calling a script
        menuDefs.SubMenu = name => (modMenus ??= new ModMenus(_videoManager, menuDrawing, menuLoop, menuInput, menuDefs)).Run(name);
        menuDefs.Article = name =>
        {
            articleReader.ShowText(name);
            StartCPMusic(MENUSONG);
            return 0;
        };
        menuDefs.Script = function =>
        {
            if (gameScripts.Scripts is not { } scripts || scripts.Globals[function].Type != Lua.LuaValueType.Function)
                Console.WriteLine($"CP_Lua: no script function {function}");
            else
                scripts.Call(function, []);
            return 0;
        };

        menuDefs.RegisterSource("player-classes", PlayerClassRows);
        menuDefs.RegisterSource("mods", BuildModsPage);
        menuDefs.RegisterSource("lan-games", BuildJoinRows);
    }

    // The Mods screen (Ui/ModsMenu.cs, made in InitServices); menudefs' CP_Mods action
    static ModsMenu modsMenu = null!;

    // Customize controls (Ui/CustomizeControlsScreen.cs); CustomControls keeps its name for menudefs
    static CustomizeControlsScreen customizeScreen = null!;
    internal static int CustomControls(int _) => customizeScreen.Run();
    internal static int RunModsMenu(int _) => modsMenu.Run();
    private static ControlPanelItem[] BuildModsPage() => modsMenu.BuildModsPage();

    private static ControlPanelItem[] PlayerClassRows() => newGameMenu.PlayerClassRows();

    // ==== Automap ================================================================================

    // Drawing the automap (Ui/AutomapDrawing.cs)
    static AutomapDrawing automapDrawing = null!;       // made in InitServices

    static AutomapDrawing MakeAutomapDrawing() =>
        new(_assetManager, _videoManager, _graphicManager, _fontManager, _automapManager, _mapManager, _factionManager,
            _translationManager, doors, pushWalls, spriteDrawing, session, new AutomapHooks(
                () => (viewscreenx, viewscreeny, viewwidth, viewheight), GetMapDisplayName, FormatPlayTime,
                () => objectives.StatsLines()));

    internal static void DrawAutomap() => automapDrawing.DrawAutomap();

    // The automap's keys and console commands (Ui/AutomapInput.cs)
    static AutomapInput automapInput = null!;       // made in InitServices

    static AutomapInput MakeAutomapInput() => new(_inputManager, _consoleManager, _automapManager, _mapManager, controls, controlSettings,
        inputPolling, session);

    internal static void ToggleAutomap() => automapInput.ToggleAutomap();
    internal static void UpdateAutomap() => automapInput.UpdateAutomap();
    internal static bool HandleAutomapKey(InputCode code) => automapInput.HandleAutomapKey(code);
    internal static void RouteMovementToAutomap() => automapInput.RouteMovementToAutomap();

    private static void Cmd_AmStyle(string[] args) => automapInput.Cmd_AmStyle(args);
    private static void Cmd_AmOverlay(string[] args) => automapInput.Cmd_AmOverlay(args);
    private static void Cmd_AmGrid(string[] args) => automapInput.Cmd_AmGrid(args);
    private static void Cmd_AmStats(string[] args) => automapInput.Cmd_AmStats(args);
    private static void Cmd_AmReveal(string[] args) => automapInput.Cmd_AmReveal(args);

    // ==== StatusBarParts =========================================================================

    /*
    =============================================================================

                                    STATUS BAR

    The layout (statusbar.yaml) and its parts drawn from the player's state are in
    Ui/StatusBarDrawing.cs, and the weapon's ammo and charge light in Gameplay/PlayerWeapons.cs.

    =============================================================================
    */

    static StatusBarDrawing statusBarDrawing = null!;     // made in InitServices

    static StatusBarDrawing MakeStatusBarDrawing() =>
        new(_gameEngineManager, _assetManager, _videoManager, _graphicManager, _inventoryManager, _audioManager,
            _hudMessageManager, ui, session, settings, new StatusBarHooks(GetMapDisplayName, WrapText));

    static void DrawKeys() => statusBarDrawing.DrawKeys();
    static void DrawScore() => statusBarDrawing.DrawScore();

    // ==== Radar ==================================================================================

    // The status bar's radar (Ui/Radar.cs)
    static Radar radar = null!;     // made in InitServices

    static Radar MakeRadar() =>
        new(_mapManager, _inventoryManager, _inputManager, _hudMessageManager, _videoManager, _graphicManager,
            statusBarDrawing, doors, session, settings);

    internal static void UpdateRadar() => radar.UpdateRadar();
    static string RadarTileColor(int x, int y, string unmapped, string floor, string door, string locked) =>
        radar.RadarTileColor(x, y, unmapped, floor, door, locked);

    // ==== HudMessages ============================================================================

    // The HUD messages drawn over the view, and the obituary (Ui/HudMessageDrawing.cs)
    static HudMessageDrawing hudMessageDrawing = null!;     // made in InitServices

    static HudMessageDrawing MakeHudMessageDrawing() => new(_videoManager, _graphicManager, _inventoryManager, _hudMessageManager, ui,
        statusBarDrawing, playersManager, render, session);

    internal static void DrawHudMessages() => hudMessageDrawing.DrawHudMessages();
    internal static void ShowObituary() => hudMessageDrawing.ShowObituary();
    static List<string> WrapText(string text, string font, int width) => hudMessageDrawing.WrapText(text, font, width);

    // ==== ConsoleDraw ============================================================================

    // The drop-down console drawn over the screen (Ui/ConsoleDrawing.cs)
    static ConsoleDrawing consoleDrawing = null!;       // made in InitServices

    internal static void RestoreBorderAfterConsole() => consoleDrawing.RestoreBorderAfterConsole();
    internal static void DrawConsole() => consoleDrawing.DrawConsole();

    // ==== WL_DEBUG ===============================================================================
    // The old Tab+key debug keys are now console commands (GameHost.ConsoleCommands.cs); the screens
    // they show and save are Ui/DebugScreens.cs.

    static DebugScreens debugScreens = null!;       // made in InitServices
    static DebugCommands debugCommands = null!;     // (Commands/DebugCommands.cs)

    // Set by the "screenshot" command; ThreeDRefresh takes the shot before drawing the console.
    static bool screenshotPending { get => debugScreens.screenshotPending; set => debugScreens.screenshotPending = value; }

    internal static string TakeScreenshot() => debugScreens.TakeScreenshot();
}
