using Microsoft.Extensions.DependencyInjection;
using PFWolf.DependencyInjection;

namespace PFWolf.Startup;

/// <summary>The managers the game is built from, one of each</summary>
internal static class GameServices
{
    internal static IServiceCollection Register(IServiceCollection services, GameSession session)
    {
        services.AddTransient(typeof(Lazy<>), typeof(Lazier<>));
        services.AddSingleton(session);
        services.AddSingleton<GameEngineManager>();
        services.AddSingleton<VideoManager>();
        services.AddSingleton<AudioManager>();
        services.AddSingleton<InputManager>();
        services.AddSingleton<GraphicManager>();
        services.AddSingleton<FontManager>();
        services.AddSingleton<MapManager>();
        services.AddSingleton<AssetManager>();
        services.AddSingleton<InventoryManager>();
        services.AddSingleton<ConsoleManager>();
        services.AddSingleton<AutomapManager>();
        services.AddSingleton<HudMessageManager>();
        services.AddSingleton<FactionManager>();
        services.AddSingleton<TranslationManager>();
        return services;
    }

    internal static ServiceProvider Build(GameSession session) => Register(new ServiceCollection(), session).BuildServiceProvider();
}
