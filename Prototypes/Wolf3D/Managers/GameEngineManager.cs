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

    public GameEngineManager(
        VideoManager videoManager,
        InputManager inputManager,
        AudioManager audioManager,
        Lazy<AssetManager> assetManager,
        ConsoleManager consoleManager)
    {
        this.videoManager = videoManager;
        this.inputManager = inputManager;
        this.consoleManager = consoleManager;
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
    }

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
        Array.Copy(config.ButtonScan, Program.buttonscan, Program.buttonscan.Length);
        Array.Copy(config.ButtonMouse, Program.buttonmouse, Program.buttonmouse.Length);
        Array.Copy(config.ButtonJoy, Program.buttonjoy, Program.buttonjoy.Length);

        // Devices that were there last time may not be now.
        Program.mouseenabled = config.MouseEnabled && inputManager.IsMousePresent();
        Program.joystickenabled = config.JoystickEnabled && inputManager.JoyPresent();
        Program.mouseadjustment = Math.Clamp(config.MouseAdjustment, 0, 9);
        Program.viewsize = Math.Clamp(config.ViewSize, 4, 21);
        if (config.PauseWhenOpen is bool pause)
            consoleManager.PauseWhenOpen = pause;

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

        br.ReadBytes(3); // sound, music and digitized sound modes -- not settings in this port yet

        var config = new ConfigData
        {
            Scores = scores,
            MouseEnabled = br.ReadByte() != 0,
            JoystickEnabled = br.ReadByte() != 0,
            DirScan = new ScanCodes[Program.dirscan.Length],
            ButtonScan = new ScanCodes[Program.buttonscan.Length],
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

        return config;
    }

    private void SetDefaultConfig()
    {
        //SDMode sd;
        //SMMode sm;
        //SDSMode sds;
        //if (Program.SoundBlasterPresent || Program.AdLibPresent)
        //{
            //sd = SDMode.AdLib;
            //sm = SMMode.AdLib;
        //}
        //else
        //{
        //    sd = SDMode.PC;
        //    sm = SMMode.Off;
        //}

        // always true
        //if (Program.SoundBlasterPresent)
            //sds = SDSMode.SoundBlaster;
        //else
        //    sds = SDSMode.Off;

        if (inputManager.IsMousePresent())
            Program.mouseenabled = true;

        if (inputManager.JoyPresent())
            Program.joystickenabled = true;

        Program.viewsize = 19;
        Program.mouseadjustment = 5;

        //audioManager.SetMusicMode(sm);
        //audioManager.SetSoundMode(sd);
        //audioManager.SetDigiDevice(sds);
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

        bw.Write((byte)0);//audioManager.SoundMode);
        bw.Write((byte)0);//audioManager.MusicMode);
        bw.Write((byte)0);//audioManager.DigiMode);

        bw.Write(Program.mouseenabled);
        bw.Write(Program.joystickenabled);
        bw.Write((byte)0); // joypad placeholder
        bw.Write((byte)0); // joystick-progressive placeholder
        bw.Write((int)0); // joystick port placeholder

        for (int i = 0; i < Program.dirscan.Length; i++)
            bw.Write((int)Program.dirscan[i]);

        for (int i = 0; i < Program.buttonscan.Length; i++)
            bw.Write((int)Program.buttonscan[i]);

        for (int i = 0; i < Program.buttonmouse.Length; i++)
            bw.Write((int)Program.buttonmouse[i]);

        for (int i = 0; i < Program.buttonjoy.Length; i++)
            bw.Write((int)Program.buttonjoy[i]);

        bw.Write(Program.viewsize);
        bw.Write(Program.mouseadjustment);
        bw.Write(consoleManager.PauseWhenOpen);
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
