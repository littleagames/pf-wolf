using System.Text;
using PFWolf.Configuration;
using PFWolf.Extensions;

namespace PFWolf.SaveGames;

/// <summary>What saving and loading the game needs from the game</summary>
/// <param name="RunLoadSaveScreen">The load or save screen (Ui/LoadSaveScreen.cs), saving or not; its menu result</param>
/// <param name="MenuLoaded">A game was loaded from the menu: start it, and "Read This!" back to its normal color</param>
internal sealed record GameSavesHooks(
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
    GameSettings settings,
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
    internal bool AutosaveEnabled { get => settings.AutosaveEnabled; set => settings.AutosaveEnabled = value; }

    // Set as a new level begins; PlayLoop saves once the first frame is drawn, so the
    // autosave has a picture of it
    internal bool AutosavePending;

    /// <summary>Saves over the autosave, if it's on. Quietly: a failure is only logged.</summary>
    internal void AutoSaveGame()
    {
        AutosavePending = false;
        if (AutosaveEnabled)
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
        var skill = engine.GetGameInfo().Skills.Values.ElementAtOrDefault(session.GameState.Difficulty);

        return new SaveInfo
        {
            Path = path,
            Name = name,
            SavedAt = DateTime.UtcNow,
            GamePack = engine.GamePackId,
            MapOn = session.GameState.MapOn,
            MapName = playScreen.GetMapDisplayName(session.GameState.MapOn),
            Difficulty = session.GameState.Difficulty,
            SkillName = skill?.Name.ToLanguageText(language) ?? "",
            LevelTime = session.GameState.TimeCount,
            PlayTime = session.GameState.PlayTime,
            Score = session.PlayerState.Score,
            Lives = session.PlayerState.Lives,
            Health = session.PlayerState.Health,
            Kills = session.GameState.KillCount,
            KillTotal = session.GameState.KillTotal,
            Secrets = session.GameState.SecretCount,
            SecretTotal = session.GameState.SecretTotal,
            Treasure = session.GameState.TreasureCount,
            TreasureTotal = session.GameState.TreasureTotal,
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
        // A save holds one player (GameState's stats, one inventory, one pawn)
        if (session.Players.Count > 1)
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
        session.GameState.Write(bw, session.PlayerState);

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

        bw.Write(session.PlayerState.WeaponCharge);

        DiskFlopAnim(x, y);
        hubs.WriteHubState(bw);

        // The gun they're manning, what's latched on, what they ride, going unseen, their kill
        // chain and the timed switches still counting down
        PlayerSaveState.Capture(session.PlayerState, mapManager.GetSavedActors(), TimedSwitches?.Pending ?? []).Write(bw);
    }

    /// <summary>The timed switches waiting to turn back (Gameplay/TimedSwitches.cs), kept in a save</summary>
    internal Gameplay.TimedSwitches? TimedSwitches { get; init; }

    /// <summary>Everything a save's body holds, read without changing any game state.</summary>
    internal sealed record SaveGameData(
        GameState GameState,
        Entities.PlayerState Player,
        Dictionary<string, LevelRatio> LevelRatios,
        Dictionary<string, int> Inventory,
        LevelBody Body,
        Entities.Actors.ActorSnapshot? Weapon,
        int WeaponCharge,
        HubState Hubs,
        PlayerSaveState Extras);

    internal SaveGameData ReadSaveBody(BinaryReader br)
    {
        var (state, player) = GameState.Read(br);

        var ratios = new Dictionary<string, LevelRatio>();
        for (int i = br.ReadCount(); i > 0; i--)
            ratios[br.ReadString()] = LevelRatio.Read(br);

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
            Hubs: Hubs.ReadHubState(br),
            Extras: PlayerSaveState.Read(br));
    }

    /// <summary>
    /// Loads a save over whatever is running. The caller sets <c>LoadedGame</c> first, so the
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

            if (!engine.GetGameInfo().Maps.ContainsKey(data.GameState.MapOn))
                throw new InvalidDataException($"Map \"{data.GameState.MapOn}\" isn't in this game.");

            if (classes.FindPlayerClass(data.Player.PlayerClass) is not { } playerClass)
                throw new InvalidDataException($"Player class \"{data.Player.PlayerClass}\" isn't in this game.");
            data.Player.PlayerClass = playerClass;

            foreach (var weapon in new[] { data.Player.Weapon, data.Player.ChosenWeapon })
            {
                if (weapon != null && inventoryManager.FindClass(weapon, "Weapon") == null)
                    throw new InvalidDataException($"Weapon \"{weapon}\" isn't in this game.");
            }

            var problem = mapManager.CheckLevelState(data.Body.Level, data.GameState.MapOn);
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
        session.GameState = data.GameState;
        playersManager.ResetPlayers();         // a save is a single player's game
        session.PlayerState.CopyStatsFrom(data.Player);
        session.LevelRatios = data.LevelRatios;
        inventoryManager.Restore(data.Inventory);

        // Rebuild the level from the map (doors with their locks, statics, the player), then
        // lay the saved state over it.
        levelSetup.SetupGameLevel();

        DiskFlopAnim(x, y);
        var actors = levelBodies.ApplyLevelBody(data.Body);
        hubs.RestoreHubState(data.Hubs);
        data.Extras.ApplyTo(session.PlayerState, actors);
        TimedSwitches?.Restore(data.Extras.TimedSwitches);

        // The weapon picks up where it was (mid-attack, say).
        session.PlayerState.WeaponSprite = null;
        if (weaponInHand.SyncWeaponSprite() is { } sprite && data.Weapon != null
            && string.Equals(data.Weapon.ClassName, sprite.Name, StringComparison.OrdinalIgnoreCase))
        {
            data.Weapon.ApplyTo(sprite);
        }
        session.PlayerState.WeaponCharge = data.WeaponCharge;

        if (!checksumOk)
        {
            var language = assets.GetText("en-us");
            draw.Message($"{"$STR_SAVECHT1".ToLanguageText(language)}\n{"$STR_SAVECHT2".ToLanguageText(language)}\n{"$STR_SAVECHT3".ToLanguageText(language)}\n{"$STR_SAVECHT4".ToLanguageText(language)}");

            input.ClearKeysDown();
            input.Ack();

            session.PlayerState.OldScore = session.PlayerState.Score = 0;
            session.PlayerState.Lives = 1;
            stats.GiveStartingInventory();
        }

        return true;
    }

    /*
    =============================================================================

                            LOAD AND SAVE FROM THE MENU

    The load and save screens are Ui/LoadSaveScreen.cs (made in InitServices); quickload
    and quicksave (F9, F8) are here, and RunLoadGame and RunSaveGame, menudefs' CP_LoadGame and
    CP_SaveGame actions.

    =============================================================================
    */



    internal int RunLoadGame(int quick)
    {
        //
        // QUICKLOAD: THE QUICKSAVE, IN PLACE, SO PLAY CARRIES STRAIGHT ON IN THE RESTORED LEVEL
        //
        if (quick != 0)
        {
            session.LoadedGame = true;
            var loaded = LoadTheGame(QuickSavePath, 0, 0);
            session.LoadedGame = false;
            if (!loaded)
                return 0;

            if (settings.ViewSize != 21)
                playScreen.DrawPlayScreen();
            music.ContinueMusic(session.LastGameMusicOffset);
            return 1;
        }

        return hooks.RunLoadSaveScreen(false);
    }

    internal int RunSaveGame(int quick)
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
        NewSaveName: () => playScreen.GetMapDisplayName(session.GameState.MapOn),
        HasOtherMods: HasOtherMods,
        Load: (path, x, y) =>
        {
            session.LoadedGame = true;
            if (LoadTheGame(path, x, y))
                return true;
            session.LoadedGame = false;
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



    internal byte DiskFlopAnimWhich = 0;
    internal void DiskFlopAnim(int x, int y)
    {
        if (x == 0 && y == 0)
            return;

        string[] diskpics = new[] { "c_diskloading1", "c_diskloading2" };

        graphics.DrawPic(diskpics[DiskFlopAnimWhich], x, y);
        video.Update();
        DiskFlopAnimWhich ^= 1;
    }

}
