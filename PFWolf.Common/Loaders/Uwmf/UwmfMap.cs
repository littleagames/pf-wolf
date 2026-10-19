using System.Globalization;

namespace PFWolf.Loaders.Uwmf;

/// <summary>A UWMF value: a string, a whole number, a decimal number or true/false</summary>
public readonly record struct UwmfValue(object Value)
{
    public static implicit operator UwmfValue(string value) => new(value);
    public static implicit operator UwmfValue(int value) => new(value);
    public static implicit operator UwmfValue(double value) => new(value);
    public static implicit operator UwmfValue(bool value) => new(value);

    /// <summary>How it's written in a TEXTMAP: quoted and escaped for a string, as is otherwise</summary>
    public string ToText() => Value switch
    {
        string text => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
        bool flag => flag ? "true" : "false",
        double number => number.ToString(number == Math.Floor(number) ? "0.0" : "0.0###", CultureInfo.InvariantCulture),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        var other => other.ToString() ?? "",
    };

    public override string ToString() => ToText();
}

/// <summary>
/// A block's key = value pairs, in the order written; keys are matched whatever their case, as
/// ECWolf does. Typed reads fall back on a default when the key's left out or of another type.
/// </summary>
public sealed class UwmfBlock
{
    private readonly List<KeyValuePair<string, UwmfValue>> _pairs = [];

    public IReadOnlyList<KeyValuePair<string, UwmfValue>> Pairs => _pairs;

    /// <summary>The line the block starts on, for messages (0 for a block made in code)</summary>
    public int Line { get; init; }

    public bool Has(string key) => _pairs.Any(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public UwmfValue? this[string key]
    {
        get
        {
            // the last one written wins, as a later assignment would in ECWolf
            for (int i = _pairs.Count - 1; i >= 0; i--)
                if (_pairs[i].Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return _pairs[i].Value;
            return null;
        }
        set
        {
            _pairs.RemoveAll(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (value is { } v)
                _pairs.Add(new(key, v));
        }
    }

    public void Add(string key, UwmfValue value) => _pairs.Add(new(key, value));

    public string String(string key, string fallback = "") => this[key]?.Value as string ?? fallback;

    public int Int(string key, int fallback = 0) => this[key]?.Value switch
    {
        int number => number,
        double number when number == Math.Floor(number) => (int)number,
        _ => fallback,
    };

    public double Double(string key, double fallback = 0) => this[key]?.Value switch
    {
        double number => number,
        int number => number,
        _ => fallback,
    };

    public bool Bool(string key, bool fallback = false) => this[key]?.Value is bool flag ? flag : fallback;
}

/// <summary>One spot of a planemap: indices into the tile, sector and zone palettes (-1 for none), and its tag</summary>
public readonly record struct UwmfSpot(int Tile, int Sector, int Zone, int Tag);

/// <summary>
/// A UWMF level (ECWolf's TEXTMAP) as written: the header's values, the tile, sector and zone
/// palettes, the planes and their planemaps, and the things and triggers. Nothing is looked up:
/// UwmfTranslator turns it into a level.
/// </summary>
public sealed class UwmfMap
{
    /// <summary>The top-level key = value pairs (namespace, name, width, height, tilesize, ...)</summary>
    public UwmfBlock Header { get; } = new();

    public string Namespace => Header.String("namespace");
    public string Name => Header.String("name");
    public int Width => Header.Int("width", 64);
    public int Height => Header.Int("height", 64);
    public int TileSize => Header.Int("tilesize", 64);

    public List<UwmfBlock> Tiles { get; } = [];
    public List<UwmfBlock> Sectors { get; } = [];
    public List<UwmfBlock> Zones { get; } = [];
    public List<UwmfBlock> Planes { get; } = [];

    /// <summary>One planemap per plane, each Width x Height spots row by row</summary>
    public List<UwmfSpot[]> PlaneMaps { get; } = [];

    public List<UwmfBlock> Things { get; } = [];
    public List<UwmfBlock> Triggers { get; } = [];

    /// <summary>Top-level blocks of kinds this reader doesn't know, by name (skipped, as ECWolf does)</summary>
    public List<string> UnknownBlocks { get; } = [];
}
