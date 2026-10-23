using PFWolf.Loaders;

namespace PFWolf.Exceptions;

/// <summary>
/// A data file that's there but can't be read (a raw data mod's FOXTROT5.WL6): says which file,
/// what it is (VGAGRAPH) and which files were read with it, before what went wrong
/// </summary>
public class DataFileReadException(string message, Exception inner) : Exception(message, inner)
{
    /// <summary>
    /// What <paramref name="read"/> returns; whatever it throws comes back as a DataFileReadException
    /// naming <paramref name="file"/>
    /// </summary>
    public static T Reading<T>(DataFileKind kind, string file, Func<T> read, params string[] readWith)
    {
        try
        {
            return read();
        }
        catch (Exception e) when (e is not DataFileReadException and not OutOfMemoryException)
        {
            var with = readWith.Length > 0 ? $", read with {string.Join(" and ", readWith.Select(Path.GetFileName))}" : "";
            throw new DataFileReadException($"{Path.GetFileName(file)} ({DataFileSniffer.StemOf(kind)}{with}) can't be read: {e.Message}", e);
        }
    }

    /// <summary>Runs <paramref name="read"/>; whatever it throws comes back naming <paramref name="file"/></summary>
    public static void Reading(DataFileKind kind, string file, Action read, params string[] readWith)
        => Reading(kind, file, () => { read(); return true; }, readWith);
}
