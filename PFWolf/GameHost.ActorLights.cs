namespace PFWolf;

internal static partial class GameHost
{
    // Light actors give off (Rendering/ActorLights.cs)
    static ActorLights actorLights = null!;     // made in InitServices

    static ActorLights MakeActorLights() => new(_mapManager, doors, lighting ??= MakeLighting(), session);

    static Dictionary<string, byte[]> colortints => actorLights.colortints;

    private static void RegisterActorLightActions() => (actorLights ??= MakeActorLights()).RegisterActorLightActions();
    static void TickActorLights(uint tics) => actorLights.TickActorLights(tics);
}
