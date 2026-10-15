namespace PFWolf;

internal partial class Program
{
    // Light actors give off (Rendering/ActorLights.cs)
    static ActorLights actorLights = null!;     // made in InitServices

    static ActorLights MakeActorLights() => new(_mapManager, doors, lighting ??= MakeLighting(), session);

    static Dictionary<string, byte[]> colortints => actorLights.colortints;
    static bool haslights => actorLights.haslights;

    private static void RegisterActorLightActions() => (actorLights ??= MakeActorLights()).RegisterActorLightActions();
    static IEnumerable<string> DescribeActorLights() => actorLights.DescribeActorLights();
    static void TickActorLights(uint tics) => actorLights.TickActorLights(tics);
    static void CastActorLights() => actorLights.CastActorLights();
}
