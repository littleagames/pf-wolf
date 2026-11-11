using System.Globalization;
using PFWolf.Shading;

namespace PFWolf.Entities.Actors;

/// <summary>
/// An actor class's light, from its actordefs properties (inherited like any other):
///   light.intensity   light added at the actor, 0-255 (as zone light: 255 is full); 0 or left
///                     out: none, unless a state's `light:` gives one
///   light.radius      how far it reaches, in tiles (default 2)
///   light.color       #RRGGBB it tints what it lights (default none: white light)
///   light.effect      none, flicker, pulse or strobe, as for light zones
///   light.low         the effect's dark end, 0-255 (default half the intensity)
///   light.tics        the effect's timing, 70 a second (default flicker 8, pulse 70, strobe 35)
///   light.brighttics  how long a strobe stays at the intensity (default 5)
///   light.mode        add (the default): its light is added to what's there; set: what it
///                     reaches takes its intensity as its light, as a light zone would, darker
///                     than the level too (a bulb whose room goes dark as it goes off)
/// </summary>
public sealed record ActorLightInfo(int Intensity, double Radius, string? Color, ZoneEffect Effect, int? Low, int Tics, int BrightTics,
    bool HasStateLights, bool Sets = false)
{
    public const double DefaultRadius = 2;

    /// <summary>
    /// The light of a class (by its name, merged properties and resolved states), or null if it
    /// has none at all; what isn't right is said on the console, as the game says it
    /// </summary>
    public static ActorLightInfo? From(string name, IReadOnlyDictionary<string, object> properties, IEnumerable<ActorStateFrame> resolvedStates)
    {
        int intensity = Math.Clamp(Int(properties, "light.intensity") ?? 0, 0, 255);
        bool stateLights = resolvedStates.Any(HasLight);
        bool sets = false;
        if (properties.TryGetValue("light.mode", out var mode))
        {
            switch (mode?.ToString()?.Trim().ToLowerInvariant())
            {
                case "set": sets = true; break;
                case "add" or "" or null: break;
                default: Console.WriteLine($"{name}: unknown light.mode '{mode}' (add or set)"); break;
            }
        }
        // A set light at 0 is a dark one, not none
        if (intensity == 0 && !stateLights && !sets)
            return null;

        var effect = ZoneEffect.None;
        if (properties.TryGetValue("light.effect", out var effectName)
            && !ShadingResolver.TryParseZoneEffect(effectName?.ToString() ?? "", out effect))
            Console.WriteLine($"{name}: unknown light.effect '{effectName}' (none, flicker, pulse or strobe)");

        double radius = properties.TryGetValue("light.radius", out var r)
            && double.TryParse(Convert.ToString(r, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var tiles)
            && tiles >= 0 ? tiles : DefaultRadius;

        var color = properties.TryGetValue("light.color", out var c) ? c?.ToString() : null;

        return new ActorLightInfo(intensity, radius, color, effect, Int(properties, "light.low"),
            Math.Max(Int(properties, "light.tics") ?? LightEffects.DefaultTics(effect), 1),
            Math.Max(Int(properties, "light.brighttics") ?? 5, 0), stateLights, sets);
    }

    private static int? Int(IReadOnlyDictionary<string, object> properties, string key) =>
        properties.TryGetValue(key, out var value)
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

/// <summary>
/// An actor's light as it goes: its class's light (null for none, once Resolved), what
/// A_SetLight / A_LightOff set in its place, and where its effect is.
/// </summary>
public sealed class ActorLightState
{
    public bool Resolved;
    public ActorLightInfo? Info;
    public LightOverride? Override;
    public int Phase, FlickerAmount;
}

/// <summary>
/// What A_SetLight (or A_LightOff: Off) set an actor's light to, in place of its class's
/// light. A state's own `light:` still changes it frame by frame, unless it's off. Saved.
/// </summary>
public sealed record LightOverride(bool Off, int Intensity, double Radius, string? Color)
{
    public void Write(BinaryWriter bw)
    {
        bw.Write(Off);
        bw.Write(Intensity);
        bw.Write(Radius);
        bw.Write(Color ?? "");
    }

    public static LightOverride Read(BinaryReader br)
    {
        bool off = br.ReadBoolean();
        int intensity = br.ReadInt32();
        double radius = br.ReadDouble();
        var color = br.ReadString();
        if (intensity is < 0 or > 255 || radius < 0 || double.IsNaN(radius))
            throw new InvalidDataException("An actor's light is out of range.");
        return new LightOverride(off, intensity, radius, color.Length == 0 ? null : color);
    }
}

/// <summary>The light an actor gives off, from what it's made of: the game's and the editor's rule</summary>
public static class ActorLightRules
{
    /// <summary>
    /// The light an actor gives off now (intensity 0-255, radius in tiles, color or null): its
    /// class's light (<paramref name="info"/>) or what A_SetLight set (<paramref name="set"/>),
    /// changed by its frame's `light:` and its effect where that is (phase, flicker amount).
    /// A_LightOff, or a frame's `light: none`, puts it out.
    /// </summary>
    public static (int Intensity, double Radius, string? Color) Current(ActorLightInfo? info, LightOverride? set, StateLight? light,
        int phase, int flickerAmount)
    {
        if (set?.Off == true || light?.Off == true || (info == null && set == null))
            return (0, 0, null);

        int intensity = light?.Intensity ?? set?.Intensity ?? info!.Intensity;
        double radius = light?.Radius ?? set?.Radius ?? info!.Radius;
        string? color = light?.Color ?? (set != null ? set.Color : info!.Color);
        if (info is { Effect: not ZoneEffect.None })
            intensity = LightEffects.Apply(info.Effect, intensity, info.Low ?? intensity / 2, info.Tics, info.BrightTics,
                phase, flickerAmount);
        return (intensity, radius, color);
    }
}
