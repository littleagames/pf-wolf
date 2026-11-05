using System.Globalization;

namespace PFWolf.Assets;

/// <summary>
/// A game pack's particle effects (gamepacks/{pack}/particles.yaml, a mod's particles.yaml):
/// the kinds of particle there are and the emitters that throw them out. Particles are only for
/// show: they're drawn on this machine alone, can't hurt or block anything, and aren't saved.
/// Distances are in texels (64 a tile or a story) and times in tics (70 a second). A pack
/// built on a base-pack starts from its file.
/// </summary>
public record ParticlesAsset : Asset
{
    /// <summary>The kinds of particle, by name: how each looks and moves</summary>
    public Dictionary<string, ParticleTypeInfo> Types { get; set; } = [];

    /// <summary>The emitters, by name: which kind of particle each throws out, how many, which way and how fast</summary>
    public Dictionary<string, EmitterInfo> Emitters { get; set; } = [];
}

/// <summary>One kind of particle in particles.yaml</summary>
public record ParticleTypeInfo
{
    /// <summary>
    /// How it's drawn: dot (a round blob), square, or streak (a line back along the way it's
    /// going, trail tics long)
    /// </summary>
    public string Shape { get; set; } = "dot";

    /// <summary>How big it is, in texels (a streak: how wide); at least a pixel however far away</summary>
    public double Size { get; set; } = 1;

    /// <summary>How big it is at the end of its life, growing or shrinking to it; size when left out</summary>
    public double? SizeEnd { get; set; }

    /// <summary>Its color: #RRGGBB or a palette index (0-255)</summary>
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>
    /// Colors it goes through over its life, first to last (#RRGGBB or palette indices), in
    /// place of color: a spark from white through yellow to dark red
    /// </summary>
    public List<string> Colors { get; set; } = [];

    /// <summary>How long it lasts, in tics: a number, or a range (20-35) for each to last its own time</summary>
    public string Life { get; set; } = "35";

    /// <summary>How fast it falls faster, in texels a tic each tic; negative rises (smoke, embers)</summary>
    public double Gravity { get; set; }

    /// <summary>The share of its speed it loses each tic, 0 to 1 (air slowing it down)</summary>
    public double Drag { get; set; }

    /// <summary>Drawn at full brightness, in the dark too (sparks, embers); otherwise lit as the level is where it is</summary>
    public bool Bright { get; set; }

    /// <summary>
    /// What it does when it meets a wall, a shut door, the floor or a ceiling: die (the
    /// default), bounce, stop (stays there till its life is out), or none (goes through)
    /// </summary>
    public string Collide { get; set; } = "die";

    /// <summary>With collide: bounce, how much of its speed it keeps off what it hits, 0 to 1</summary>
    public double Bounce { get; set; } = 0.5;

    /// <summary>A streak's length: how many tics of its travel it trails behind it</summary>
    public double Trail { get; set; } = 2;
}

/// <summary>
/// One emitter in particles.yaml: what it throws out and how. A burst throws out count at
/// once; a running emitter (on an actor, a tile, or from a script) rate a second.
/// </summary>
public record EmitterInfo
{
    /// <summary>The kind of particle (a particles.yaml types name)</summary>
    public string Type { get; set; } = "";

    /// <summary>How many a burst throws out</summary>
    public int Count { get; set; } = 10;

    /// <summary>How many a second a running emitter throws out</summary>
    public double Rate { get; set; } = 10;

    /// <summary>Which way they go: degrees round from the way the emitter faces (positive to the left)</summary>
    public double Angle { get; set; }

    /// <summary>Which way they go: degrees up (90 straight up, -90 straight down)</summary>
    public double Pitch { get; set; }

    /// <summary>How far from that way each can go, in degrees: 0 all the same way, 180 every way</summary>
    public double Spread { get; set; } = 180;

    /// <summary>How fast they go, in texels a tic: a number, or a range (1-3)</summary>
    public string Speed { get; set; } = "1";

    /// <summary>How far from the emitter each starts, at most, in texels</summary>
    public double Radius { get; set; }

    /// <summary>How high they start, in texels above the floor (32 is halfway up a story)</summary>
    public double Z { get; set; } = 32;

    /// <summary>A running emitter farther than this from the view, in tiles, throws nothing out</summary>
    public double Range { get; set; } = 16;
}

/// <summary>
/// A number or a range of them, as particles.yaml writes them: "35", "20-35", "-2--1",
/// "0.5-1.5". Each particle picks its own from a range.
/// </summary>
public readonly record struct ParticleRange(double Min, double Max)
{
    /// <summary>Reads a number or range; false (and the fallback) when it's neither</summary>
    public static bool TryParse(string? text, out ParticleRange range)
    {
        range = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        text = text.Trim();
        if (TryNumber(text, out double single))
        {
            range = new ParticleRange(single, single);
            return true;
        }

        // The dash between the two: not one that starts a negative number (the first character,
        // or one straight after another dash)
        for (int i = 1; i < text.Length; i++)
        {
            if (text[i] != '-' || text[i - 1] == '-' || text[i - 1] == 'e' || text[i - 1] == 'E')
                continue;
            if (TryNumber(text[..i], out double min) && TryNumber(text[(i + 1)..], out double max))
            {
                range = min <= max ? new ParticleRange(min, max) : new ParticleRange(max, min);
                return true;
            }
        }
        return false;
    }

    /// <summary>A number or range, or the fallback when it's neither</summary>
    public static ParticleRange Parse(string? text, double fallback) =>
        TryParse(text, out var range) ? range : new ParticleRange(fallback, fallback);

    static bool TryNumber(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    /// <summary>A value in the range, roll 0 to 1 from its least to its most</summary>
    public double At(double roll) => Min + (Max - Min) * roll;

    public override string ToString() => Min == Max
        ? Min.ToString(CultureInfo.InvariantCulture)
        : $"{Min.ToString(CultureInfo.InvariantCulture)}-{Max.ToString(CultureInfo.InvariantCulture)}";
}
