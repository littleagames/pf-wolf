namespace PFWolf.Menus;

/// <summary>
/// The menudefs names the game's code asks for, and the actions and items-sources menudefs
/// items can name: shared by the game and the editor's menu window ("Used by", problems,
/// pickers), so the two can't drift
/// </summary>
public static class MenuNames
{
    /// <summary>The menus the engine opens itself, and what it opens each for</summary>
    public static IReadOnlyDictionary<string, string> EngineUses { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["main-menu"] = "The engine: the main menu (Esc)",
        ["options"] = "The engine: Options (CP_Options)",
        ["sound"] = "The engine: Sound (CP_Sound)",
        ["video"] = "The engine: Video (CP_Video)",
        ["control"] = "The engine: Control (CP_Control)",
        ["controller"] = "The engine: Controller settings (CP_ControllerSettings)",
        ["mouse-sensitivity"] = "The engine: Mouse sensitivity (MouseSensitivity)",
        ["customize"] = "The engine: Customize controls (CustomControls)",
        ["game-options"] = "The engine: Game options (CP_GameOptions)",
        ["load-game"] = "The engine: Load game (CP_LoadGame)",
        ["save-game"] = "The engine: Save game (CP_SaveGame)",
        ["jukebox"] = "The engine: the jukebox (a cheat key)",
        ["mods"] = "The engine: Mods (CP_Mods)",
        ["multiplayer"] = "The engine: Multiplayer (CP_Multiplayer)",
        ["join-game"] = "The engine: Multiplayer's Join",
        ["lobby"] = "The engine: Multiplayer's lobby",
        ["net-rules"] = "The engine: Multiplayer's rules",
        ["new-game"] = "The engine: New Game's skill menu (CP_NewGame)",
        ["new-episode"] = "The engine: New Game's episode menu",
        ["new-level"] = "The engine: New Game's level menu (an episode's level-select)",
        ["new-class"] = "The engine: New Game's class menu (with more than one player class)",
    };

    /// <summary>The game's own actions a MenuSwitcher can run (the original's CP_ routines), and what each does</summary>
    public static IReadOnlyDictionary<string, string> Actions { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["CP_NewGame"] = "Starts a new game: the episode, skill and class menus",
        ["CP_Multiplayer"] = "The Multiplayer menu",
        ["CP_Options"] = "The Options menu",
        ["CP_Video"] = "The Video menu",
        ["CP_GameOptions"] = "The Game options menu",
        ["CP_Mods"] = "The Mods screen",
        ["CP_Sound"] = "The Sound menu",
        ["CP_Control"] = "The Control menu",
        ["CP_ControllerSettings"] = "The Controller settings menu",
        ["CP_LoadGame"] = "The Load game screen",
        ["CP_SaveGame"] = "The Save game screen",
        ["CP_ChangeView"] = "The Change view screen",
        ["CP_ReadThis"] = "Wolf3D's help screens (Read This!)",
        ["CP_ViewScores"] = "The high scores",
        ["CP_EndGame"] = "Ends the game being played",
        ["CP_Quit"] = "Quits, asking first",
        ["MouseSensitivity"] = "The Mouse sensitivity menu",
        ["CustomControls"] = "The Customize controls screen",
    };

    /// <summary>The menus each of the game's actions opens (a screen of the game's own reached from them, too)</summary>
    public static IReadOnlyDictionary<string, string[]> ActionMenus { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["CP_NewGame"] = ["new-game", "new-episode", "new-level", "new-class"],
        ["CP_Multiplayer"] = ["multiplayer", "join-game", "lobby", "net-rules"],
        ["CP_Options"] = ["options"],
        ["CP_Video"] = ["video"],
        ["CP_GameOptions"] = ["game-options"],
        ["CP_Mods"] = ["mods"],
        ["CP_Sound"] = ["sound"],
        ["CP_Control"] = ["control"],
        ["CP_ControllerSettings"] = ["controller"],
        ["MouseSensitivity"] = ["mouse-sensitivity"],
        ["CustomControls"] = ["customize"],
        ["CP_LoadGame"] = ["load-game"],
        ["CP_SaveGame"] = ["save-game"],
    };

    /// <summary>The menus the engine opens with no menu item to get there: the main menu (Esc) and the jukebox (a cheat key)</summary>
    public static IReadOnlyList<string> RootMenus { get; } = ["main-menu", "jukebox"];

    /// <summary>Actions that open something an item names: a menu, a text, a script function, a presenter text</summary>
    public const string SubMenuAction = "CP_Menu", ArticleAction = "CP_Article", ScriptAction = "CP_Lua", TextScreenAction = "CP_TextScreen";

    /// <summary>Every action an item can name: the game's own and the ones that open what the item names</summary>
    public static IEnumerable<string> AllActions => Actions.Keys.Concat([SubMenuAction, ArticleAction, ScriptAction, TextScreenAction]);

    /// <summary>The items-sources a menu can build its rows from, and from what</summary>
    public static IReadOnlyDictionary<string, string> ItemsSources { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["episodes"] = "game-info's episodes",
        ["skills"] = "game-info's skills",
        ["player-classes"] = "the player classes (player.yaml)",
        ["mods"] = "the mods in the mods folder",
        ["lan-games"] = "the games found on the network",
    };
}
