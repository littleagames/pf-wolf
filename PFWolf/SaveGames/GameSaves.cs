using System.Text;
using PFWolf.Extensions;

namespace PFWolf.SaveGames;

/// <summary>What saving and loading the game needs from the game</summary>
/// <param name="SetLoadedGame">A saved game is being loaded (the level rebuild doesn't count anything again)</param>
/// <param name="ViewSize">The view's size setting (21: the full screen, nothing to redraw around it)</param>
/// <param name="LastMusicOffset">Where the menu left the level's song</param>
/// <param name="RunLoadSaveScreen">The load or save screen (Ui/LoadSaveScreen.cs), saving or not; its menu result</param>
/// <param name="MenuLoaded">A game was loaded from the menu: start it, and "Read This!" back to its normal color</param>
internal sealed record GameSavesHooks(
    Action<bool> SetLoadedGame,
    Func<int> ViewSize,
    Func<int> LastMusicOffset,
    Func<bool, int> RunLoadSaveScreen,
    Action MenuLoaded);

/// <summary>
/// Saving and loading the game in progress: the save's header, thumbnail and body (the game,
/// level ratios, inventory, the level as it stands, the weapon and the hub levels kept), the
/// autosave a new level makes, the quicksave and quickload keys, and the menu's load and save
/// actions. Nothing is restored until the whole file has been read and checked against the
/// current map and actordefs, so a save that can't be loaded leaves the game as it was.
/// </summary>
internal sealed class GameSaves(
    GameEngineManager engine,
    AssetManager assets,
    VideoManager video,
    InputManager input,
    GraphicManager graphics,
    InventoryManager inventoryManager,
    MapManager mapManager,
    MenuDrawing draw,
    PlayScreen playScreen,
    LevelBodies levelBodies,
    LevelSetup levelSetup,
    LevelMusic music,
    Players playersManager,
    WeaponInHand weaponInHand,
    PlayerClassRules classes,
    PlayerStats stats,
    Hubs hubs,
    GameSession session,
    GameSavesHooks hooks)
{
    // Thumbnails are taken this wide (less if the view is narrower), their height from the
    // view's shape; big enough to stay sharp in the menus at a few times 320x200
    private const int ThumbnailWidth = 256;

    internal string SaveDirectory => engine.ConfigDirectories.SaveGameDirectory;

    // F8 saves over this one and F9 loads it; the level start autosave is the other
    internal string QuickSavePath => Path.Combine(SaveDirectory, SaveFile.QuickSaveFile);
    internal string AutoSavePath => Path.Combine(SaveDirectory, SaveFile.AutoSaveFile);

    // Whether a new level saves itself as it starts (the `autosave` setting, saved in the config)
    internal bool autosaveEnabled = true;

    // Set as a new level begins; PlayLoop saves once the first frame is drawn, so the
    // autosave has a picture of it
    internal bool autosavePending;

    /// <summary>Saves over the autosave, if it's on. Quietly: a failure is only logged.</summary>
    internal void AutoSaveGame()
    {
        autosavePending = false;
        if (autosaveEnabled)
            SaveTheGame(AutoSavePath, "$STR_LS_AUTO".ToLanguageText(assets.GetText("en-us")), 0, 0);
    }

    /// <summary>A path for a new save in the save folder, named for when it was made.</summary>
    internal string NewSaveGamePath() => SaveFile.NewPath(SaveDirectory, DateTime.Now);

    internal string FormatPlayTime(int tics) => SaveFile.FormatPlayTime(tics);

    /// <summary>Every save in the save folder that this build can read, newest first.</summary>
    internal List<SaveInfo> ListSaveGames() => SaveFile.List(SaveDirectory);

    internal SaveInfo? ReadSaveInfo(string path) => SaveFile.ReadInfo(path);
    internal SaveThumbnail? ReadSaveThumbnail(SaveInfo save) => SaveFile.ReadThumbnail(save);
    internal byte[] ReadExactly(BinaryReader br, int count) => SaveFile.ReadExactly(br, count);

    /// <summary>The header for a save of the game as it stands.</summary>
    internal SaveInfo CurrentSaveInfo(string path, string name)
    {
        var language = assets.GetText("en-us");
        var skill = engine.GetGameInfo().Skills.Values.ElementAtOrDefault(session.gamestate.difficulty);

        return new SaveInfo
        {
            Path = path,
            Name = name,
            SavedAt = DateTime.UtcNow,
            GamePack = engine.GamePackId,
            MapOn = session.gamestate.mapon,
            MapName = playScreen.GetMapDisplayName(session.gamestate.mapon),
            Difficulty = session.gamestate.difficulty,
            SkillName = skill?.Name.ToLanguageText(language) ?? "",
            LevelTime = session.gamestate.TimeCount,
            PlayTime = session.gamestate.PlayTime,
            Score = session.playerstate.score,
            Lives = session.playerstate.lives,
            Health = session.playerstate.health,
            Kills = session.gamestate.killcount,
            KillTotal = session.gamestate.killtotal,
            Secrets = session.gamestate.secretcount,
            SecretTotal = session.gamestate.secrettotal,
            Treasure = session.gamestate.treasurecount,
            TreasureTotal = session.gamestate.treasuretotal,
            Mods = CurrentSavedMods(),
        };
    }

    /// <summary>The mods loaded now, as a save records them</summary>
    internal List<SavedMod> CurrentSavedMods()
        => assets.LoadedMods
            .Select(mod => new SavedMod(mod.DisplayName, mod.Info.Version ?? "", Path.GetFileName(mod.FullPath)))
            .ToList();

    /// <summary>Whether a save was made with other mods than are loaded now</summary>
    internal bool HasOtherMods(SaveInfo save) => !SavedMod.SameMods(save.Mods, CurrentSavedMods());

    /// <summary>
    /// Says a save was made with other mods than are loaded now, and waits for a key: it still
    /// loads, but may not play the same
    /// </summary>
    internal void WarnOtherMods(SaveInfo save)
    {
        var language = assets.GetText("en-us");
        string L(string key) => key.ToLanguageText(language);
        string ModList(List<SavedMod> mods) => mods.Count switch
        {
            0 => L("$STR_LS_NOMODS"),
            <= 3 => string.Join(", ", mods),
            _ => string.Format(L("$STR_LS_MOREMODS"), string.Join(", ", mods.Take(2)), mods.Count - 2),
        };

        draw.Message($"{L("$STR_LS_OTHERMODS")}\n\n"
            + $"{MenuDrawing.WrapForMessage($"{L("$STR_LS_SAVEDWITH")} {ModList(save.Mods)}")}\n"
            + MenuDrawing.WrapForMessage($"{L("$STR_LS_LOADEDNOW")} {ModList(CurrentSavedMods())}"));
        input.ClearKeysDown();
        input.Ack();
    }

    /// <summary>
    /// Saves the game in progress. Written to a temporary file first, so a failed save
    /// never destroys the one it was replacing. Returns false if it couldn't be written.
    /// </summary>
    internal bool SaveTheGame(string path, string name, int x, int y)
    {
        // A save holds one player (gametype's stats, one inventory, one pawn)
        if (session.players.Count > 1)
        {
            Console.WriteLine($"Couldn't save the game to {path}: games with more than one player can't be saved yet.");
            return false;
        }

        try
        {
            using var body = new MemoryStream();
            using (var bw = new BinaryWriter(body, Encoding.UTF8, leaveOpen: true))
                WriteSaveBody(bw, x, y);

            SaveFile.Write(path, CurrentSaveInfo(path, name), video.MakeThumbnail(ThumbnailWidth), body.ToArray());
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't save the game to {path}: {e.Message}");
            return false;
        }
    }

    internal void WriteSaveBody(BinaryWriter bw, int x, int y)
    {
        DiskFlopAnim(x, y);
        session.gamestate.Write(bw, session.playerstate);

        bw.Write(session.LevelRatios.Count);
        foreach (var (map, ratios) in session.LevelRatios)
        {
            bw.Write(map);
            ratios.Write(bw);
        }

        bw.Write(inventoryManager.Items.Count);
        foreach (var (item, count) in inventoryManager.Items)
        {
            bw.Write(item);
            bw.Write(count);
        }

        DiskFlopAnim(x, y);
        levelBodies.WriteLevelBody(bw);

        var weapon = weaponInHand.SyncWeaponSprite();
        bw.Write(weapon != null);
        if (weapon != null)
            Entities.Actors.ActorSnapshot.Capture(weapon).Write(bw);

        bw.Write(session.playerstate.WeaponCharge);

        DiskFlopAnim(x, y);
        hubs.WriteHubState(bw);
    }

    /// <summary>Everything a save's body holds, read without changing any game state.</summary>
    internal sealed record SaveGameData(
        gametype GameState,
        Entities.PlayerState Player,
        Dictionary<string, LRstruct> LevelRatios,
        Dictionary<string, int> Inventory,
        LevelBody Body,
        Entities.Actors.ActorSnapshot? Weapon,
        int WeaponCharge,
        HubState Hubs);

    internal SaveGameData ReadSaveBody(BinaryReader br)
    {
        var (state, player) = gametype.Read(br);

        var ratios = new Dictionary<string, LRstruct>();
        for (int i = br.ReadCount(); i > 0; i--)
            ratios[br.ReadString()] = LRstruct.Read(br);

        var inventory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = br.ReadCount(); i > 0; i--)
            inventory[br.ReadString()] = br.ReadInt32();

        return new SaveGameData(
            state,
            player,
            ratios,
            inventory,
            levelBodies.ReadLevelBody(br),
            Weapon: br.ReadBoolean() ? Entities.Actors.ActorSnapshot.Read(br) : null,
            WeaponCharge: br.ReadInt32(),
            Hubs: Hubs.ReadHubState(br));
    }

    /// <summary>
    /// Loads a save over whatever is running. The caller sets <c>loadedgame</c> first, so the
    /// level rebuild doesn't re-count kills, treasure and secrets. Returns false, having shown
    /// why and changed nothing, if the save can't be loaded.
    /// </summary>
    internal bool LoadTheGame(string path, int x, int y)
    {
        SaveGameData data;
        SaveInfo? info = null;
        bool checksumOk;
        try
        {
            DiskFlopAnim(x, y);
            byte[] body;
            (body, checksumOk) = SaveFile.ReadBody(path, out info);

            DiskFlopAnim(x, y);
            using var bodyReader = new BinaryReader(new MemoryStream(body));
            data = ReadSaveBody(bodyReader);

            if (!engine.GetGameInfo().Maps.ContainsKey(data.GameState.mapon))
                throw new InvalidDataException($"Map \"{data.GameState.mapon}\" isn't in this game.");

            if (classes.FindPlayerClass(data.Player.playerclass) is not { } playerClass)
                throw new InvalidDataException($"Player class \"{data.Player.playerclass}\" isn't in this game.");
            data.Player.playerclass = playerClass;

            foreach (var weapon in new[] { data.Player.weapon, data.Player.chosenweapon })
            {
                if (weapon != null && inventoryManager.FindClass(weapon, "Weapon") == null)
                    throw new InvalidDataException($"Weapon \"{weapon}\" isn't in this game.");
            }

            var problem = mapManager.CheckLevelState(data.Body.Level, data.GameState.mapon);
            if (problem != null)
                throw new InvalidDataException(problem);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            // EndOfStreamException is an IOException, so a truncated file lands here too, and a
            // garbled string length is a FormatException.
            Console.WriteLine($"Couldn't load {path}: {e.Message}");
            // Other mods are the likely reason a map, weapon or actor is missing
            var modsNote = info != null && HasOtherMods(info)
                ? $"\n\n{"$STR_LS_SAVEDOTHERMODS".ToLanguageText(assets.GetText("en-us"))}"
                : "";
            draw.Message($"This saved game can't\nbe loaded.\n\n{MenuDrawing.WrapForMessage(e.Message)}{modsNote}");
            input.ClearKeysDown();
            input.Ack();
            return false;
        }

        // Read above, or the load would have stopped
        if (HasOtherMods(info!))
            WarnOtherMods(info!);

        DiskFlopAnim(x, y);
        session.gamestate = data.GameState;
        playersManager.ResetPlayers();         // a save is a single player's game
        session.playerstate.CopyStatsFrom(data.Player);
        session.LevelRatios = data.LevelRatios;
        inventoryManager.Restore(data.Inventory);

        // Rebuild the level from the map (doors with their locks, statics, the player), then
        // lay the saved state over it.
        levelSetup.SetupGameLevel();

        DiskFlopAnim(x, y);
        levelBodies.ApplyLevelBody(data.Body);
        hubs.RestoreHubState(data.Hubs);

        // The weapon picks up where it was (mid-attack, say).
        session.playerstate.WeaponSprite = null;
        if (weaponInHand.SyncWeaponSprite() is { } sprite && data.Weapon != null
            && string.Equals(data.Weapon.ClassName, sprite.Name, StringComparison.OrdinalIgnoreCase))
        {
            data.Weapon.ApplyTo(sprite);
        }
        session.playerstate.WeaponCharge = data.WeaponCharge;

        if (!checksumOk)
        {
            var language = assets.GetText("en-us");
            draw.Message($"{"$STR_SAVECHT1".ToLanguageText(language)}\n{"$STR_SAVECHT2".ToLanguageText(language)}\n{"$STR_SAVECHT3".ToLanguageText(language)}\n{"$STR_SAVECHT4".ToLanguageText(language)}");

            input.ClearKeysDown();
            input.Ack();

            session.playerstate.oldscore = session.playerstate.score = 0;
            session.playerstate.lives = 1;
            stats.GiveStartingInventory();
        }

        return true;
    }

    /*
    =============================================================================

                            LOAD AND SAVE FROM THE MENU

    The load and save screens are Ui/LoadSaveScreen.cs (made in InitServices); quickload
    and quicksave (F9, F8) are here. CP_LoadGame and CP_SaveGame keep their names: menudefs
    name the actions.

    =============================================================================
    */



    internal int CP_LoadGame(int quick)
    {
        //
        // QUICKLOAD: THE QUICKSAVE, IN PLACE, SO PLAY CARRIES STRAIGHT ON IN THE RESTORED LEVEL
        //
        if (quick != 0)
        {
            hooks.SetLoadedGame(true);
            var loaded = LoadTheGame(QuickSavePath, 0, 0);
            hooks.SetLoadedGame(false);
            if (!loaded)
                return 0;

            if (hooks.ViewSize() != 21)
                playScreen.DrawPlayScreen();
            music.ContinueMusic(hooks.LastMusicOffset());
            return 1;
        }

        return hooks.RunLoadSaveScreen(false);
    }

    internal int CP_SaveGame(int quick)
    {
        //
        // QUICKSAVE: OVER THE QUICKSAVE, WITHOUT ASKING
        //
        if (quick != 0)
        {
            if (!SaveTheGame(QuickSavePath, "$STR_LS_QUICK".ToLanguageText(assets.GetText("en-us")), 0, 0))
                ShowSaveFailed();
            return 1;
        }

        return hooks.RunLoadSaveScreen(true);
    }

    /// <summary>What the load/save screen needs from the game</summary>
    internal LoadSaveHooks MakeLoadSaveHooks() => new(
        CurrentSave: () => CurrentSaveInfo("", ""),
        CurrentThumbnail: () => video.MakeThumbnail(ThumbnailWidth),
        ListSaves: ListSaveGames,
        NewSavePath: NewSaveGamePath,
        NewSaveName: () => playScreen.GetMapDisplayName(session.gamestate.mapon),
        HasOtherMods: HasOtherMods,
        Load: (path, x, y) =>
        {
            hooks.SetLoadedGame(true);
            if (LoadTheGame(path, x, y))
                return true;
            hooks.SetLoadedGame(false);
            return false;
        },
        Save: SaveTheGame,
        Loaded: hooks.MenuLoaded,
        SaveFailed: ShowSaveFailed);
    internal void ShowSaveFailed()
    {
        draw.Message("The game couldn't\nbe saved.");
        input.ClearKeysDown();
        input.Ack();
    }



    internal byte diskflopanim_which = 0;
    internal void DiskFlopAnim(int x, int y)
    {
        if (x == 0 && y == 0)
            return;

        string[] diskpics = new[] { "c_diskloading1", "c_diskloading2" };

        graphics.DrawPic(diskpics[diskflopanim_which], x, y);
        video.Update();
        diskflopanim_which ^= 1;
    }

}
