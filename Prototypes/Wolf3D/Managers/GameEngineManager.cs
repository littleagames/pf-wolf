using SDL2;
using System.ComponentModel;
using System.Reflection;
using Wolf3D.Assets;
using Wolf3D.Configuration;

namespace Wolf3D.Managers;


internal enum GameType
{
    [Description("wolf3d")]
    Wolf3D,
    [Description("spear")]
    SpearOfDestiny
}

internal class GameEngineManager
{
    private readonly VideoManager videoManager;
    private readonly InputManager inputManager;
    private readonly AudioManager audioManager;
    private readonly Lazy<AssetManager> assetManager;
    private readonly ConsoleManager consoleManager;
    private readonly AutomapManager automapManager;

    public GameEngineManager(
        VideoManager videoManager,
        InputManager inputManager,
        AudioManager audioManager,
        Lazy<AssetManager> assetManager,
        ConsoleManager consoleManager,
        AutomapManager automapManager)
    {
        this.videoManager = videoManager;
        this.inputManager = inputManager;
        this.consoleManager = consoleManager;
        this.automapManager = automapManager;
        InputManager.Quit += Quit;
        InputManager.Pause += SetPaused;
        this.audioManager = audioManager;
        this.assetManager = assetManager;
    }

    /// <summary>
    /// Console parameters from commandline or shortcuts. Will be used to override config values
    /// </summary>
    public GameParams GameParams { get; private set; }
    
    public ConfigDirectories ConfigDirectories { get; private set; }

    public GameType GameType { get; set; }


    internal bool Paused;

    internal const string ConfigFileName = "config.cfg";
    private const ushort ConfigSignature = 0xfefa;

    public void Init(GameParams args)
    {
        // First: the settings and save folders depend on which game is running
        GameType = ParseGameType(args.Game);
        ReadConfigData(args);
    }

    /// <summary>
    /// The game --game names by its pack id ("spear"); Wolf3D when unset or unknown
    /// </summary>
    private static GameType ParseGameType(string gamePackId)
    {
        if (string.IsNullOrWhiteSpace(gamePackId))
            return GameType.Wolf3D;

        foreach (var type in Enum.GetValues<GameType>())
        {
            if (GetGamePackId(type).Equals(gamePackId.Trim(), StringComparison.OrdinalIgnoreCase))
                return type;
        }

        Console.WriteLine($"Unknown --game '{gamePackId}' (expected {string.Join(", ", KnownGamePackIds)}); running {GetGamePackId(GameType.Wolf3D)}.");
        return GameType.Wolf3D;
    }

    /// <summary>
    /// Name of the running game pack ("wolf3d", "spear"): the gamepacks/ folder name,
    /// and what menudefs list under game-packs
    /// </summary>
    public string GamePackId => GetGamePackId(GameType);

    /// <summary>
    /// Key of the running release in gamepacks/gamepack-info.yaml, which names its data files
    /// and palette. Fixed per game until the release is detected from the data files.
    /// </summary>
    public string GameReleaseId => GameType switch
    {
        GameType.SpearOfDestiny => "spear",
        _ => "wolf3d-apogee",
    };

    /// <summary>
    /// Folder under %APPDATA%\PFWolf holding this game's settings, high scores and saves,
    /// so games don't share them. Wolf3D keeps the folder it has always used.
    /// </summary>
    private string GameDataFolderName => GameType switch
    {
        GameType.SpearOfDestiny => "SpearOfDestiny",
        _ => "Wolfenstein3D",
    };

    /// <summary>
    /// Every game pack name the engine knows about
    /// </summary>
    public static IEnumerable<string> KnownGamePackIds => Enum.GetValues<GameType>().Select(GetGamePackId);

    private static string GetGamePackId(GameType type)
        => typeof(GameType).GetField(type.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description
           ?? type.ToString().ToLowerInvariant();

    public GameInfoAsset GetGameInfo()
    {
        var gameInfo = assetManager.Value.FindInGamePack<GameInfoAsset>("game-info");
        if (gameInfo == null)
            throw new Exception("Game info not found");
        return gameInfo;
    }

    internal const string BindsFileName = "binds.cfg";
    internal const string AutoexecFileName = "autoexec.cfg";

    /// <summary>Where a file of the given name lives in the config directory (config.cfg, binds.cfg, autoexec.cfg).</summary>
    internal string GetConfigFilePath(string fileName) =>
        string.IsNullOrEmpty(ConfigDirectories.ConfigDirectory) ? fileName : Path.Combine(ConfigDirectories.ConfigDirectory, fileName);

    /// <summary>The settings config.cfg holds, read in full before any of them is applied.</summary>
    private sealed class ConfigData
    {
        public required HighScore[] Scores;
        public bool MouseEnabled, JoystickEnabled;
        public required ScanCodes[] DirScan, ButtonScan;
        public required buttontypes[] ButtonMouse, ButtonJoy;
        public int ViewSize, MouseAdjustment;
        public bool? PauseWhenOpen;
        public ScanCodes? AutomapKey;
        public AutomapStyle? AutomapStyle;
        public bool? AutomapOverlay;
        public bool? AutomapGrid;
        public ScanCodes[]? AutomapKeys;
        public AudioDevices? AudioDevices;
        public int? SoundVolume, MusicVolume;
    }

    /// <summary>The sound devices and music switched on in the Sound menu, as saved in config.cfg.</summary>
    [Flags]
    private enum AudioDevices : byte
    {
        None = 0,
        PcSound = 1,
        AdLibSound = 2,
        DigitizedSound = 4,
        Music = 8,
    }

    // Keyboard buttons stored in the original fixed layout. Buttons added after them (the
    // automap key) are appended at the end, so older configs still read correctly.
    private const int LayoutButtonCount = (int)buttontypes.bt_automap;

    internal void ReadConfig()
    {
        string configpath = GetConfigFilePath(ConfigFileName);

        if (!File.Exists(configpath))
        {
            SetDefaultConfig();
            return;
        }

        ConfigData config;
        try
        {
            using var br = new BinaryReader(File.OpenRead(configpath));
            if (br.ReadUInt16() != ConfigSignature)
            {
                Console.WriteLine($"{configpath} isn't a config file; using the default settings.");
                SetDefaultConfig();
                return;
            }

            config = ParseConfig(br);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A truncated or unreadable config (EndOfStreamException is an IOException). It's
            // left in place: WriteConfig replaces it on exit, and until then the high scores in
            // it can still be recovered by hand.
            Console.WriteLine($"Couldn't read {configpath} ({e.Message}); using the default settings.");
            SetDefaultConfig();
            return;
        }

        Array.Copy(config.Scores, Program.Scores, Program.Scores.Length);
        Array.Copy(config.DirScan, Program.dirscan, Program.dirscan.Length);
        Array.Copy(config.ButtonScan, Program.buttonscan, LayoutButtonCount);
        if (config.AutomapKey is ScanCodes automapKey)
            Program.buttonscan[(int)buttontypes.bt_automap] = automapKey;
        Array.Copy(config.ButtonMouse, Program.buttonmouse, Program.buttonmouse.Length);
        Array.Copy(config.ButtonJoy, Program.buttonjoy, Program.buttonjoy.Length);

        // Devices that were there last time may not be now.
        Program.mouseenabled = config.MouseEnabled && inputManager.IsMousePresent();
        Program.joystickenabled = config.JoystickEnabled && inputManager.JoyPresent();
        Program.mouseadjustment = Math.Clamp(config.MouseAdjustment, 0, 9);
        Program.viewsize = Math.Clamp(config.ViewSize, 4, 21);
        if (config.PauseWhenOpen is bool pause)
            consoleManager.PauseWhenOpen = pause;
        if (config.AutomapStyle is AutomapStyle style && Enum.IsDefined(style))
            automapManager.Style = style;
        if (config.AutomapOverlay is bool overlay)
            automapManager.Overlay = overlay;
        if (config.AutomapGrid is bool grid)
            automapManager.ShowGrid = grid;
        if (config.AudioDevices is AudioDevices devices)
        {
            audioManager.PcSoundEnabled = devices.HasFlag(AudioDevices.PcSound);
            audioManager.AdLibSoundEnabled = devices.HasFlag(AudioDevices.AdLibSound);
            audioManager.DigitizedSoundEnabled = devices.HasFlag(AudioDevices.DigitizedSound);
            audioManager.MusicEnabled = devices.HasFlag(AudioDevices.Music);
        }
        if (config.SoundVolume is int soundVolume)
            audioManager.SoundVolume = soundVolume;     // clamped by the setter
        if (config.MusicVolume is int musicVolume)
            audioManager.MusicVolume = musicVolume;

        // A key the config doesn't have (it's from before that key existed) keeps its default
        var automapKeys = config.AutomapKeys ?? [];
        for (int i = 0; i < Math.Min(automapKeys.Length, Program.automapscan.Length); i++)
        {
            if (automapKeys[i] > ScanCodes.sc_None && automapKeys[i] < ScanCodes.sc_Last)
                Program.automapscan[i] = automapKeys[i];
        }

        // Set "Read This" back to standard active
        Program.FindMenuItem(Program.MainMenu, "readthis")?.active = 1;
        Program.MainItems.curpos = 0;
    }

    private static ConfigData ParseConfig(BinaryReader br)
    {
        var scores = new HighScore[Program.Scores.Length];
        for (int i = 0; i < scores.Length; i++)
        {
            scores[i] = new HighScore();
            scores[i].Read(br);
        }

        br.ReadBytes(3); // the original's sound, music and digitized modes -- unused; the device switches are appended at the end

        var config = new ConfigData
        {
            Scores = scores,
            MouseEnabled = br.ReadByte() != 0,
            JoystickEnabled = br.ReadByte() != 0,
            DirScan = new ScanCodes[Program.dirscan.Length],
            ButtonScan = new ScanCodes[LayoutButtonCount],
            ButtonMouse = new buttontypes[Program.buttonmouse.Length],
            ButtonJoy = new buttontypes[Program.buttonjoy.Length],
        };
        _ = br.ReadByte(); // joypad enabled placeholder
        _ = br.ReadByte(); // joystick progressive placeholder
        _ = br.ReadInt32(); // joystick port placeholder

        for (int i = 0; i < config.DirScan.Length; i++)
            config.DirScan[i] = (ScanCodes)br.ReadInt32();
        for (int i = 0; i < config.ButtonScan.Length; i++)
            config.ButtonScan[i] = (ScanCodes)br.ReadInt32();
        for (int i = 0; i < config.ButtonMouse.Length; i++)
            config.ButtonMouse[i] = (buttontypes)br.ReadInt32();
        for (int i = 0; i < config.ButtonJoy.Length; i++)
            config.ButtonJoy[i] = (buttontypes)br.ReadInt32();

        config.ViewSize = br.ReadInt32();
        config.MouseAdjustment = br.ReadInt32();

        // Settings appended after the original layout. Older configs end before them, so
        // each is read only if present.
        var stream = br.BaseStream;
        if (stream.Position < stream.Length)
            config.PauseWhenOpen = br.ReadByte() != 0;
        if (stream.Position < stream.Length)
            config.AutomapKey = (ScanCodes)br.ReadInt32();
        if (stream.Position < stream.Length)
            config.AutomapStyle = (AutomapStyle)br.ReadByte();
        if (stream.Position < stream.Length)
            config.AutomapOverlay = br.ReadByte() != 0;
        if (stream.Position < stream.Length)
            config.AutomapGrid = br.ReadByte() != 0;
        if (stream.Position < stream.Length)
        {
            // Counted, so keys added to the automap later still read from older configs
            int count = br.ReadInt32();
            if (count >= 0 && count <= 256)     // anything else is garbage: keep the default keys
            {
                config.AutomapKeys = new ScanCodes[count];
                for (int i = 0; i < count; i++)
                    config.AutomapKeys[i] = (ScanCodes)br.ReadInt32();
            }
        }
        if (stream.Position < stream.Length)
            config.AudioDevices = (AudioDevices)br.ReadByte();
        if (stream.Position < stream.Length)
            config.SoundVolume = br.ReadByte();
        if (stream.Position < stream.Length)
            config.MusicVolume = br.ReadByte();

        return config;
    }

    private void SetDefaultConfig()
    {
        // Every sound device and music start switched on (AudioManager's defaults).
        if (inputManager.IsMousePresent())
            Program.mouseenabled = true;

        if (inputManager.JoyPresent())
            Program.joystickenabled = true;

        Program.viewsize = 19;
        Program.mouseadjustment = 5;
    }

    /// <summary>
    /// Writes config.cfg and binds.cfg. Called on exit, and whenever the settings or high
    /// scores change, so they survive a crash. A failure is logged, not thrown, since this
    /// also runs on the way out of the game.
    /// </summary>
    internal void WriteConfig()
    {
        try
        {
            // Binds are console commands, so they're saved as a script the console runs at
            // startup. Written even when empty so that unbinding everything sticks.
            ReplaceFile(GetConfigFilePath(BindsFileName), stream =>
            {
                using var writer = new StreamWriter(stream);
                writer.WriteLine("// Written by the game; use autoexec.cfg for your own commands.");
                foreach (var command in consoleManager.GetBindCommands())
                    writer.WriteLine(command);
            });

            ReplaceFile(GetConfigFilePath(ConfigFileName), stream =>
            {
                using var bw = new BinaryWriter(stream);
                WriteConfigData(bw);
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't save the settings to {ConfigDirectories.ConfigDirectory}: {e.Message}");
        }
    }

    // Writes a temporary file and then swaps it in, so a failed write leaves the old file whole.
    private static void ReplaceFile(string path, Action<Stream> write)
    {
        var tempPath = path + ".tmp";
        using (var stream = File.Create(tempPath))
            write(stream);
        File.Move(tempPath, path, overwrite: true);
    }

    private void WriteConfigData(BinaryWriter bw)
    {
        bw.Write(ConfigSignature);
        foreach (var s in Program.Scores)
            s.Write(bw);

        bw.Write((byte)0); // sound mode placeholder
        bw.Write((byte)0); // music mode placeholder
        bw.Write((byte)0); // digitized mode placeholder

        bw.Write(Program.mouseenabled);
        bw.Write(Program.joystickenabled);
        bw.Write((byte)0); // joypad placeholder
        bw.Write((byte)0); // joystick-progressive placeholder
        bw.Write((int)0); // joystick port placeholder

        for (int i = 0; i < Program.dirscan.Length; i++)
            bw.Write((int)Program.dirscan[i]);

        for (int i = 0; i < LayoutButtonCount; i++)
            bw.Write((int)Program.buttonscan[i]);

        for (int i = 0; i < Program.buttonmouse.Length; i++)
            bw.Write((int)Program.buttonmouse[i]);

        for (int i = 0; i < Program.buttonjoy.Length; i++)
            bw.Write((int)Program.buttonjoy[i]);

        bw.Write(Program.viewsize);
        bw.Write(Program.mouseadjustment);
        bw.Write(consoleManager.PauseWhenOpen);
        bw.Write((int)Program.buttonscan[(int)buttontypes.bt_automap]);
        bw.Write((byte)automapManager.Style);
        bw.Write(automapManager.Overlay);
        bw.Write(automapManager.ShowGrid);
        bw.Write(Program.automapscan.Length);
        foreach (var key in Program.automapscan)
            bw.Write((int)key);

        var devices = AudioDevices.None;
        if (audioManager.PcSoundEnabled)
            devices |= AudioDevices.PcSound;
        if (audioManager.AdLibSoundEnabled)
            devices |= AudioDevices.AdLibSound;
        if (audioManager.DigitizedSoundEnabled)
            devices |= AudioDevices.DigitizedSound;
        if (audioManager.MusicEnabled)
            devices |= AudioDevices.Music;
        bw.Write((byte)devices);
        bw.Write((byte)audioManager.SoundVolume);
        bw.Write((byte)audioManager.MusicVolume);
    }

    /// <summary>
    /// Settles where settings and saves live: the %APPDATA% defaults, unless --configdir or
    /// --savedir name somewhere else.
    /// </summary>
    public void ReadConfigData(GameParams args)
    {
        GameParams = args;

        var directories = ConfigDirectories.Default(GameDataFolderName);
        if (!string.IsNullOrWhiteSpace(args.ConfigDir))
            directories = directories with { ConfigDirectory = Path.GetFullPath(args.ConfigDir) };
        if (!string.IsNullOrWhiteSpace(args.SavesDir))
            directories = directories with { SaveGameDirectory = Path.GetFullPath(args.SavesDir) };

        try
        {
            Directory.CreateDirectory(directories.ConfigDirectory);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Nothing is on screen yet to report this, so carry on with the settings next to
            // the game, where they used to live.
            Console.WriteLine($"Couldn't create {directories.ConfigDirectory} ({e.Message}); using the game folder for settings.");
            directories = directories with { ConfigDirectory = "" };
        }

        ConfigDirectories = directories;
        CopyLegacyConfigFiles();
    }

    /// <summary>
    /// Settings used to be kept in the game's working folder. The first time the config folder
    /// has none of its own, copy them over (copy, not move, so an older build still finds them).
    /// </summary>
    private void CopyLegacyConfigFiles()
    {
        var configDirectory = ConfigDirectories.ConfigDirectory;
        if (string.IsNullOrEmpty(configDirectory)
            || string.Equals(Path.GetFullPath(configDirectory).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(Environment.CurrentDirectory).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var fileName in new[] { ConfigFileName, BindsFileName, AutoexecFileName })
        {
            var legacyPath = Path.Combine(Environment.CurrentDirectory, fileName);
            var newPath = GetConfigFilePath(fileName);
            try
            {
                if (File.Exists(legacyPath) && !File.Exists(newPath))
                {
                    File.Copy(legacyPath, newPath);
                    Console.WriteLine($"Copied {legacyPath} to {newPath}");
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Couldn't copy {legacyPath} to {newPath}: {e.Message}");
            }
        }
    }


    internal static uint GetTimeCount() => ((SDL.SDL_GetTicks() * 7) / 100);

    internal static void DelayTics(int wolfticks)
    {
        if (wolfticks > 0)
            SDL.SDL_Delay((uint)((wolfticks * 100) / 7));
    }

    internal static void DelayMs(uint millis)
    {
        if (millis > 0)
            SDL.SDL_Delay(millis);
    }

    internal static void WaitVBL(uint a) => DelayMs((a) * 8);
    
    public void Quit(object? sender, EventArgs e)
    {
        Quit("");
    }
    public void Quit(string errorStr)
    {
        var returnCode = errorStr.Length > 0 ? 1 : 0;

        if (returnCode == 0)
            WriteConfig(); // TODO: This should happen every setting change

        Shutdown();

        if (returnCode != 0)
            Error(errorStr);

        Environment.Exit(returnCode);
    }
    public static void Error(string errorStr)
    {
        SDL2.SDL.SDL_ShowSimpleMessageBox(SDL2.SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR, "Wolf4CSharp", errorStr, IntPtr.Zero);
    }

    public bool IsPaused() => Paused;

    public void SetPaused(object? sender, bool paused)
        => SetPaused(paused);

    public void SetPaused(bool paused) => Paused = paused;

    public void Shutdown()
    {
        videoManager.Shutdown();
        inputManager.Shutdown();
        audioManager.Shutdown();

        //US_Shutdown(); // This line is completely useless...
        //SD_Shutdown();
        //PM_Shutdown();
        //IN_Shutdown();
        //CA_Shutdown();
    }
}
