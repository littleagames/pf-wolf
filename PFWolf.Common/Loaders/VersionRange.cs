using System.Globalization;

namespace PFWolf.Loaders;

/// <summary>
/// A range of PFWolf versions, as modinfo.yaml's pfwolf gives it: comparisons separated by
/// spaces, each of which a version must pass (">=0.8.0 &lt;0.9.0"), or a version alone, for it
/// and every later one ("0.8.0"). A version is up to three numbers separated by dots, the
/// missing ones 0 ("0.8" is 0.8.0); a leading v and anything from a - or + on ("0.8.0-alpha")
/// are left off.
/// </summary>
public sealed class VersionRange
{
    private static readonly string[] Operators = [">=", "<=", ">", "<", "="];

    private readonly List<(string Operator, Version Version)> _comparisons;

    private VersionRange(List<(string, Version)> comparisons) => _comparisons = comparisons;

    /// <summary>The range, or false when it can't be read (an empty one, or a comparison that isn't one)</summary>
    public static bool TryParse(string text, out VersionRange range)
    {
        range = new VersionRange([]);
        var parts = text.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        var comparisons = new List<(string, Version)>();
        foreach (var part in parts)
        {
            var op = Operators.FirstOrDefault(part.StartsWith) ?? "";
            if (ParseVersion(part[op.Length..]) is not { } version)
                return false;
            // A version alone: it and every later one
            comparisons.Add((op.Length == 0 ? ">=" : op, version));
        }
        range = new VersionRange(comparisons);
        return true;
    }

    /// <summary>Whether the version passes every comparison</summary>
    public bool Includes(Version version)
    {
        var v = Normalize(version);
        return _comparisons.All(c => c.Operator switch
        {
            ">=" => v >= c.Version,
            "<=" => v <= c.Version,
            ">" => v > c.Version,
            "<" => v < c.Version,
            _ => v == c.Version,
        });
    }

    /// <summary>A version as text ("0.8.0"), as a range names it</summary>
    public static string Format(Version version) => Normalize(version).ToString(3);

    /// <summary>"0.8", "v0.8.0", "0.8.0-alpha" as 0.8.0; null for anything else</summary>
    public static Version? ParseVersion(string text)
    {
        text = text.Trim().TrimStart('v', 'V');
        var end = text.IndexOfAny(['-', '+']);
        if (end >= 0)
            text = text[..end];

        var numbers = text.Split('.');
        if (numbers.Length is 0 or > 3)
            return null;
        var values = new int[3];
        for (int i = 0; i < numbers.Length; i++)
        {
            if (!int.TryParse(numbers[i], NumberStyles.None, CultureInfo.InvariantCulture, out values[i]))
                return null;
        }
        return new Version(values[0], values[1], values[2]);
    }

    // Three numbers, so 0.8 and 0.8.0 (and an assembly's 0.8.0.0) compare alike
    private static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));
}
