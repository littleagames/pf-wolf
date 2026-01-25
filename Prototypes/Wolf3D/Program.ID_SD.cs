using SDL2;

namespace Wolf3D;

internal enum SDMode
{
    Off,
    PC,
    AdLib
}

internal enum SMMode
{
    Off,
    AdLib
}

internal enum SDSMode
{
    Off,
    PC,
    SoundBlaster
}

internal partial class Program
{
    internal const int TickBase = 70;     // 70Hz per tick - used as a base for timer 0

    // Global variables
    internal static bool AdLibPresent,
        SoundBlasterPresent, SBProPresent,
        SoundPositioned;
    internal static byte SoundMode;
    internal static byte MusicMode;
    internal static byte DigiMode;

    static int[] DigiMap = new int[(int)soundnames.LASTSOUND];
    static int[] DigiChannel = new int[STARTMUSIC - STARTDIGISOUNDS];

    // Internal variables
    private static bool SD_Started;
    private static bool nextsoundpos;
    private static int SoundNumber;
    private static int DigiNumber;
    private static ushort SoundPriority;
    private static ushort DigiPriority;
    private static int LeftPosition;
    private static int RightPosition;
    private static ushort NumDigi;
    // private static digiinfo[] DigiList
    private static bool DigiPlaying;

    // PC Sound variables


    // Global variables
    internal static uint GetTimeCount() => ((SDL.SDL_GetTicks() * 7) / 100);

    internal static void SD_Startup()
    {

        if (SD_Started)
            return;

        SD_Started = true;
    }

    internal static void SD_PlaySound(int sound)
    {

    }

    internal static void SD_StopSound()
    {
        // TODO:
    }

    internal static void SD_Shutdown()
    {
        if (!SD_Started)
            return;
        SD_Started = false;
    }

    internal static void SD_PrepareSound(int which)
    {

    }

    internal static void SD_MusicOn()
    {
        
    }

    internal static void SD_MusicOff()
    {
    }

    static bool SD_SetMusicMode(byte mode)
    {
        bool result = false;

        return result;
    }
    static bool SD_SetSoundMode(byte mode)
    {
        bool result = false;

        return result;
    }
    static void SD_SetDigiDevice(byte mode)
    {
    }

    static void SD_StopDigitized()
    {
        // TODO:
    }

    static int SD_SoundPlaying()
    {
        byte result = 0;

        switch (SoundMode)
        {
            //case SDMode.PC:
            //    result = pcSound ? true : false;
            //    break;
            //case SDMode.AdLib:
            //    result = alSound ? true : false;
            //    break;

            default:
                break;
        }

        if (result > 0)
            return 1; // sound index being played
        else
            return 0;
    }

    static void SD_WaitSoundDone()
    {
        while (SD_SoundPlaying() != 0)
        {
            SDL.SDL_Delay(5);
        }
    }
}
