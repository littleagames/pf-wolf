using System.Text;
using PFWolf.Extensions;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                    SAVE GAMES

    The file around a save (header, thumbnail, checksum) is SaveGames/SaveFile.cs's. Its body
    holds the game (gamestate, level ratios, inventory), the level as it stands
    (WriteLevelBody), the weapon, and the hub clusters' levels kept as they were left
    (Gameplay/Hubs.cs). Saving and loading are SaveGames/GameSaves.cs; the level's part,
    SaveGames/LevelBodies.cs.

    =============================================================================
    */


    // Saving and loading the game, the autosave and the quick keys (SaveGames/GameSaves.cs)
    static GameSaves gameSaves = null!;     // made in InitServices

    static GameSaves MakeGameSaves() => new(_gameEngineManager, _assetManager, _videoManager, _inputManager, _graphicManager,
        _inventoryManager, _mapManager, menuDrawing, playScreen, levelBodies, levelSetup, levelMusic, playersManager, weaponInHand,
        playerClassRules, playerStats, hubs, session,
        new GameSavesHooks(on => loadedgame = on, () => viewsize, () => lastgamemusicoffset, saving => loadSaveScreen.Run(saving),
            () =>
            {
                StartGame = 1;
                //
                // CHANGE "READ THIS!" TO NORMAL COLOR
                //
                FindMenuItem(MainMenu, "readthis")?.active = 1;
            }));

    internal static string QuickSavePath => gameSaves.QuickSavePath;
    internal static bool autosaveEnabled { get => gameSaves.autosaveEnabled; set => gameSaves.autosaveEnabled = value; }
    static bool autosavePending { get => gameSaves.autosavePending; set => gameSaves.autosavePending = value; }

    internal static void AutoSaveGame() => gameSaves.AutoSaveGame();
    internal static string NewSaveGamePath() => gameSaves.NewSaveGamePath();
    internal static string FormatPlayTime(int tics) => SaveFile.FormatPlayTime(tics);
    internal static List<SaveInfo> ListSaveGames() => gameSaves.ListSaveGames();
    internal static List<SavedMod> CurrentSavedMods() => gameSaves.CurrentSavedMods();
    internal static bool SaveTheGame(string path, string name, int x, int y) => gameSaves.SaveTheGame(path, name, x, y);
    internal static bool LoadTheGame(string path, int x, int y) => gameSaves.LoadTheGame(path, x, y);

    // The menu's load and save actions: menudefs name them, so they keep their names
    internal static int CP_LoadGame(int quick) => gameSaves.CP_LoadGame(quick);
    internal static int CP_SaveGame(int quick) => gameSaves.CP_SaveGame(quick);

    // The load and save screens (Ui/LoadSaveScreen.cs, made in InitServices)
    static LoadSaveScreen loadSaveScreen = null!;
    private static LoadSaveHooks MakeLoadSaveHooks() => gameSaves.MakeLoadSaveHooks();

    // The level as it stands, as saves, hub levels and joining players hold it (SaveGames/LevelBodies.cs)
    static LevelBodies levelBodies = null!;     // made in InitServices

    static void WriteLevelBody(BinaryWriter bw) => levelBodies.WriteLevelBody(bw);
    static LevelBody ReadLevelBody(BinaryReader br) => levelBodies.ReadLevelBody(br);
    static List<Entities.Actors.Actor> ApplyLevelBody(LevelBody body, Func<int, Entities.PlayerState?>? ownerOf = null) =>
        levelBodies.ApplyLevelBody(body, ownerOf);

    private static string WrapForMessage(string text) => MenuDrawing.WrapForMessage(text);
}