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
    /// <summary>
    /// Console parameters from commandline or shortcuts. Will be used to override config values
    /// </summary>
    public GameParams GameParams { get; private set; }
    
    public ConfigDirectories ConfigDirectories { get; private set; }

    public GameType GameType { get; set; }


    public void Init(GameParams args)
    {
        ReadConfigData(args);
        GameType = GameType.Wolf3D; // TODO: Pull from config or PK3 in future
    }

    public void ReadConfigData(GameParams args)
    {
        ConfigDirectories = ConfigDirectories.Default();

        // If args is set, check that first
    }


    internal static uint GetTimeCount() => ((SDL.SDL_GetTicks() * 7) / 100);

    internal static void Delay(int wolfticks)
    {
        if (wolfticks > 0)
            SDL.SDL_Delay((uint)((wolfticks * 100) / 7));
    }

    internal static void WaitVBL(uint a) => SDL.SDL_Delay((a) * 8);
}
