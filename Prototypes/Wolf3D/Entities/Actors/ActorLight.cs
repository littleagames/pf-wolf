using System.Globalization;

namespace Wolf3D.Entities.Actors;

/// <summary>
/// An actor class's light, from its actordefs properties (inherited like any other):
///   light.intensity   light added at the actor, 0-255 (as zone light: 255 is full); 0 or left
///                     out: none, unless a state's `light:` gives one
///   light.radius      how far it reaches, in tiles (default 2)
///   light.effect      none, flicker, pulse or strobe, as for light zones
///   light.low         the effect's dark end, 0-255 (default half the intensity)
///   light.tics        the effect's timing, 70 a second (default flicker 8, pulse 70, strobe 35)
///   light.brighttics  how long a strobe stays at the intensity (default 5)
/// </summary>
internal sealed record ActorLightInfo(int Intensity, double Radius, ZoneEffect Effect, int? Low, int Tics, int BrightTics, bool HasStateLights)
{
    public const double DefaultRadius = 2;

    /// <summary>The light of an actor's class, or null if it has none at all.</summary>
    public static ActorLightInfo? Of(Actor actor)
    {
        int intensity = Math.Clamp(Int(actor, "light.intensity") ?? 0, 0, 255);
        bool stateLights = actor.ResolvedStates.Values.Any(HasLight);
        if (intensity == 0 && !stateLights)
            return null;

        var effect = ZoneEffect.None;
        if (actor.Properties.TryGetValue("light.effect", out var name)
            && !Program.TryParseZoneEffect(name?.ToString() ?? "", out effect))
            Console.WriteLine($"{actor.Name}: unknown light.effect '{name}' (none, flicker, pulse or strobe)");

        double radius = actor.Properties.TryGetValue("light.radius", out var r)
            && double.TryParse(Convert.ToString(r, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var tiles)
            && tiles >= 0 ? tiles : DefaultRadius;

        return new ActorLightInfo(intensity, radius, effect, Int(actor, "light.low"),
            Math.Max(Int(actor, "light.tics") ?? LightEffects.DefaultTics(effect), 1),
            Math.Max(Int(actor, "light.brighttics") ?? 5, 0), stateLights);
    }

    private static int? Int(Actor actor, string key) =>
        actor.Properties.TryGetValue(key, out var value)
        && int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
        ? number : null;

    // Whether any frame reachable from this one has a state light
    private static bool HasLight(ActorStateFrame first)
    {
        var seen = new HashSet<ActorStateFrame>(ReferenceEqualityComparer.Instance);
        for (var frame = first; frame != null && seen.Add(frame); frame = frame.Next)
            if (frame.Light != null)
                return true;
        return false;
    }
}

/// <summary>An actor's light as it goes: its class's light (null for none), and where its effect is.</summary>
internal sealed class ActorLightState
{
    public ActorLightInfo? Info;
    public int Phase, FlickerAmount;
}
