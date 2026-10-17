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

    private static void RegisterZoneLightActions() => (lighting ??= MakeLighting()).RegisterZoneLightActions();
}
