using SDL2;
using System.ComponentModel;
using System.Reflection;
using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.Loaders;

namespace PFWolf.Managers;


internal enum GameType
{
    [Description("wolf3d")]
    Wolf3D,
    [Description("spear")]
    SpearOfDestiny,
    [Description("blake")]
    BlakeStone,
    [Description("planetstrike")]
    PlanetStrike,
    [Description("wolf3d-shareware")]
    WolfShareware,
    [Description("wolf3d-apogee")]
    WolfApogee,
    [Description("spear-demo")]
    SpearDemo
}

internal class GameEngineManager
{
    private readonly VideoManager videoManager;
    private readonly InputManager inputManager;
    private readonly AudioManager audioManager;
    private readonly Lazy<AssetManager> assetManager;
    private readonly ConsoleManager consoleManager;
    private readonly AutomapManager automapManager;
    private readonly HudMessageManager hudMessageManager;

    public GameEngineManager(
        VideoManager videoManager,
        InputManager inputManager,
        AudioManager audioManager,
        Lazy<AssetManager> assetManager,
        ConsoleManager consoleManager,
        AutomapManager automapManager,
        HudMessageManager hudMessageManager)
    {
        this.videoManager = videoManager;
        this.inputManager = inputManager;
        this.consoleManager = consoleManager;
        this.automapManager = automapManager;
        this.hudMessageManager = hudMessageManager;
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
        GameType = PickGameType(ParseGameType(args.Game));
        ReadConfigData(args);
    }

    /// <summary>
    /// What's played when no game is asked for, or the one asked for has no data files here: the
    /// first of these whose data files are all in the game folder
    /// </summary>
    private static readonly GameType[] FallbackOrder =
        [GameType.Wolf3D, GameType.WolfShareware, GameType.SpearOfDestiny, GameType.SpearDemo, GameType.BlakeStone, GameType.PlanetStrike];

    /// <summary>
    /// The game to run: the one --game asks for when its data files are here, else the first game
    /// whose files are (Wolf3D's shareware first, so asking for Wolf3D with only the shareware's
    /// files plays that; asking for Spear with only its demo's plays the demo). With none, the one
    /// asked for (or Wolf3D), which then says what's missing.
    /// A game's files can be another release's under the same names (Apogee's Wolf3D files are
    /// named as the GT ones wolf3d is for): then that release, which strict-md5 marks, is played.
    /// </summary>
    private static GameType PickGameType(GameType? requested)
    {
        GamePackInfoAsset? gamePackInfo;
        try
        {
            gamePackInfo = PfWolfPk3Loader.ReadGamePackInfo(new Pk3AssetSource(AssetManager.BasePk3FileName));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            gamePackInfo = null;        // loading the assets reports it
        }

        if (gamePackInfo == null)
            return requested ?? GameType.Wolf3D;

        bool IsStrict(GameType type) => gamePackInfo.GetGamePack(GetReleaseId(type)).FilePack?.StrictMd5 == true;

        bool HasDataFiles(GameType type)
            => gamePackInfo.GamePacks.ContainsKey(GetReleaseId(type))
               && gamePackInfo.FindMissingDataFiles(GetReleaseId(type)).Count == 0
               && (!IsStrict(type) || gamePackInfo.FindMismatchedDataFiles(GetReleaseId(type)).Count == 0);

        // The game the files here are for, when they're the given game's by name: the game itself,
        // unless they're another version, and a strict release with the same file names fits them
        GameType? Playable(GameType type)
        {
            if (!HasDataFiles(type))
                return null;
            if (IsStrict(type) || gamePackInfo.FindMismatchedDataFiles(GetReleaseId(type)).Count == 0)
                return type;

            foreach (var other in Enum.GetValues<GameType>())
            {
                if (other != type && gamePackInfo.GamePacks.ContainsKey(GetReleaseId(other)) && IsStrict(other)
                    && gamePackInfo.HasSameDataFileNames(GetReleaseId(type), GetReleaseId(other)) && HasDataFiles(other))
                    return other;
            }
            return type;
        }

        void NoteSwitch(GameType asked, GameType played, string reason)
        {
            if (played != asked)
                Console.WriteLine($"Running {GetGamePackId(played)}: {reason}");
        }

        string Description(GameType type)
        {
            var gamePack = gamePackInfo.GetGamePack(GetReleaseId(type));
            return gamePack.FilePack?.Description ?? gamePack.Title ?? GetGamePackId(type);
        }

        if (requested is { } asked && Playable(asked) is { } game)
        {
            NoteSwitch(asked, game, $"the data files here are {Description(game)}'s.");
            return game;
        }

        // Asked for Wolf3D (or for nothing): its shareware is the closest thing; for Spear, its demo
        var candidates = requested switch
        {
            null or GameType.Wolf3D => FallbackOrder,
            GameType.SpearOfDestiny => [GameType.SpearDemo],
            _ => [],
        };
        foreach (var type in candidates)
        {
            if (Playable(type) is not { } found)
                continue;
            NoteSwitch(requested ?? GameType.Wolf3D, found, requested == null
                ? $"its data files are the ones here ({Description(found)}; --game picks another)."
                : $"{GetGamePackId(requested.Value)}'s data files aren't here.");
            return found;
        }

        return requested ?? GameType.Wolf3D;
    }

    /// <summary>
    /// The game --game names by its pack id ("spear"); null when unset or unknown
    /// </summary>
    private static GameType? ParseGameType(string gamePackId)
    {
        if (string.IsNullOrWhiteSpace(gamePackId))
            return null;

        foreach (var type in Enum.GetValues<GameType>())
        {
            if (GetGamePackId(type).Equals(gamePackId.Trim(), StringComparison.OrdinalIgnoreCase))
                return type;
        }

        Console.WriteLine($"Unknown --game '{gamePackId}' (expected {string.Join(", ", KnownGamePackIds)}); picking by the data files here.");
        return null;
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
    public string GameReleaseId => GetReleaseId(GameType);

    private static string GetReleaseId(GameType type) => type switch
    {
        GameType.SpearOfDestiny => "spear",
        GameType.BlakeStone => "blake-aog",
        GameType.PlanetStrike => "blake-ps",
        GameType.WolfShareware => "wolf3d-shareware",
        GameType.WolfApogee => "wolf3d-apogee",
        GameType.SpearDemo => "spear-demo",
        _ => "wolf3d",
    };

    /// <summary>
    /// Folder under %APPDATA%\PFWolf holding this game's settings, high scores and saves,
    /// so games don't share them. Wolf3D keeps the folder it has always used.
    /// </summary>
    private string GameDataFolderName => GameType switch
    {
        GameType.SpearOfDestiny => "SpearOfDestiny",
        GameType.BlakeStone => "BlakeStone",
        GameType.PlanetStrike => "PlanetStrike",
        GameType.WolfShareware => "Wolfenstein3DShareware",
        GameType.SpearDemo => "SpearOfDestinyDemo",
        _ => "Wolfenstein3D",       // Apogee's release too: the same game, so the same settings
    };

    /// <summary>
    /// Every game pack name the engine knows about
    /// </summary>
    public static IEnumerable<string> KnownGamePackIds => Enum.GetValues<GameType>().Select(GetGamePackId);

    private static string GetGamePackId(GameType type)
        => typeof(GameType).GetField(type.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description
           ?? type.ToString().ToLowerInvariant();

    /// <summary>
    /// The engine's version from the build (Version in Wolf3D.csproj): "0.1", followed by the
    /// commit it was built from when the build knows it, as in "0.1 (1a2b3c4)"
    /// </summary>
    internal static string EngineVersion { get; } = ReadEngineVersion();

    private static string ReadEngineVersion()
    {
        var assembly = typeof(GameEngineManager).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "?";

        // The SDK adds "+<commit hash>" when it builds from a git checkout
        var plus = version.IndexOf('+');
        if (plus < 0)
            return version;

        var commit = version[(plus + 1)..];
        return commit.Length == 0 ? version[..plus] : $"{version[..plus]} ({commit[..Math.Min(7, commit.Length)]})";
    }

    public GameInfoAsset GetGameInfo()
    {
        var gameInfo = assetManager.Value.FindInGamePack<GameInfoAsset>("game-info");
        if (gameInfo == null)
            throw new Exception("Game info not found");
        return gameInfo;
    }

    /// <summary>
    /// Set once config.cfg, controls.cfg and binds.cfg have been read at startup; WriteConfig
    /// does nothing before then.
    /// </summary>
    internal bool SettingsLoaded { get; set; }

    internal const string ControlsFileName = "controls.cfg";
    internal const string BindsFileName = "binds.cfg";
    internal const string AutoexecFileName = "autoexec.cfg";

    /// <summary>Where a file of the given name lives in the config directory (config.cfg, controls.cfg, binds.cfg, autoexec.cfg).</summary>
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
        public VideoSettings? Video;
        public bool? AutoSave;
        public HudMessagesSetting? HudMessages;
        public bool? AutomapStats;
        public HighScore[]? ScoreTable;
    }

    /// <summary>The video settings appended after the score table (see ReadMoreVideoSettings)</summary>
    [Flags]
    private enum MoreVideoFlags : byte
    {
        None = 0,
        MatchWindow = 1,
    }

    /// <summary>The `msg_enabled` setting, as saved in config.cfg</summary>
    private enum HudMessagesSetting : byte
    {
        Off = 0,
        On = 1,
        GameDefault = 2,
    }

    /// <summary>The Video menu's on/off settings, as saved in config.cfg.</summary>
    [Flags]
    private enum VideoFlags : byte
    {
        None = 0,
        Fullscreen = 1,
        VSync = 2,
        AspectCorrect = 4,
    }

    // Limits on the saved sizes, so a damaged config can't ask for an impossible mode
    private const int MaxRenderScale = 8;
    private const int MaxWindowSize = 16384;
    private const int MaxUiScale = 32;

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

    // The controls' place in the original fixed layout: the movement keys, a key per button
    // (the automap key was appended at the end later), and a button for each mouse and joystick
    // button. They're in controls.cfg now; these are only read from an older config.cfg.
    private const int LayoutDirCount = 4;
    private const int LayoutButtonCount = (int)buttontypes.bt_automap;
    private const int LayoutMouseCount = 4;
    private const int LayoutJoyCount = 32;

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

        var savedScores = config.ScoreTable ?? config.Scores;
        Array.Copy(savedScores, Program.Scores, Math.Min(savedScores.Length, Program.Scores.Length));

        // The controls moved to controls.cfg, which the console runs at startup; config.cfg now
        // only has blanks where they were. A config from before that still has them: take them
        // over this once, and controls.cfg holds them from the next save on.
        bool hasLegacyControls = config.DirScan.Any(key => key != ScanCodes.sc_None);
        bool legacyConfig = hasLegacyControls && !File.Exists(GetConfigFilePath(ControlsFileName));
        if (legacyConfig)
            Program.controls.ImportLegacy(config.DirScan, config.ButtonScan, config.ButtonMouse, config.ButtonJoy,
                config.AutomapKey, config.AutomapKeys);

        // Devices that were there last time may not be now. A controller can be plugged in
        // later, so its setting is kept as it was either way. Before that, the joystick was
        // saved as off whenever none was plugged in, so an old config's "off" doesn't count.
        Program.mouseenabled = config.MouseEnabled && inputManager.IsMousePresent();
        Program.joystickenabled = config.JoystickEnabled || legacyConfig;
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
        if (config.AutoSave is bool autoSave)
            Program.autosaveEnabled = autoSave;
        if (config.HudMessages is HudMessagesSetting.Off or HudMessagesSetting.On)
            hudMessageManager.EnabledSetting = config.HudMessages == HudMessagesSetting.On;
        if (config.AutomapStats is bool stats)
            automapManager.ShowStats = stats;

        // Set "Read This" back to standard active
        Program.FindMenuItem(Program.MainMenu, "readthis")?.active = 1;
        Program.MainItems.curpos = 0;
    }

    private static ConfigData ParseConfig(BinaryReader br)
    {
        var scores = new HighScore[Program.LegacyScoreCount];
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
            DirScan = new ScanCodes[LayoutDirCount],
            ButtonScan = new ScanCodes[LayoutButtonCount],
            ButtonMouse = new buttontypes[LayoutMouseCount],
            ButtonJoy = new buttontypes[LayoutJoyCount],
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
        if (stream.Position < stream.Length)
            config.Video = ReadVideoSettings(br);
        if (stream.Position < stream.Length)
            config.AutoSave = br.ReadByte() != 0;
        if (stream.Position < stream.Length)
            config.HudMessages = (HudMessagesSetting)br.ReadByte();
        if (stream.Position < stream.Length)
            config.AutomapStats = br.ReadByte() != 0;
        if (stream.Position < stream.Length)
        {
            // The whole high score table, with each one's ratio: games with more than the original block's 7
            int count = br.ReadByte();
            config.ScoreTable = new HighScore[count];
            for (int i = 0; i < count; i++)
            {
                config.ScoreTable[i] = new HighScore();
                config.ScoreTable[i].Read(br);
                config.ScoreTable[i].ratio = br.ReadUInt16();
            }
        }
        if (stream.Position < stream.Length && config.Video != null)
            config.Video = ReadMoreVideoSettings(br, config.Video);

        return config;
    }

    /// <summary>
    /// The video settings added after the first video block, which sits mid-file: whether the
    /// render size matches the window, a render size of its own (0x0 for none), and the UI
    /// scale in hundredths (0 for auto), so a fractional one needs no new layout.
    /// </summary>
    private static VideoSettings ReadMoreVideoSettings(BinaryReader br, VideoSettings video)
    {
        var flags = (MoreVideoFlags)br.ReadByte();
        int width = br.ReadUInt16(), height = br.ReadUInt16();
        int uiScale = br.ReadUInt16();

        bool hasSize = width >= VideoSettings.BaseWidth && width <= VideoSettings.MaxRenderWidth
            && height >= VideoSettings.BaseHeight && height <= VideoSettings.MaxRenderHeight;
        return video with
        {
            MatchWindow = flags.HasFlag(MoreVideoFlags.MatchWindow),
            RenderSize = hasSize ? (width, height) : null,
            UiScale = Math.Clamp(uiScale / 100.0, 0, MaxUiScale),
        };
    }

    private static void WriteMoreVideoSettings(BinaryWriter bw, VideoSettings video)
    {
        bw.Write((byte)(video.MatchWindow ? MoreVideoFlags.MatchWindow : MoreVideoFlags.None));
        // An Auto resolution's size is worked out afresh each time, so it isn't kept
        var size = video.MatchWindow ? null : video.RenderSize;
        bw.Write((ushort)(size?.Width ?? 0));
        bw.Write((ushort)(size?.Height ?? 0));
        bw.Write((ushort)Math.Round(Math.Clamp(video.UiScale, 0, MaxUiScale) * 100));
    }

    private static VideoSettings ReadVideoSettings(BinaryReader br)
    {
        var flags = (VideoFlags)br.ReadByte();
        int renderScale = br.ReadByte();
        int windowWidth = br.ReadUInt16();
        int windowHeight = br.ReadUInt16();
        var filter = (ScaleFilter)br.ReadByte();

        return new VideoSettings
        {
            Fullscreen = flags.HasFlag(VideoFlags.Fullscreen),
            VSync = flags.HasFlag(VideoFlags.VSync),
            AspectCorrect = flags.HasFlag(VideoFlags.AspectCorrect),
            RenderScale = Math.Clamp(renderScale, 1, MaxRenderScale),
            WindowWidth = Math.Clamp(windowWidth, VideoSettings.BaseWidth, MaxWindowSize),
            WindowHeight = Math.Clamp(windowHeight, VideoSettings.BaseHeight, MaxWindowSize),
            Filter = Enum.IsDefined(filter) ? filter : ScaleFilter.Nearest,
        };
    }

    private static void WriteVideoSettings(BinaryWriter bw, VideoSettings video)
    {
        var flags = VideoFlags.None;
        if (video.Fullscreen)
            flags |= VideoFlags.Fullscreen;
        if (video.VSync)
            flags |= VideoFlags.VSync;
        if (video.AspectCorrect)
            flags |= VideoFlags.AspectCorrect;

        bw.Write((byte)flags);
        bw.Write((byte)video.RenderScale);
        bw.Write((ushort)video.WindowWidth);
        bw.Write((ushort)video.WindowHeight);
        bw.Write((byte)video.Filter);
    }

    /// <summary>
    /// The video mode to open the window in: config.cfg's, or the default without one, then
    /// changed by any --fullscreen, --windowed, --res or --scale given. Read on its own, ahead
    /// of the rest of the config, since the window has to exist before anything else loads.
    /// A mode picked on the command line is saved like one picked in the menu.
    /// </summary>
    internal VideoSettings ReadVideoConfig()
    {
        var video = new VideoSettings();
        string configpath = GetConfigFilePath(ConfigFileName);

        if (File.Exists(configpath))
        {
            try
            {
                using var br = new BinaryReader(File.OpenRead(configpath));
                if (br.ReadUInt16() == ConfigSignature)
                    video = ParseConfig(br).Video ?? video;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // ReadConfig reports this when it reads the rest
            }
        }

        return ApplyVideoParams(video, GameParams);
    }

    private static VideoSettings ApplyVideoParams(VideoSettings video, GameParams args)
    {
        if (args.Fullscreen && args.Windowed)
            Console.WriteLine("Both --fullscreen and --windowed given; using --windowed.");

        if (args.Fullscreen)
            video = video with { Fullscreen = true };
        if (args.Windowed)
            video = video with { Fullscreen = false };

        if (!string.IsNullOrWhiteSpace(args.Resolution))
        {
            var parts = args.Resolution.ToLowerInvariant().Split('x');
            if (parts.Length == 2
                && int.TryParse(parts[0], out int width) && width >= VideoSettings.BaseWidth && width <= MaxWindowSize
                && int.TryParse(parts[1], out int height) && height >= VideoSettings.BaseHeight && height <= MaxWindowSize)
                video = video with { WindowWidth = width, WindowHeight = height };
            else
                Console.WriteLine($"Ignoring --res '{args.Resolution}': expected a window size like 960x600, at least 320x200.");
        }

        if (args.Scale is int scale)
        {
            if (scale >= 1 && scale <= MaxRenderScale)
                video = video with { RenderScale = scale, RenderSize = null, MatchWindow = false };
            else
                Console.WriteLine($"Ignoring --scale {scale}: expected 1 to {MaxRenderScale}.");
        }

        return video;
    }

    private void SetDefaultConfig()
    {
        // Every sound device and music start switched on (AudioManager's defaults).
        if (inputManager.IsMousePresent())
            Program.mouseenabled = true;

        Program.joystickenabled = true;     // used as soon as one is plugged in

        Program.viewsize = 19;
        Program.mouseadjustment = 5;
    }

    /// <summary>
    /// Writes config.cfg, controls.cfg and binds.cfg. Called on exit, and whenever the settings
    /// or high scores change, so they survive a crash. A failure is logged, not thrown, since
    /// this also runs on the way out of the game.
    /// </summary>
    internal void WriteConfig()
    {
        // Until the settings are all loaded, what's in memory is partly defaults: writing it (as
        // quitting at the signon screen would) would replace the saved controls with them.
        if (!SettingsLoaded)
            return;

        try
        {
            // The controls and binds are console commands, so they're saved as scripts the
            // console runs at startup. Binds are written even when empty so that unbinding
            // everything sticks; every control is written, bound or not, for the same reason.
            ReplaceFile(GetConfigFilePath(ControlsFileName), stream =>
            {
                using var writer = new StreamWriter(stream);
                writer.WriteLine("// Written by the game; use autoexec.cfg for your own commands.");
                foreach (var command in Program.controls.GetCommands().Concat(Program.GetControllerSettingCommands()))
                    writer.WriteLine(command);
            });

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
        for (int i = 0; i < Program.LegacyScoreCount; i++)
            (i < Program.Scores.Length ? Program.Scores[i] : new HighScore()).Write(bw);

        bw.Write((byte)0); // sound mode placeholder
        bw.Write((byte)0); // music mode placeholder
        bw.Write((byte)0); // digitized mode placeholder

        bw.Write(Program.mouseenabled);
        bw.Write(Program.joystickenabled);
        bw.Write((byte)0); // joypad placeholder
        bw.Write((byte)0); // joystick-progressive placeholder
        bw.Write((int)0); // joystick port placeholder

        // The controls are in controls.cfg: blanks where the old layout had them (see ReadConfig)
        for (int i = 0; i < LayoutDirCount + LayoutButtonCount + LayoutMouseCount + LayoutJoyCount; i++)
            bw.Write((int)0);

        bw.Write(Program.viewsize);
        bw.Write(Program.mouseadjustment);
        bw.Write(consoleManager.PauseWhenOpen);
        bw.Write((int)ScanCodes.sc_None);   // automap key placeholder
        bw.Write((byte)automapManager.Style);
        bw.Write(automapManager.Overlay);
        bw.Write(automapManager.ShowGrid);
        bw.Write(0);                        // no automap keys: they're in controls.cfg

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
        WriteVideoSettings(bw, videoManager.Settings);
        bw.Write((byte)(Program.autosaveEnabled ? 1 : 0));
        bw.Write((byte)(hudMessageManager.EnabledSetting switch
        {
            true => HudMessagesSetting.On,
            false => HudMessagesSetting.Off,
            null => HudMessagesSetting.GameDefault,
        }));
        bw.Write(automapManager.ShowStats);

        bw.Write((byte)Program.Scores.Length);
        foreach (var s in Program.Scores)
        {
            s.Write(bw);
            bw.Write(s.ratio);
        }

        WriteMoreVideoSettings(bw, videoManager.Settings);
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
    /// <summary>
    /// Quits as normal, saving the settings, and starts the game again: the same game pack and
    /// folders, with the mods mods.cfg names. Mods, video options and --exec commands given on
    /// the command line aren't passed on (the video options were saved as settings).
    /// </summary>
    public void Restart()
    {
        var args = new List<string> { "--game", GamePackId };
        if (!string.IsNullOrWhiteSpace(GameParams.ConfigDir))
            args.AddRange(["--configdir", GameParams.ConfigDir]);
        if (!string.IsNullOrWhiteSpace(GameParams.SavesDir))
            args.AddRange(["--savedir", GameParams.SavesDir]);

        WriteConfig();
        Shutdown();

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
            {
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = false,
            };
            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);
            System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception e)
        {
            Error($"The game couldn't be started again: {e.Message}");
        }

        Environment.Exit(0);
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
