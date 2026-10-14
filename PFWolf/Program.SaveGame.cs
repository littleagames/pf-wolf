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
    (Program.Hubs.cs). Nothing is restored until the whole file has been read and checked
    against the current map and actordefs, so a save that can't be loaded leaves the game as
    it was.

    =============================================================================
    */

    // Thumbnails are taken this wide (less if the view is narrower), their height from the
    // view's shape; big enough to stay sharp in the menus at a few times 320x200
    private const int ThumbnailWidth = 256;

    private static string SaveDirectory => _gameEngineManager.ConfigDirectories.SaveGameDirectory;

    // F8 saves over this one and F9 loads it; the level start autosave is the other
    internal static string QuickSavePath => Path.Combine(SaveDirectory, SaveFile.QuickSaveFile);
    internal static string AutoSavePath => Path.Combine(SaveDirectory, SaveFile.AutoSaveFile);

    // Whether a new level saves itself as it starts (the `autosave` setting, saved in the config)
    internal static bool autosaveEnabled = true;

    // Set as a new level begins; PlayLoop saves once the first frame is drawn, so the
    // autosave has a picture of it
    private static bool autosavePending;

    /// <summary>Saves over the autosave, if it's on. Quietly: a failure is only logged.</summary>
    internal static void AutoSaveGame()
    {
        autosavePending = false;
        if (autosaveEnabled)
            SaveTheGame(AutoSavePath, "$STR_LS_AUTO".ToLanguageText(_assetManager.GetText("en-us")), 0, 0);
    }

    /// <summary>A path for a new save in the save folder, named for when it was made.</summary>
    internal static string NewSaveGamePath() => SaveFile.NewPath(SaveDirectory, DateTime.Now);

    internal static string FormatPlayTime(int tics) => SaveFile.FormatPlayTime(tics);

    /// <summary>Every save in the save folder that this build can read, newest first.</summary>
    internal static List<SaveInfo> ListSaveGames() => SaveFile.List(SaveDirectory);

    internal static SaveInfo? ReadSaveInfo(string path) => SaveFile.ReadInfo(path);
    internal static SaveThumbnail? ReadSaveThumbnail(SaveInfo save) => SaveFile.ReadThumbnail(save);
    private static byte[] ReadExactly(BinaryReader br, int count) => SaveFile.ReadExactly(br, count);

    /// <summary>The header for a save of the game as it stands.</summary>
    private static SaveInfo CurrentSaveInfo(string path, string name)
    {
        var language = _assetManager.GetText("en-us");
        var skill = _gameEngineManager.GetGameInfo().Skills.Values.ElementAtOrDefault(gamestate.difficulty);

        return new SaveInfo
        {
            Path = path,
            Name = name,
            SavedAt = DateTime.UtcNow,
            GamePack = _gameEngineManager.GamePackId,
            MapOn = gamestate.mapon,
            MapName = GetMapDisplayName(gamestate.mapon),
            Difficulty = gamestate.difficulty,
            SkillName = skill?.Name.ToLanguageText(language) ?? "",
            LevelTime = gamestate.TimeCount,
            PlayTime = gamestate.PlayTime,
            Score = playerstate.score,
            Lives = playerstate.lives,
            Health = playerstate.health,
            Kills = gamestate.killcount,
            KillTotal = gamestate.killtotal,
            Secrets = gamestate.secretcount,
            SecretTotal = gamestate.secrettotal,
            Treasure = gamestate.treasurecount,
            TreasureTotal = gamestate.treasuretotal,
            Mods = CurrentSavedMods(),
        };
    }

    /// <summary>The mods loaded now, as a save records them</summary>
    internal static List<SavedMod> CurrentSavedMods()
        => _assetManager.LoadedMods
            .Select(mod => new SavedMod(mod.DisplayName, mod.Info.Version ?? "", Path.GetFileName(mod.FullPath)))
            .ToList();

    /// <summary>Whether a save was made with other mods than are loaded now</summary>
    internal static bool HasOtherMods(SaveInfo save) => !SavedMod.SameMods(save.Mods, CurrentSavedMods());

    /// <summary>
    /// Says a save was made with other mods than are loaded now, and waits for a key: it still
    /// loads, but may not play the same
    /// </summary>
    private static void WarnOtherMods(SaveInfo save)
    {
        var language = _assetManager.GetText("en-us");
        string L(string key) => key.ToLanguageText(language);
        string ModList(List<SavedMod> mods) => mods.Count switch
        {
            0 => L("$STR_LS_NOMODS"),
            <= 3 => string.Join(", ", mods),
            _ => string.Format(L("$STR_LS_MOREMODS"), string.Join(", ", mods.Take(2)), mods.Count - 2),
        };

        Message($"{L("$STR_LS_OTHERMODS")}\n\n"
            + $"{WrapForMessage($"{L("$STR_LS_SAVEDWITH")} {ModList(save.Mods)}")}\n"
            + WrapForMessage($"{L("$STR_LS_LOADEDNOW")} {ModList(CurrentSavedMods())}"));
        _inputManager.ClearKeysDown();
        _inputManager.Ack();
    }

    /// <summary>
    /// Saves the game in progress. Written to a temporary file first, so a failed save
    /// never destroys the one it was replacing. Returns false if it couldn't be written.
    /// </summary>
    internal static bool SaveTheGame(string path, string name, int x, int y)
    {
        // A save holds one player (gametype's stats, one inventory, one pawn)
        if (players.Count > 1)
        {
            Console.WriteLine($"Couldn't save the game to {path}: games with more than one player can't be saved yet.");
            return false;
        }

        try
        {
            using var body = new MemoryStream();
            using (var bw = new BinaryWriter(body, Encoding.UTF8, leaveOpen: true))
                WriteSaveBody(bw, x, y);

            SaveFile.Write(path, CurrentSaveInfo(path, name), _videoManager.MakeThumbnail(ThumbnailWidth), body.ToArray());
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't save the game to {path}: {e.Message}");
            return false;
        }
    }

    private static void WriteSaveBody(BinaryWriter bw, int x, int y)
    {
        DiskFlopAnim(x, y);
        gamestate.Write(bw, playerstate);

        bw.Write(LevelRatios.Count);
        foreach (var (map, ratios) in LevelRatios)
        {
            bw.Write(map);
            ratios.Write(bw);
        }

        bw.Write(_inventoryManager.Items.Count);
        foreach (var (item, count) in _inventoryManager.Items)
        {
            bw.Write(item);
            bw.Write(count);
        }

        DiskFlopAnim(x, y);
        WriteLevelBody(bw);

        var weapon = SyncWeaponSprite();
        bw.Write(weapon != null);
        if (weapon != null)
            Entities.Actors.ActorSnapshot.Capture(weapon).Write(bw);

        bw.Write(weaponcharge);

        DiskFlopAnim(x, y);
        WriteHubState(bw);
    }

    /// <summary>
    /// The level as it stands, as a save holds it and as a hub keeps a level that's been left:
    /// the map and its actors (MapManager.WriteLevelState), then doors, area connections, the
    /// moving pushwalls, the player's last attacker, the automap's seen tiles and the light zones.
    /// </summary>
    private static void WriteLevelBody(BinaryWriter bw)
    {
        _mapManager.WriteLevelState(bw);

        bw.Write(lastdoorobj);
        for (int i = 0; i < lastdoorobj; i++)
            doorobjlist[i].WriteState(bw);

        bw.Write(areabyplayer.Length);
        bw.Write(areaconnect);
        bw.Write(areabyplayer);

        bw.Write(pushwalls.Count);
        foreach (var wall in pushwalls)
            wall.Write(bw);

        // Died() turns the player to face their killer, so keep who that is.
        // (By reference: actors are records, so IndexOf would match a look-alike by value.)
        bw.Write(_mapManager.GetSavedActors().FindIndex(a => ReferenceEquals(a, LastAttacker)));

        bw.Write(_mapManager.GetSeenBytes());

        WriteZoneLights(bw);
    }

    /// <summary>What <see cref="WriteLevelBody"/> wrote, read without changing any game state.</summary>
    private sealed record LevelBody(
        LevelSnapshot Level,
        doorobj_t[] Doors,
        byte[,] AreaConnect,
        byte[] AreaByPlayer,
        List<MovingPushWall> PushWalls,
        int LastAttacker,
        byte[] Seen,
        Dictionary<int, ZoneState> Zones);

    private static LevelBody ReadLevelBody(BinaryReader br)
    {
        var level = MapManager.ReadLevelState(br);

        var doors = new doorobj_t[br.ReadCount()];
        for (int i = 0; i < doors.Length; i++)
        {
            doors[i] = new doorobj_t();
            doors[i].ReadState(br);
        }

        var numareas = br.ReadInt32();
        if (numareas != _mapManager.Floors.NumAreas)
            throw new InvalidDataException($"It was saved with {numareas} map areas; this game has {_mapManager.Floors.NumAreas}.");

        return new LevelBody(
            level,
            doors,
            AreaConnect: ReadExactly(br, numareas * numareas).ToFixedArray(numareas, numareas),
            AreaByPlayer: ReadExactly(br, numareas),
            PushWalls: Enumerable.Range(0, br.ReadCount()).Select(_ => MovingPushWall.Read(br)).ToList(),
            LastAttacker: br.ReadInt32(),
            Seen: ReadExactly(br, MapManager.MAPAREA),
            Zones: ReadZoneLights(br));
    }

    /// <summary>
    /// Lays a level body over the level SetupGameLevel just built from the same map (with
    /// loadedgame set, so it didn't count kills, treasure and secrets again)
    /// </summary>
    /// <param name="ownerOf">With several players (one joining mid-game), whose each saved pawn is, by its place among the actors</param>
    private static List<Entities.Actors.Actor> ApplyLevelBody(LevelBody body, Func<int, Entities.PlayerState?>? ownerOf = null)
    {
        var actors = _mapManager.RestoreLevelState(body.Level, ownerOf);

        // Door positions and orientations come from the map; only the motion is saved.
        for (int i = 0; i < Math.Min(lastdoorobj, body.Doors.Length); i++)
        {
            doorobjlist[i].action = body.Doors[i].action;
            doorobjlist[i].ticcount = body.Doors[i].ticcount;
            doorobjlist[i].position = body.Doors[i].position;
            doorobjlist[i].held = body.Doors[i].held;
            doorobjlist[i].unlocked = body.Doors[i].unlocked;
        }

        areaconnect = body.AreaConnect;
        areabyplayer = body.AreaByPlayer;

        // copies, so a hub level's kept body isn't moved along with the live walls
        pushwalls.Clear();
        foreach (var wall in body.PushWalls)
        {
            pushwalls.Add(new MovingPushWall
            {
                State = wall.State, Pos = wall.Pos, X = wall.X, Y = wall.Y, Dir = wall.Dir, Tile = wall.Tile,
            });
        }

        _mapManager.SetSeenBytes(body.Seen);
        RestoreZoneLights(body.Zones);

        LastAttacker = body.LastAttacker >= 0 && body.LastAttacker < actors.Count ? actors[body.LastAttacker] : null;
        facetimes = 0;
        return actors;
    }

    /// <summary>Everything a save's body holds, read without changing any game state.</summary>
    private sealed record SaveGameData(
        gametype GameState,
        Entities.PlayerState Player,
        Dictionary<string, LRstruct> LevelRatios,
        Dictionary<string, int> Inventory,
        LevelBody Body,
        Entities.Actors.ActorSnapshot? Weapon,
        int WeaponCharge,
        HubState Hubs);

    private static SaveGameData ReadSaveBody(BinaryReader br)
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
            ReadLevelBody(br),
            Weapon: br.ReadBoolean() ? Entities.Actors.ActorSnapshot.Read(br) : null,
            WeaponCharge: br.ReadInt32(),
            Hubs: ReadHubState(br));
    }

    /// <summary>
    /// Loads a save over whatever is running. The caller sets <c>loadedgame</c> first, so the
    /// level rebuild doesn't re-count kills, treasure and secrets. Returns false, having shown
    /// why and changed nothing, if the save can't be loaded.
    /// </summary>
    internal static bool LoadTheGame(string path, int x, int y)
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

            if (!_gameEngineManager.GetGameInfo().Maps.ContainsKey(data.GameState.mapon))
                throw new InvalidDataException($"Map \"{data.GameState.mapon}\" isn't in this game.");

            if (FindPlayerClass(data.Player.playerclass) is not { } playerClass)
                throw new InvalidDataException($"Player class \"{data.Player.playerclass}\" isn't in this game.");
            data.Player.playerclass = playerClass;

            foreach (var weapon in new[] { data.Player.weapon, data.Player.chosenweapon })
            {
                if (weapon != null && _inventoryManager.FindClass(weapon, "Weapon") == null)
                    throw new InvalidDataException($"Weapon \"{weapon}\" isn't in this game.");
            }

            var problem = _mapManager.CheckLevelState(data.Body.Level, data.GameState.mapon);
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
                ? $"\n\n{"$STR_LS_SAVEDOTHERMODS".ToLanguageText(_assetManager.GetText("en-us"))}"
                : "";
            Message($"This saved game can't\nbe loaded.\n\n{WrapForMessage(e.Message)}{modsNote}");
            _inputManager.ClearKeysDown();
            _inputManager.Ack();
            return false;
        }

        // Read above, or the load would have stopped
        if (HasOtherMods(info!))
            WarnOtherMods(info!);

        DiskFlopAnim(x, y);
        gamestate = data.GameState;
        ResetPlayers();         // a save is a single player's game
        playerstate.CopyStatsFrom(data.Player);
        LevelRatios = data.LevelRatios;
        _inventoryManager.Restore(data.Inventory);

        // Rebuild the level from the map (doors with their locks, statics, the player), then
        // lay the saved state over it.
        SetupGameLevel();

        DiskFlopAnim(x, y);
        ApplyLevelBody(data.Body);
        RestoreHubState(data.Hubs);

        // The weapon picks up where it was (mid-attack, say).
        weaponSprite = null;
        if (SyncWeaponSprite() is { } sprite && data.Weapon != null
            && string.Equals(data.Weapon.ClassName, sprite.Name, StringComparison.OrdinalIgnoreCase))
        {
            data.Weapon.ApplyTo(sprite);
        }
        weaponcharge = data.WeaponCharge;

        if (!checksumOk)
        {
            var language = _assetManager.GetText("en-us");
            Message($"{"$STR_SAVECHT1".ToLanguageText(language)}\n{"$STR_SAVECHT2".ToLanguageText(language)}\n{"$STR_SAVECHT3".ToLanguageText(language)}\n{"$STR_SAVECHT4".ToLanguageText(language)}");

            _inputManager.ClearKeysDown();
            _inputManager.Ack();

            playerstate.oldscore = playerstate.score = 0;
            playerstate.lives = 1;
            GiveStartingInventory();
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

    static LoadSaveScreen loadSaveScreen = null!;

    internal static int CP_LoadGame(int quick)
    {
        //
        // QUICKLOAD: THE QUICKSAVE, IN PLACE, SO PLAY CARRIES STRAIGHT ON IN THE RESTORED LEVEL
        //
        if (quick != 0)
        {
            loadedgame = true;
            var loaded = LoadTheGame(QuickSavePath, 0, 0);
            loadedgame = false;
            if (!loaded)
                return 0;

            if (viewsize != 21)
                DrawPlayScreen();
            ContinueMusic(lastgamemusicoffset);
            return 1;
        }

        return loadSaveScreen.Run(saving: false);
    }

    internal static int CP_SaveGame(int quick)
    {
        //
        // QUICKSAVE: OVER THE QUICKSAVE, WITHOUT ASKING
        //
        if (quick != 0)
        {
            if (!SaveTheGame(QuickSavePath, "$STR_LS_QUICK".ToLanguageText(_assetManager.GetText("en-us")), 0, 0))
                ShowSaveFailed();
            return 1;
        }

        return loadSaveScreen.Run(saving: true);
    }

    /// <summary>What the load/save screen needs from the game</summary>
    private static LoadSaveHooks MakeLoadSaveHooks() => new(
        CurrentSave: () => CurrentSaveInfo("", ""),
        CurrentThumbnail: () => _videoManager.MakeThumbnail(ThumbnailWidth),
        ListSaves: ListSaveGames,
        NewSavePath: NewSaveGamePath,
        NewSaveName: () => GetMapDisplayName(gamestate.mapon),
        HasOtherMods: HasOtherMods,
        Load: (path, x, y) =>
        {
            loadedgame = true;
            if (LoadTheGame(path, x, y))
                return true;
            loadedgame = false;
            return false;
        },
        Save: SaveTheGame,
        Loaded: () =>
        {
            StartGame = 1;
            //
            // CHANGE "READ THIS!" TO NORMAL COLOR
            //
            FindMenuItem(MainMenu, "readthis")?.active = 1;
        },
        SaveFailed: ShowSaveFailed);
    private static void ShowSaveFailed()
    {
        Message("The game couldn't\nbe saved.");
        _inputManager.ClearKeysDown();
        _inputManager.Ack();
    }

    private static string WrapForMessage(string text) => MenuDrawing.WrapForMessage(text);

    static byte diskflopanim_which = 0;
    internal static void DiskFlopAnim(int x, int y)
    {
        if (x == 0 && y == 0)
            return;

        string[] diskpics = new[] { "c_diskloading1", "c_diskloading2" };

        _graphicManager.DrawPic(diskpics[diskflopanim_which], x, y);
        _videoManager.Update();
        diskflopanim_which ^= 1;
    }

}
