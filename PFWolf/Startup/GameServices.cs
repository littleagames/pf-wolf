using Microsoft.Extensions.DependencyInjection;
using PFWolf.Configuration;
using PFWolf.DependencyInjection;

namespace PFWolf.Startup;

/// <summary>The managers the game is built from, one of each</summary>
internal static class GameServices
{
    /// <param name="actorWorld">
    /// What the actors use of the game (Entities/Actors/ActorWorld.cs), which GameHost builds from
    /// the managers here and its own classes once they exist: the map takes it lazily, as it needs it
    /// only when actors are made (on a level), well after that
    /// </param>
    internal static IServiceCollection Register(IServiceCollection services, GameSession session, GameSettings settings,
        Func<Entities.Actors.ActorWorld>? actorWorld = null)
    {
        services.AddTransient(typeof(Lazy<>), typeof(Lazier<>));
        services.AddSingleton(session);
        services.AddSingleton(settings);
        services.AddSingleton(_ => actorWorld?.Invoke()
            ?? throw new InvalidOperationException("The actor world is asked for before the game has built it"));
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

    internal static ServiceProvider Build(GameSession session, GameSettings settings, Func<Entities.Actors.ActorWorld> actorWorld) =>
        Register(new ServiceCollection(), session, settings, actorWorld).BuildServiceProvider();
}
