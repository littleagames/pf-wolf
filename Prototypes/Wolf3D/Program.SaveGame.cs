using System.Text;
using Wolf3D.Extensions;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                    SAVE GAMES

    Every *.dat in the save folder is a save; there are no slots. A save file is:

        byte[4]   "PFWS"
        int       format version
        int       header length, then the header (SaveInfo: name, when, where, progress)
                  -- the load/save menus list saves from this alone
        int, int  thumbnail width and height, then that many palette indices
        int       body length, then the body
        int       checksum of the body

    The body holds the game (gamestate, level ratios, inventory) and the level as
    it stands (MapManager.WriteLevelState, then doors, area connections, the
    moving pushwall and the automap's seen tiles). Nothing is restored until the whole file has been read and
    checked against the current map and actordefs, so a save that can't be loaded
    leaves the game as it was.

    =============================================================================
    */

    internal const string SaveExtension = ".dat";
    internal const string QuickSaveFile = "quicksave" + SaveExtension;
    internal const string AutoSaveFile = "autosave" + SaveExtension;

    private static readonly byte[] SaveSignature = "PFWS"u8.ToArray();
    // Still in development, so a layout change bumps SaveVersion and older saves are refused
    // rather than converted. 6: the header (SaveInfo) and thumbnail ahead of the body, no slots.
    // 7: the mods loaded when it was saved, at the end of the header.
    // 8: a fourth map plane, wall heights moved from plane 2 to it (plane 2 is flats).
    // 9: the area count (mapdefs floors) ahead of the area tables.
    // 10: the player class, after the skill in gamestate.
    // 11: armor points and percent, after health in gamestate.
    // 12: a fifth map plane (tags), each actor's tag after its tile, and whether each door is
    //     held open by a switch.
    private const int SaveVersion = 12;
    private const int OldestLoadableSaveVersion = SaveVersion;

    // Thumbnails are taken this wide (less if the view is narrower), their height from the
    // view's shape; big enough to stay sharp in the menus at a few times 320x200
    private const int ThumbnailWidth = 256;
    // Anything claiming more than this is garbage
    private const int MaxThumbnailPixels = 1024 * 1024;

    private static string SaveDirectory => _gameEngineManager.ConfigDirectories.SaveGameDirectory;

    // F8 saves over this one and F9 loads it; the level start autosave is the other
    internal static string QuickSavePath => Path.Combine(SaveDirectory, QuickSaveFile);
    internal static string AutoSavePath => Path.Combine(SaveDirectory, AutoSaveFile);

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

    /// <summary>
    /// A path for a new save, named for when it was made (the save's own name is inside the
    /// file, so it can be anything).
    /// </summary>
    internal static string NewSaveGamePath()
    {
        var stem = Path.Combine(SaveDirectory, $"save_{DateTime.Now:yyyyMMdd_HHmmss}");
        var path = stem + SaveExtension;
        for (int i = 2; File.Exists(path); i++)
            path = $"{stem}_{i}{SaveExtension}";
        return path;
    }

    /// <summary>A time in tics as m:ss, or h:mm:ss from an hour up.</summary>
    internal static string FormatPlayTime(int tics)
    {
        var time = TimeSpan.FromSeconds(Math.Max(tics, 0) / 70);
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time:mm\\:ss}" : $"{time.Minutes}:{time:ss}";
    }

    /// <summary>Every save in the save folder that this build can read, newest first.</summary>
    internal static List<SaveInfo> ListSaveGames()
    {
        var saves = new List<SaveInfo>();
        try
        {
            if (!Directory.Exists(SaveDirectory))
                return saves;

            foreach (var path in Directory.EnumerateFiles(SaveDirectory, "*" + SaveExtension))
            {
                if (ReadSaveInfo(path) is { } info)
                    saves.Add(info);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't list the saves in {SaveDirectory}: {e.Message}");
        }

        saves.Sort((a, b) => b.SavedAt.CompareTo(a.SavedAt));
        return saves;
    }

    /// <summary>The save's header, or null (having said why) if it isn't a save this build reads.</summary>
    internal static SaveInfo? ReadSaveInfo(string path)
    {
        try
        {
            using var br = new BinaryReader(File.OpenRead(path));
            ReadSaveVersion(br);
            using var header = new BinaryReader(new MemoryStream(ReadExactly(br, br.ReadCount())));
            return SaveInfo.Read(header) with { Path = path };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            Console.WriteLine($"Skipping {path}: {e.Message}");
            return null;
        }
    }

    /// <summary>Checks the signature and reads the format version, refusing one this build can't read.</summary>
    private static int ReadSaveVersion(BinaryReader br)
    {
        if (!br.ReadBytes(SaveSignature.Length).AsSpan().SequenceEqual(SaveSignature))
            throw new InvalidDataException("It isn't a PFWolf save game.");

        var version = br.ReadInt32();
        if (version < OldestLoadableSaveVersion || version > SaveVersion)
            throw new InvalidDataException($"It was saved in format {version}; this build reads {OldestLoadableSaveVersion} to {SaveVersion}.");
        return version;
    }

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
            Score = gamestate.score,
            Lives = gamestate.lives,
            Health = gamestate.health,
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
        var tempPath = path + ".tmp";
        try
        {
            using var body = new MemoryStream();
            using (var bw = new BinaryWriter(body, Encoding.UTF8, leaveOpen: true))
                WriteSaveBody(bw, x, y);
            var bodyBytes = body.ToArray();

            using var header = new MemoryStream();
            using (var bw = new BinaryWriter(header, Encoding.UTF8, leaveOpen: true))
                CurrentSaveInfo(path, name).Write(bw);
            var headerBytes = header.ToArray();
            var thumbnail = _videoManager.MakeThumbnail(ThumbnailWidth);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var bw = new BinaryWriter(File.Create(tempPath)))
            {
                bw.Write(SaveSignature);
                bw.Write(SaveVersion);
                bw.Write(headerBytes.Length);
                bw.Write(headerBytes);
                bw.Write(thumbnail?.Width ?? 0);
                bw.Write(thumbnail?.Height ?? 0);
                if (thumbnail != null)
                    bw.Write(thumbnail.Pixels);
                bw.Write(bodyBytes.Length);
                bw.Write(bodyBytes);
                bw.Write(DoChecksum(bodyBytes, 0));
            }

            File.Move(tempPath, path, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't save the game to {path}: {e.Message}");
            try { File.Delete(tempPath); } catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
            return false;
        }
    }

    private static void WriteSaveBody(BinaryWriter bw, int x, int y)
    {
        DiskFlopAnim(x, y);
        gamestate.Write(bw);

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
        _mapManager.WriteLevelState(bw);

        DiskFlopAnim(x, y);
        bw.Write(lastdoorobj);
        for (int i = 0; i < lastdoorobj; i++)
            doorobjlist[i].WriteState(bw);

        bw.Write(areabyplayer.Length);
        bw.Write(areaconnect);
        bw.Write(areabyplayer);

        bw.Write(pwallstate);
        bw.Write(pwallpos);
        bw.Write(pwallx);
        bw.Write(pwally);
        bw.Write((byte)pwalldir);
        bw.Write(pwalltile);

        // Died() turns the player to face their killer, so keep who that is.
        // (By reference: actors are records, so IndexOf would match a look-alike by value.)
        bw.Write(_mapManager.GetSavedActors().FindIndex(a => ReferenceEquals(a, LastAttacker)));

        bw.Write(_mapManager.GetSeenBytes());

        var weapon = SyncWeaponSprite();
        bw.Write(weapon != null);
        if (weapon != null)
            Entities.Actors.ActorSnapshot.Capture(weapon).Write(bw);
    }

    /// <summary>Everything a save's body holds, read without changing any game state.</summary>
    private sealed record SaveGameData(
        gametype GameState,
        Dictionary<string, LRstruct> LevelRatios,
        Dictionary<string, int> Inventory,
        LevelSnapshot Level,
        doorobj_t[] Doors,
        byte[,] AreaConnect,
        byte[] AreaByPlayer,
        ushort PwallState,
        ushort PwallPos,
        ushort PwallX,
        ushort PwallY,
        controldirs PwallDir,
        byte PwallTile,
        int LastAttacker,
        byte[] Seen,
        Entities.Actors.ActorSnapshot? Weapon);

    private static SaveGameData ReadSaveBody(BinaryReader br)
    {
        var state = gametype.Read(br);

        var ratios = new Dictionary<string, LRstruct>();
        for (int i = br.ReadCount(); i > 0; i--)
            ratios[br.ReadString()] = LRstruct.Read(br);

        var inventory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = br.ReadCount(); i > 0; i--)
            inventory[br.ReadString()] = br.ReadInt32();

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

        return new SaveGameData(
            state,
            ratios,
            inventory,
            level,
            doors,
            AreaConnect: ReadExactly(br, numareas * numareas).ToFixedArray(numareas, numareas),
            AreaByPlayer: ReadExactly(br, numareas),
            PwallState: br.ReadUInt16(),
            PwallPos: br.ReadUInt16(),
            PwallX: br.ReadUInt16(),
            PwallY: br.ReadUInt16(),
            PwallDir: (controldirs)br.ReadByte(),
            PwallTile: br.ReadByte(),
            LastAttacker: br.ReadInt32(),
            Seen: ReadExactly(br, MapManager.MAPAREA),
            Weapon: br.ReadBoolean() ? Entities.Actors.ActorSnapshot.Read(br) : null);
    }

    // BinaryReader.ReadBytes quietly returns fewer bytes at the end of the stream.
    private static byte[] ReadExactly(BinaryReader br, int count)
    {
        var bytes = br.ReadBytes(count);
        if (bytes.Length != count)
            throw new EndOfStreamException();
        return bytes;
    }

    private static SaveThumbnail? ReadThumbnail(BinaryReader br)
    {
        int width = br.ReadInt32(), height = br.ReadInt32();
        if (width < 0 || height < 0 || (long)width * height > MaxThumbnailPixels)
            throw new InvalidDataException($"Bad thumbnail size {width}x{height}.");
        var pixels = ReadExactly(br, width * height);
        return width > 0 && height > 0 ? new SaveThumbnail(width, height, pixels) : null;
    }

    /// <summary>
    /// The picture of the game kept with a save, read only when the menu shows it; null if it
    /// has none or can't be read.
    /// </summary>
    internal static SaveThumbnail? ReadSaveThumbnail(SaveInfo save)
    {
        try
        {
            using var br = new BinaryReader(File.OpenRead(save.Path));
            ReadSaveVersion(br);
            ReadExactly(br, br.ReadCount());
            return ReadThumbnail(br);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Console.WriteLine($"Couldn't read the picture in {save.Path}: {e.Message}");
            return null;
        }
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
            using var br = new BinaryReader(File.OpenRead(path));
            ReadSaveVersion(br);
            // The header is for the menus, and for saying whether it was saved with other mods
            using (var header = new BinaryReader(new MemoryStream(ReadExactly(br, br.ReadCount()))))
                info = SaveInfo.Read(header);
            ReadThumbnail(br);

            var body = br.ReadBytes(br.ReadCount());
            checksumOk = br.BaseStream.Length - br.BaseStream.Position >= sizeof(int)
                && br.ReadInt32() == DoChecksum(body, 0);

            DiskFlopAnim(x, y);
            using var bodyReader = new BinaryReader(new MemoryStream(body));
            data = ReadSaveBody(bodyReader);

            if (!_gameEngineManager.GetGameInfo().Maps.ContainsKey(data.GameState.mapon))
                throw new InvalidDataException($"Map \"{data.GameState.mapon}\" isn't in this game.");

            if (FindPlayerClass(data.GameState.playerclass) is not { } playerClass)
                throw new InvalidDataException($"Player class \"{data.GameState.playerclass}\" isn't in this game.");
            data.GameState.playerclass = playerClass;

            foreach (var weapon in new[] { data.GameState.weapon, data.GameState.chosenweapon })
            {
                if (weapon != null && _inventoryManager.FindClass(weapon, "Weapon") == null)
                    throw new InvalidDataException($"Weapon \"{weapon}\" isn't in this game.");
            }

            var problem = _mapManager.CheckLevelState(data.Level, data.GameState.mapon);
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
        LevelRatios = data.LevelRatios;
        _inventoryManager.Restore(data.Inventory);

        // Rebuild the level from the map (doors with their locks, statics, the player), then
        // lay the saved state over it.
        SetupGameLevel();

        DiskFlopAnim(x, y);
        var actors = _mapManager.RestoreLevelState(data.Level);

        // Door positions and orientations come from the map; only the motion is saved.
        for (int i = 0; i < Math.Min(lastdoorobj, data.Doors.Length); i++)
        {
            doorobjlist[i].action = data.Doors[i].action;
            doorobjlist[i].ticcount = data.Doors[i].ticcount;
            doorobjlist[i].position = data.Doors[i].position;
            doorobjlist[i].held = data.Doors[i].held;
        }

        areaconnect = data.AreaConnect;
        areabyplayer = data.AreaByPlayer;

        pwallstate = data.PwallState;
        pwallpos = data.PwallPos;
        pwallx = data.PwallX;
        pwally = data.PwallY;
        pwalldir = data.PwallDir;
        pwalltile = data.PwallTile;

        _mapManager.SetSeenBytes(data.Seen);

        LastAttacker = data.LastAttacker >= 0 && data.LastAttacker < actors.Count ? actors[data.LastAttacker] : null;
        facetimes = 0;

        // The weapon picks up where it was (mid-attack, say).
        weaponSprite = null;
        if (SyncWeaponSprite() is { } sprite && data.Weapon != null
            && string.Equals(data.Weapon.ClassName, sprite.Name, StringComparison.OrdinalIgnoreCase))
        {
            data.Weapon.ApplyTo(sprite);
        }

        if (!checksumOk)
        {
            var language = _assetManager.GetText("en-us");
            Message($"{"$STR_SAVECHT1".ToLanguageText(language)}\n{"$STR_SAVECHT2".ToLanguageText(language)}\n{"$STR_SAVECHT3".ToLanguageText(language)}\n{"$STR_SAVECHT4".ToLanguageText(language)}");

            _inputManager.ClearKeysDown();
            _inputManager.Ack();

            gamestate.oldscore = gamestate.score = 0;
            gamestate.lives = 1;
            GiveStartingInventory();
        }

        return true;
    }

    private static void ShowSaveFailed()
    {
        Message("The game couldn't\nbe saved.");
        _inputManager.ClearKeysDown();
        _inputManager.Ack();
    }

    // Message boxes don't wrap, so break a long reason onto lines of about 28 characters.
    private static string WrapForMessage(string text)
    {
        var lines = new List<string>();
        var line = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > 28)
            {
                lines.Add(line.ToString());
                line.Clear();
            }
            if (line.Length > 0)
                line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0)
            lines.Add(line.ToString());
        return string.Join('\n', lines);
    }

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

    internal static int DoChecksum(byte[] source, int checksum)
    {
        for (int i = 0; i < source.Length - 1; i++)
            checksum += source[i] ^ source[i + 1];

        return checksum;
    }
}

internal enum SaveKind { Normal, Quick, Auto }

/// <summary>
/// A mod a save was made with: its modinfo.yaml name (or file name) and version, and the file
/// or folder it was loaded from
/// </summary>
internal sealed record SavedMod(string Name, string Version, string FileName)
{
    public override string ToString() => Version.Length == 0 ? Name : $"{Name} {Version}";

    /// <summary>
    /// Whether two lists name the same mods, by name and version, in the same order (a
    /// different order can change which mod's files win)
    /// </summary>
    public static bool SameMods(IReadOnlyList<SavedMod> a, IReadOnlyList<SavedMod> b)
        => a.Count == b.Count && a.Zip(b).All(pair =>
            string.Equals(pair.First.Name, pair.Second.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(pair.First.Version, pair.Second.Version, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A save's picture of the game: game palette indices, a row at a time.</summary>
internal sealed record SaveThumbnail(int Width, int Height, byte[] Pixels);

/// <summary>
/// A save's header: what the load/save menus show about it, read without loading the game.
/// Map and skill names are kept as they read when saved, so a save from another game pack
/// or mod still shows them.
/// </summary>
internal sealed record SaveInfo
{
    /// <summary>The file it was read from; not stored in it.</summary>
    public string Path { get; init; } = "";

    public string Name { get; init; } = "";
    public DateTime SavedAt { get; init; }      // UTC
    public string GamePack { get; init; } = "";
    public string MapOn { get; init; } = "";
    public string MapName { get; init; } = "";
    public short Difficulty { get; init; }      // the skill's place in game-info's skills
    public string SkillName { get; init; } = "";
    public int LevelTime { get; init; }         // tics on this level
    public int PlayTime { get; init; }          // tics since the game began
    public int Score { get; init; }
    public short Lives { get; init; }
    public short Health { get; init; }
    public short Kills { get; init; }
    public short KillTotal { get; init; }
    public short Secrets { get; init; }
    public short SecretTotal { get; init; }
    public short Treasure { get; init; }
    public short TreasureTotal { get; init; }

    /// <summary>The mods loaded when it was saved, in load order</summary>
    public List<SavedMod> Mods { get; init; } = [];

    // Anything claiming more than this is garbage
    private const int MaxMods = 1000;

    public SaveKind Kind => System.IO.Path.GetFileName(Path).ToLowerInvariant() switch
    {
        Program.QuickSaveFile => SaveKind.Quick,
        Program.AutoSaveFile => SaveKind.Auto,
        _ => SaveKind.Normal,
    };

    public void Write(BinaryWriter bw)
    {
        bw.Write(Name);
        bw.Write(SavedAt.ToUniversalTime().Ticks);
        bw.Write(GamePack);
        bw.Write(MapOn);
        bw.Write(MapName);
        bw.Write((short)Difficulty);
        bw.Write(SkillName);
        bw.Write(LevelTime);
        bw.Write(PlayTime);
        bw.Write(Score);
        bw.Write(Lives);
        bw.Write(Health);
        bw.Write(Kills);
        bw.Write(KillTotal);
        bw.Write(Secrets);
        bw.Write(SecretTotal);
        bw.Write(Treasure);
        bw.Write(TreasureTotal);

        bw.Write(Mods.Count);
        foreach (var mod in Mods)
        {
            bw.Write(mod.Name);
            bw.Write(mod.Version);
            bw.Write(mod.FileName);
        }
    }

    public static SaveInfo Read(BinaryReader br)
    {
        var name = br.ReadString();
        var ticks = br.ReadInt64();
        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            throw new InvalidDataException($"Bad save time {ticks}.");

        var info = ReadStats(br, name, ticks);

        var modCount = br.ReadInt32();
        if (modCount < 0 || modCount > MaxMods)
            throw new InvalidDataException($"Bad mod count {modCount}.");
        for (int i = 0; i < modCount; i++)
            info.Mods.Add(new SavedMod(br.ReadString(), br.ReadString(), br.ReadString()));

        return info;
    }

    private static SaveInfo ReadStats(BinaryReader br, string name, long ticks)
    {
        return new SaveInfo
        {
            Name = name,
            SavedAt = new DateTime(ticks, DateTimeKind.Utc),
            GamePack = br.ReadString(),
            MapOn = br.ReadString(),
            MapName = br.ReadString(),
            Difficulty = br.ReadInt16(),
            SkillName = br.ReadString(),
            LevelTime = br.ReadInt32(),
            PlayTime = br.ReadInt32(),
            Score = br.ReadInt32(),
            Lives = br.ReadInt16(),
            Health = br.ReadInt16(),
            Kills = br.ReadInt16(),
            KillTotal = br.ReadInt16(),
            Secrets = br.ReadInt16(),
            SecretTotal = br.ReadInt16(),
            Treasure = br.ReadInt16(),
            TreasureTotal = br.ReadInt16(),
        };
    }
}
