using System.Text;
using Wolf3D.Extensions;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                    SAVE GAMES

    A save file is:

        byte[32]  the save's name, Latin-1, zero padded -- the load/save menus list
                  saves from this alone
        byte[4]   "PFWS"
        int       format version
        int       body length, then the body
        int       checksum of the body

    The body holds the game (gamestate, level ratios, inventory) and the level as
    it stands (MapManager.WriteLevelState, then doors, area connections, the
    moving pushwall and the automap's seen tiles). Nothing is restored until the whole file has been read and
    checked against the current map and actordefs, so a save that can't be loaded
    leaves the game as it was.

    =============================================================================
    */

    internal const string SaveName = "savegam?.dat";
    private const int SaveSlots = 10;

    private static readonly byte[] SaveSignature = "PFWS"u8.ToArray();
    // 2: the patrol arrows became PatrolPoint actors, which version-1 saves' actor lists lack
    // 3: the automap's seen tiles follow the rest of the body; version-2 saves load with none seen
    private const int SaveVersion = 3;
    private const int OldestLoadableSaveVersion = 2;

    internal static string GetSaveGamePath(int slot) =>
        Path.Combine(_gameEngineManager.ConfigDirectories.SaveGameDirectory, SaveName.Replace('?', (char)('0' + slot)));

    /// <summary>Fills the load/save menu's slots from the save files present.</summary>
    internal static void SetupSaveGames()
    {
        for (int i = 0; i < SaveSlots; i++)
        {
            var name = ReadSaveGameName(GetSaveGamePath(i));
            SaveGamesAvail[i] = name != null ? 1 : 0;
            SaveGameNames[i] = name ?? "";
        }
    }

    /// <summary>The save's name, or null if there's no save there in this format.</summary>
    private static string? ReadSaveGameName(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            using var br = new BinaryReader(File.OpenRead(path));
            var name = ReadSaveHeaderName(br);
            return br.ReadBytes(SaveSignature.Length).AsSpan().SequenceEqual(SaveSignature) ? name : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string ReadSaveHeaderName(BinaryReader br) =>
        Encoding.Latin1.GetString(br.ReadBytes(MaxGameName)).TrimEnd('\0');

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

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var bw = new BinaryWriter(File.Create(tempPath)))
            {
                var header = new byte[MaxGameName];
                Encoding.Latin1.GetBytes(name, 0, Math.Min(name.Length, MaxGameName), header, 0);
                bw.Write(header);
                bw.Write(SaveSignature);
                bw.Write(SaveVersion);
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
        byte[]? Seen);

    private static SaveGameData ReadSaveBody(BinaryReader br, int version)
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

        return new SaveGameData(
            state,
            ratios,
            inventory,
            level,
            doors,
            AreaConnect: ReadExactly(br, MapDataConstants.NUMAREAS * MapDataConstants.NUMAREAS)
                .ToFixedArray(MapDataConstants.NUMAREAS, MapDataConstants.NUMAREAS),
            AreaByPlayer: ReadExactly(br, MapDataConstants.NUMAREAS),
            PwallState: br.ReadUInt16(),
            PwallPos: br.ReadUInt16(),
            PwallX: br.ReadUInt16(),
            PwallY: br.ReadUInt16(),
            PwallDir: (controldirs)br.ReadByte(),
            PwallTile: br.ReadByte(),
            LastAttacker: br.ReadInt32(),
            Seen: version >= 3 ? ReadExactly(br, MapManager.MAPAREA) : null);
    }

    // BinaryReader.ReadBytes quietly returns fewer bytes at the end of the stream.
    private static byte[] ReadExactly(BinaryReader br, int count)
    {
        var bytes = br.ReadBytes(count);
        if (bytes.Length != count)
            throw new EndOfStreamException();
        return bytes;
    }

    /// <summary>
    /// Loads a save over whatever is running. The caller sets <c>loadedgame</c> first, so the
    /// level rebuild doesn't re-count kills, treasure and secrets. Returns false, having shown
    /// why and changed nothing, if the save can't be loaded.
    /// </summary>
    internal static bool LoadTheGame(string path, int x, int y)
    {
        SaveGameData data;
        bool checksumOk;
        try
        {
            DiskFlopAnim(x, y);
            using var br = new BinaryReader(File.OpenRead(path));
            ReadSaveHeaderName(br);
            if (!br.ReadBytes(SaveSignature.Length).AsSpan().SequenceEqual(SaveSignature))
                throw new InvalidDataException("It isn't a PFWolf save game.");

            var version = br.ReadInt32();
            if (version < OldestLoadableSaveVersion || version > SaveVersion)
                throw new InvalidDataException($"It was saved in format {version}; this build reads {OldestLoadableSaveVersion} to {SaveVersion}.");

            var body = br.ReadBytes(br.ReadCount());
            checksumOk = br.BaseStream.Length - br.BaseStream.Position >= sizeof(int)
                && br.ReadInt32() == DoChecksum(body, 0);

            DiskFlopAnim(x, y);
            using var bodyReader = new BinaryReader(new MemoryStream(body));
            data = ReadSaveBody(bodyReader, version);

            if (!_gameEngineManager.GetGameInfo().Maps.ContainsKey(data.GameState.mapon))
                throw new InvalidDataException($"Map \"{data.GameState.mapon}\" isn't in this game.");

            var problem = _mapManager.CheckLevelState(data.Level, data.GameState.mapon);
            if (problem != null)
                throw new InvalidDataException(problem);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            // EndOfStreamException is an IOException, so a truncated file lands here too, and a
            // garbled string length is a FormatException.
            Console.WriteLine($"Couldn't load {path}: {e.Message}");
            Message($"This saved game can't\nbe loaded.\n\n{WrapForMessage(e.Message)}");
            _inputManager.ClearKeysDown();
            _inputManager.Ack();
            return false;
        }

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
        }

        areaconnect = data.AreaConnect;
        areabyplayer = data.AreaByPlayer;

        pwallstate = data.PwallState;
        pwallpos = data.PwallPos;
        pwallx = data.PwallX;
        pwally = data.PwallY;
        pwalldir = data.PwallDir;
        pwalltile = data.PwallTile;

        if (data.Seen != null)
            _mapManager.SetSeenBytes(data.Seen);    // older saves: SetupGameLevel left it all unseen

        LastAttacker = data.LastAttacker >= 0 && data.LastAttacker < actors.Count ? actors[data.LastAttacker] : null;
        facetimes = 0;

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
