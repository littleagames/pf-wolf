using System.Text;
using PFWolf.Editor.Editing;

namespace PFWolf.Editor.Definitions;

/// <summary>
/// Saves definition files into a mod (a folder or a pk3). pfwolf.pk3's own files are read-only:
/// a change to one of them goes in a mod, laid over it.
/// </summary>
public static class DefinitionSaver
{
    /// <summary>Saves a change worked out by DefinitionSet.PlanWrite; returns where it went</summary>
    public static IReadOnlyList<string> Save(DefinitionWrite write)
        => SaveText(write.File, write.NewText);

    /// <summary>Saves a file's whole text (from the YAML editor); returns where it went</summary>
    public static IReadOnlyList<string> SaveText(DefinitionFile file, string text)
    {
        if (file.IsBase)
            throw new UnauthorizedAccessException($"{file.EntryPath} is one of pfwolf.pk3's own files, which are read-only: put the change in a mod");
        return [ModFiles.Write(file.ModPath!, (file.EntryPath, new UTF8Encoding(false).GetBytes(text)))];
    }
}
