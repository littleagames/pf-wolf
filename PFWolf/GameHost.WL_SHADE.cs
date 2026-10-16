using PFWolf.Assets;

namespace PFWolf;

internal static partial class GameHost
{
    // The level's shading, light grid and light zones (Rendering/Lighting.cs)
    static Lighting lighting = null!;       // made in InitServices

    // The level-editing console commands (Commands/LevelEditCommands.cs)
    static LevelEditCommands levelEditCommands = null!;     // made in InitServices
    static FactionCommands factionCommands = null!;     // (Commands/FactionCommands.cs)

    static Lighting MakeLighting() =>
        new(_gameEngineManager, _videoManager, _mapManager,
            new LightingHooks(() => heightnumerator, () => colortints.Clear(), TickActorLights));

    internal const int LIGHTLEVELS = Lighting.LIGHTLEVELS;
    internal const int FADESTEPS = Lighting.FADESTEPS;
    internal const int LIGHTCELLSHIFT = Lighting.LIGHTCELLSHIFT;
    internal const int LIGHTCELLS = Lighting.LIGHTCELLS;
    const int CELLGLOBALSHIFT = Lighting.CELLGLOBALSHIFT;
    const int MAXLIGHTLEVEL = Lighting.MAXLIGHTLEVEL;
    internal const int GRIDSHIFT = Lighting.GRIDSHIFT;
    internal const int GRIDSIZE = Lighting.GRIDSIZE;

    static (byte, byte, byte) NoTint => Lighting.NoTint;
    static List<(byte R, byte G, byte B)> tints => lighting.tints;

    static (byte R, byte G, byte B) ParseRgb(string? color, (byte, byte, byte) fallback, string what) => Lighting.ParseRgb(color, fallback, what);
    static (int X, int Y) TileCenter(int x, int y) => Lighting.TileCenter(x, y);

    private static void RegisterZoneLightActions() => (lighting ??= MakeLighting()).RegisterZoneLightActions();
}
