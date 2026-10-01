using Wolf3D.Assets;
using Wolf3D.Entities;

namespace Wolf3D;

internal enum ZoneEffect : byte { None, Flicker, Pulse, Strobe }

/// <summary>
/// How an effect moves a light between low and its light, for light zones and actor lights
/// alike. An effect's place is a phase (tics into a pulse or strobe cycle, or for a flicker,
/// tics until it jumps again) and, for a flicker, how far toward low it jumped (0 to 256).
/// </summary>
internal static class LightEffects
{
    public static int DefaultTics(ZoneEffect effect) => effect switch
    {
        ZoneEffect.Flicker => 8,
        ZoneEffect.Pulse => 70,
        _ => 35,
    };

    /// <summary>The light an effect shows now; tics is its cycle (at least 1).</summary>
    public static int Apply(ZoneEffect effect, int light, int low, int tics, int brightTics, int phase, int flickerAmount)
    {
        switch (effect)
        {
            case ZoneEffect.Flicker:
                return light + (low - light) * flickerAmount / 256;
            case ZoneEffect.Pulse:
            {
                // down to low over the first half of the cycle, and back up over the second
                int half = Math.Max(tics / 2, 1);
                int into = phase < half ? phase : Math.Max(tics - phase, 0);
                return light + (low - light) * Math.Min(into, half) / half;
            }
            case ZoneEffect.Strobe:
                return phase < brightTics ? light : low;
            default:
                return light;
        }
    }

    /// <summary>Moves an effect on by elapsed tics; tics is its cycle (at least 1).</summary>
    public static void Tick(ZoneEffect effect, int elapsed, int tics, ref int phase, ref int flickerAmount, Random rng)
    {
        switch (effect)
        {
            case ZoneEffect.Pulse or ZoneEffect.Strobe:
                phase = (phase + elapsed) % tics;
                break;
            case ZoneEffect.Flicker:
                phase -= elapsed;
                if (phase <= 0)
                {
                    phase = rng.Next(1, tics + 1);
                    flickerAmount = rng.Next(0, 257);
                }
                break;
        }
    }
}

/// <summary>
/// A light zone as it stands: its light (game-info zones, changed by switches and actors), its
/// tint, a fade in progress and an effect. Saved with the level.
/// </summary>
internal sealed class ZoneState
{
    /// <summary>The light, 0 to 255: where a fade in progress is heading</summary>
    public int Light = 255;

    /// <summary>#RRGGBB tint, null for none</summary>
    public string? Color;

    public ZoneEffect Effect;
    public int? LowSetting, TicsSetting;    // null: the defaults (see Low, Tics)
    public int BrightTics = 5;

    // A fade to Light: from FadeFrom, with FadeLeft of FadeTotal tics to go
    public int FadeFrom, FadeLeft, FadeTotal;

    // Where the effect is: tics into a pulse or strobe cycle, or for a flicker, tics until it
    // jumps again, and how far toward Low it jumped (0 to 256)
    public int Phase, FlickerAmount;

    // The light level and tint (an index in Program's tints) its tiles have in the light grid,
    // and the color the tint is for; RowLevel -1 until they're set
    public int RowLevel = -1;
    public byte RowTint;
    public string? RowColor;

    /// <summary>The light before any effect: part way through a fade, or Light</summary>
    public int BaseLight => FadeLeft > 0 ? Light + (FadeFrom - Light) * FadeLeft / FadeTotal : Light;

    public int Low => LowSetting ?? BaseLight / 2;

    public int Tics => Math.Max(TicsSetting ?? LightEffects.DefaultTics(Effect), 1);

    /// <summary>The light it's showing now, the effect included</summary>
    public int Current => LightEffects.Apply(Effect, BaseLight, Low, Tics, BrightTics, Phase, FlickerAmount);

    public void Tick(int tics, Random rng)
    {
        if (FadeLeft > 0)
            FadeLeft = Math.Max(FadeLeft - tics, 0);
        LightEffects.Tick(Effect, tics, Tics, ref Phase, ref FlickerAmount, rng);
    }

    public void SetEffect(ZoneEffect effect)
    {
        Effect = effect;
        Phase = FlickerAmount = 0;
    }

    public static ZoneState FromInfo(ZoneInfo? map, ZoneInfo? fallback)
    {
        var zone = new ZoneState
        {
            Light = Math.Clamp(map?.Light ?? fallback?.Light ?? 255, 0, 255),
            Color = map?.Color ?? fallback?.Color,
            LowSetting = map?.Low ?? fallback?.Low,
            TicsSetting = map?.Tics ?? fallback?.Tics,
            BrightTics = Math.Max(map?.BrightTics ?? fallback?.BrightTics ?? 5, 0),
        };
        var effect = map?.Effect ?? fallback?.Effect;
        if (effect != null && !Program.TryParseZoneEffect(effect, out zone.Effect))
            Console.WriteLine($"Unknown zone effect '{effect}' (none, flicker, pulse or strobe)");
        return zone;
    }

    public void Write(BinaryWriter bw)
    {
        bw.Write(Light);
        bw.Write(Color != null);
        if (Color != null)
            bw.Write(Color);
        bw.Write((byte)Effect);
        bw.Write(LowSetting ?? -1);
        bw.Write(TicsSetting ?? -1);
        bw.Write(BrightTics);
        bw.Write(FadeFrom);
        bw.Write(FadeLeft);
        bw.Write(FadeTotal);
        bw.Write(Phase);
        bw.Write(FlickerAmount);
    }

    public static ZoneState Read(BinaryReader br)
    {
        var zone = new ZoneState { Light = br.ReadInt32() };
        zone.Color = br.ReadBoolean() ? br.ReadString() : null;
        zone.Effect = (ZoneEffect)br.ReadByte();
        if (!Enum.IsDefined(zone.Effect))
            throw new InvalidDataException($"Unknown zone effect {(byte)zone.Effect}.");
        int low = br.ReadInt32(), tics = br.ReadInt32();
        zone.LowSetting = low < 0 ? null : low;
        zone.TicsSetting = tics < 0 ? null : tics;
        zone.BrightTics = br.ReadInt32();
        zone.FadeFrom = br.ReadInt32();
        zone.FadeLeft = br.ReadInt32();
        zone.FadeTotal = br.ReadInt32();
        zone.Phase = br.ReadInt32();
        zone.FlickerAmount = br.ReadInt32();
        if (zone.FadeLeft > zone.FadeTotal)
            throw new InvalidDataException("A zone's light fade has more tics left than it started with.");
        return zone;
    }
}

internal partial class Program
{
    /*
    =============================================================================

                                ZONE LIGHTS

    Light zones change as the level is played: switches and actor states set their light
    (at once or fading over some tics) and effects make it flicker, pulse or strobe. The
    zones tick with the actors, and each frame a zone whose light level or tint has moved
    gives its tiles the new one in the light grid (Program.LightGrid.cs).

    =============================================================================
    */

    // Flickering is only for show, so it doesn't draw on the game's random numbers (demos)
    static readonly Random zonerandom = new();

    internal static bool TryParseZoneEffect(string name, out ZoneEffect effect) =>
        Enum.TryParse(name, ignoreCase: true, out effect) && Enum.IsDefined(effect);

    /// <summary>Runs the zones' fades and effects for this tic's worth of time.</summary>
    static void TickZoneLights(uint tics)
    {
        foreach (var zone in levelzones.Values)
            zone.Tick((int)tics, zonerandom);
        UpdateZoneLevels();
        TickActorLights(tics);      // and actors' (Program.ActorLights.cs)
    }

    /// <summary>The zone with this id, made (at full light) if the level has none yet.</summary>
    static ZoneState ZoneFor(int id)
    {
        if (!levelzones.TryGetValue(id, out var zone))
        {
            levelzones[id] = zone = new ZoneState();
            RebuildShading();           // a new zone may turn shading on
        }
        return zone;
    }

    /// <summary>Sets a zone's light, fading to it over tics (0: at once).</summary>
    internal static void FadeZoneLight(int id, int light, int tics)
    {
        var zone = ZoneFor(id);
        zone.FadeFrom = zone.BaseLight;
        zone.Light = Math.Clamp(light, 0, 255);
        zone.FadeTotal = zone.FadeLeft = Math.Max(tics, 0);
        PrewarmZoneRows(zone, zone.FadeFrom, zone.Light);
        UpdateZoneLevels();
    }

    /// <summary>Sets a zone's tint (null for none).</summary>
    internal static void SetZoneColor(int id, string? color)
    {
        ZoneFor(id).Color = color;
        RebuildShading();               // a tint may turn shading on
    }

    internal static void SetZoneEffect(int id, ZoneEffect effect)
    {
        var zone = ZoneFor(id);
        zone.SetEffect(effect);
        PrewarmZoneRows(zone, zone.Low, zone.BaseLight);
        UpdateZoneLevels();
    }

    /// <summary>Takes a zone out: its tiles get the level's light.</summary>
    internal static void RemoveZone(int id)
    {
        levelzones.Remove(id);
        RebuildShading();
    }

    /// <summary>Builds the light rows a zone going between two lights will need, so it doesn't stall mid-level.</summary>
    static void PrewarmZoneRows(ZoneState zone, int from, int to)
    {
        if (!shading)
            return;
        var tint = ParseRgb(zone.Color, (255, 255, 255), "zone color");
        for (int level = LightLevelOf(Math.Min(from, to)); level <= LightLevelOf(Math.Max(from, to)); level++)
            LevelRow(level, tint);
    }

    /*
    =============================================================================

                                    ACTIONS

    For switches (walls.yaml `switch: actions`) and actor states alike. They name the zone
    they change, whatever the switch's tag:

      A_SetZoneLight(zone, light, [tics], [color])  fades the zone to light (0-255) over tics
                                                    (left out or 0: at once); color #RRGGBB
                                                    tints it, "none" takes the tint off, left
                                                    out keeps it
      A_ToggleZoneLight(zone, light1, light2, [tics])  to light2 if it's at (or going to)
                                                    light1, else to light1
      A_SetZoneEffect(zone, effect)                 none, flicker, pulse or strobe, timed as
                                                    the zone's game-info low, tics, bright-tics

    =============================================================================
    */

    private static void RegisterZoneLightActions()
    {
        void Both(string name, Func<string, string[], bool> action)
        {
            MapTriggerRegistry.Register(name, (_, args) => action(name, args));
            Entities.Actors.ActorActionRegistry.Register(name, (Entities.Actors.Actor _, string[] args) => action(name, args));
        }

        Both("A_SetZoneLight", SetZoneLightAction);
        Both("A_ToggleZoneLight", ToggleZoneLightAction);
        Both("A_SetZoneEffect", SetZoneEffectAction);
    }

    private static bool SetZoneLightAction(string name, string[] args)
    {
        if (!ZoneArg(name, args, 0, out var zone) || !LightArg(name, args, 1, out var light) || !TicsArg(name, args, 2, out var tics))
            return false;

        switch (args.ElementAtOrDefault(3))
        {
            case null or "":
                break;
            case var none when none.Equals("none", StringComparison.OrdinalIgnoreCase):
                SetZoneColor(zone, null);
                break;
            case var color:
                if (!IsRgb(color))
                {
                    Console.WriteLine($"{name}: '{color}' isn't a #RRGGBB color.");
                    return false;
                }
                SetZoneColor(zone, color);
                break;
        }

        FadeZoneLight(zone, light, tics);
        return true;
    }

    private static bool ToggleZoneLightAction(string name, string[] args)
    {
        if (!ZoneArg(name, args, 0, out var zone) || !LightArg(name, args, 1, out var light1)
            || !LightArg(name, args, 2, out var light2) || !TicsArg(name, args, 3, out var tics))
            return false;

        FadeZoneLight(zone, ZoneFor(zone).Light == light1 ? light2 : light1, tics);
        return true;
    }

    private static bool SetZoneEffectAction(string name, string[] args)
    {
        if (!ZoneArg(name, args, 0, out var zone))
            return false;
        if (!TryParseZoneEffect(args.ElementAtOrDefault(1) ?? "", out var effect))
        {
            Console.WriteLine($"{name}: '{args.ElementAtOrDefault(1)}' isn't none, flicker, pulse or strobe.");
            return false;
        }

        SetZoneEffect(zone, effect);
        return true;
    }

    private static bool ZoneArg(string name, string[] args, int index, out int zone)
    {
        if (int.TryParse(args.ElementAtOrDefault(index), out zone) && zone is > 0 and <= ushort.MaxValue)
            return true;
        Console.WriteLine($"{name}: '{args.ElementAtOrDefault(index)}' isn't a zone (1-65535).");
        return false;
    }

    private static bool LightArg(string name, string[] args, int index, out int light)
    {
        if (int.TryParse(args.ElementAtOrDefault(index), out light) && light is >= 0 and <= 255)
            return true;
        Console.WriteLine($"{name}: '{args.ElementAtOrDefault(index)}' isn't a light (0-255).");
        return false;
    }

    // Left out (or "") is 0: at once
    private static bool TicsArg(string name, string[] args, int index, out int tics)
    {
        tics = 0;
        var arg = args.ElementAtOrDefault(index);
        if (string.IsNullOrEmpty(arg) || int.TryParse(arg, out tics) && tics >= 0)
            return true;
        Console.WriteLine($"{name}: '{arg}' isn't a number of tics.");
        return false;
    }

    internal static bool IsRgb(string color)
    {
        try { Color.FromHexRGBA(color); return color.TrimStart('#').Length == 6; }
        catch (Exception e) when (e is ArgumentException or FormatException) { return false; }
    }

    /*
    =============================================================================

                                    SAVES

    =============================================================================
    */

    static void WriteZoneLights(BinaryWriter bw)
    {
        bw.Write(levelzones.Count);
        foreach (var (id, zone) in levelzones)
        {
            bw.Write(id);
            zone.Write(bw);
        }
    }

    static Dictionary<int, ZoneState> ReadZoneLights(BinaryReader br)
    {
        var zones = new Dictionary<int, ZoneState>();
        for (int i = br.ReadCount(); i > 0; i--)
        {
            int id = br.ReadInt32();
            zones[id] = ZoneState.Read(br);
        }
        return zones;
    }

    /// <summary>Puts the level's zones back as a save had them (after SetupGameLevel set them from game-info).</summary>
    static void RestoreZoneLights(Dictionary<int, ZoneState> zones)
    {
        levelzones = zones;
        RebuildShading();
    }
}
