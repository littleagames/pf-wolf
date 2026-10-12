namespace PFWolf.Loaders;

/// <summary>
/// Names for one part of a data file's chunks (its walls, its pics) from raw-data-map. A data
/// file edited by WDC or ChaosEdit can have more chunks than the map names: those get a made-up
/// name (PIC0145, SPR0412) instead of stopping the load, and <see cref="Warn"/> says how many.
/// </summary>
internal sealed class DataMapNames(List<string> names, string what, string prefix)
{
    private int _unnamed;

    public int Count => names.Count;

    /// <summary>The map's name at <paramref name="index"/>, or PREFIX#### (<paramref name="number"/>, else the index) when it has none</summary>
    public string NameAt(int index, int? number = null)
    {
        if (index >= 0 && index < names.Count)
            return names[index];

        _unnamed++;
        return Generated(prefix, number ?? index);
    }

    public static string Generated(string prefix, int number) => $"{prefix}{number:0000}";

    /// <summary>Warns when any chunk got a made-up name: the data file doesn't match raw-data-map</summary>
    public void Warn(Action<string>? warn)
    {
        if (_unnamed > 0)
            warn?.Invoke($"has {_unnamed} {what} raw-data-map has no names for (it doesn't match the file): they're named {prefix}####");
    }
}
