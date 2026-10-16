using PFWolf.Assets;
using PFWolf.Entities;

namespace PFWolf;

/// <summary>A level's shading, with every value filled in (see ShadingInfo)</summary>
internal record struct ShadingSettings(string FadeColor, double FadeStart, double FadeEnd, int MaxFade, int Light, bool InverseFade = false);

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

    // The light level and tint (an index in GameHost's tints) its tiles have in the light grid,
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
        if (effect != null && !Lighting.TryParseZoneEffect(effect, out zone.Effect))
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
