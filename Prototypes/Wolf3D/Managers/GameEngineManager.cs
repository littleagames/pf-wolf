using SDL2;
using Wolf3D.Configuration;

namespace Wolf3D.Managers;


internal enum GameType
{
    Wolf3D,
    SpearOfDestiny
}
internal class GameEngineManager
{
    private readonly VideoManager videoManager;
    private readonly InputManager inputManager;

    public GameEngineManager(
        VideoManager videoManager,
        InputManager inputManager)
    {
        this.videoManager = videoManager;
        this.inputManager = inputManager;
        InputManager.Quit += Quit;
        InputManager.Pause += SetPaused;
    }

    /// <summary>
    /// Console parameters from commandline or shortcuts. Will be used to override config values
    /// </summary>
    public GameParams GameParams { get; private set; }
    
    public ConfigDirectories ConfigDirectories { get; private set; }

    public GameType GameType { get; set; }


    internal bool Paused;

    internal const int CONFIG_DIR_SIZE = 256;
    internal string configdir = string.Empty;
    internal string configname = "config.cfg";

    public void Init(GameParams args)
    {
        ReadConfigData(args);
        GameType = GameType.Wolf3D; // TODO: Pull from config or PK3 in future
    }

    internal void ReadConfig()
    {
        SDMode sd;
        SMMode sm;
        SDSMode sds;
        string configpath;

        if (!string.IsNullOrEmpty(configdir))
            configpath = $"{configdir}/{configname}";
        else
            configpath = configname;

        if (!File.Exists(configpath))
        {
            SetDefaultConfig();
            return;
        }
        try
        {
            using (FileStream fs = File.OpenRead(configpath))
            using (BinaryReader br = new BinaryReader(fs))
            {
                //
                // valid config file
                //
                ushort tmp = br.ReadUInt16();
                if (tmp != 0xfefa)
                {
                    SetDefaultConfig();
                    return;
                }

                foreach (var s in Program.Scores)
                    s.Read(br);

                sd = (SDMode)br.ReadByte();
                sm = (SMMode)br.ReadByte();
                sds = (SDSMode)br.ReadByte();

                Program.mouseenabled = (br.ReadByte() != 0) ? true : false;
                Program.joystickenabled = (br.ReadByte() != 0) ? true : false;
                _ = br.ReadByte(); // joypad enabled placeholder
                _ = br.ReadByte(); // joystick progressive placeholder
                _ = br.ReadInt32(); // joystick port placeholder

                for (int i = 0; i < Program.dirscan.Length; i++)
                    Program.dirscan[i] = (ScanCodes)br.ReadInt32();
                for (int i = 0; i < Program.buttonscan.Length; i++)
                    Program.buttonscan[i] = (ScanCodes)br.ReadInt32();
                for (int i = 0; i < Program.buttonmouse.Length; i++)
                    Program.buttonmouse[i] = (buttontypes)br.ReadInt32();
                for (int i = 0; i < Program.buttonjoy.Length; i++)
                    Program.buttonjoy[i] = (buttontypes)br.ReadInt32();

                Program.viewsize = br.ReadInt32();
                Program.mouseadjustment = br.ReadInt32();

                if ((sd == SDMode.AdLib || sm == SMMode.AdLib) && !Program.AdLibPresent
                    && !Program.SoundBlasterPresent)
                {
                    sd = SDMode.PC;
                    sm = SMMode.Off;
                }

                if ((sds == SDSMode.SoundBlaster) && !Program.SoundBlasterPresent)
                    sds = SDSMode.Off;

                // make sure values are correct
                if (Program.mouseenabled) Program.mouseenabled = true; // true
                if (Program.joystickenabled) Program.joystickenabled = true; // true

                if (!inputManager.IsMousePresent())
                    Program.mouseenabled = false; // false
                if (!inputManager.JoyPresent())
                    Program.joystickenabled = false; // false

                if (Program.mouseadjustment < 0) Program.mouseadjustment = 0;
                else if (Program.mouseadjustment > 9) Program.mouseadjustment = 9;

                if (Program.viewsize < 4) Program.viewsize = 4;
                else if (Program.viewsize > 21) Program.viewsize = 21;

                // Set "Read This" back to standard active
                throw new NotImplementedException("Menu system should listen to game events");
                //Program.MainMenu[6].active = 1;
                //Program.MainItems.curpos = 0;


                Program.SD_SetMusicMode(sm);
                Program.SD_SetSoundMode(sd);
                Program.SD_SetDigiDevice(sds);
            }
        }
        catch (Exception e)
        {
            File.Delete(configpath);
            SetDefaultConfig();
        }
    }

    private void SetDefaultConfig()
    {
        SDMode sd;
        SMMode sm;
        SDSMode sds;
        if (Program.SoundBlasterPresent || Program.AdLibPresent)
        {
            sd = SDMode.AdLib;
            sm = SMMode.AdLib;
        }
        else
        {
            sd = SDMode.PC;
            sm = SMMode.Off;
        }

        if (Program.SoundBlasterPresent)
            sds = SDSMode.SoundBlaster;
        else
            sds = SDSMode.Off;

        if (inputManager.IsMousePresent())
            Program.mouseenabled = true;

        if (inputManager.JoyPresent())
            Program.joystickenabled = true;

        Program.viewsize = 19;
        Program.mouseadjustment = 5;

        Program.SD_SetMusicMode(sm);
        Program.SD_SetSoundMode(sd);
        Program.SD_SetDigiDevice(sds);
    }

    internal void WriteConfig()
    {
        string configpath = string.Empty;

        if (configdir != string.Empty)
            configpath = $"{configdir}/{configname}";
        else
            configpath = configname;

        using (FileStream fs = File.OpenWrite(configpath))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            ushort tmp = 0xfefa;
            bw.Write(tmp);
            foreach (var s in Program.Scores)
                s.Write(bw);

            bw.Write((byte)Program.SoundMode);
            bw.Write((byte)Program.MusicMode);
            bw.Write((byte)Program.DigiMode);

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
        }
    }
    public void ReadConfigData(GameParams args)
    {
        ConfigDirectories = ConfigDirectories.Default();

        // If args is set, check that first
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
    public void Error(string errorStr)
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

        //US_Shutdown(); // This line is completely useless...
        //SD_Shutdown();
        //PM_Shutdown();
        //IN_Shutdown();
        //CA_Shutdown();
    }
}
