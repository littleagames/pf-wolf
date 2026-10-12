using System.IO.Compression;

namespace PFWolf.Editor.Editing;

/// <summary>
/// A mod the editor reads and writes files in, by their path inside it (maps/MAP01.wad,
/// game-info.yaml): a folder, or a pk3 (zip) file. A pk3 is rewritten whole for each save: the
/// new one is written beside it and put in its place, and the one it replaces is kept as
/// NAME.pk3.bak, so a save can be undone by hand.
/// </summary>
public static class ModFiles
{
    /// <summary>The extension added to a pk3's last version when it's saved over</summary>
    public const string BackupExtension = ".bak";

    /// <summary>Whether the mod is a pk3 (or zip) file, not a folder</summary>
    public static bool IsArchive(string modPath) => File.Exists(modPath);

    /// <summary>A file in the mod as text, or null when it has none of that path</summary>
    public static string? ReadText(string modPath, string entryPath)
    {
        if (!IsArchive(modPath))
        {
            var path = PathIn(modPath, entryPath);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        using var archive = ZipFile.OpenRead(modPath);
        if (FindEntry(archive, entryPath) is not { } entry)
            return null;
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    /// <summary>A file in the mod as bytes, or null when it has none of that path</summary>
    public static byte[]? ReadBytes(string modPath, string entryPath)
    {
        if (!IsArchive(modPath))
        {
            var path = PathIn(modPath, entryPath);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        using var archive = ZipFile.OpenRead(modPath);
        if (FindEntry(archive, entryPath) is not { } entry)
            return null;
        using var stream = entry.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>
    /// Writes files into the mod (into a pk3, all of them or, when something fails, none); returns
    /// where the first went: a file's path in a folder, or "mod.pk3: entry" in a pk3
    /// </summary>
    public static string Write(string modPath, params (string EntryPath, byte[] Data)[] files)
        => Change(modPath, files, []);

    /// <summary>Takes files out of the mod (out of a pk3, all of them or, when something fails, none)</summary>
    public static void Delete(string modPath, params string[] entryPaths) => Change(modPath, [], entryPaths);

    /// <summary>
    /// Writes some files into the mod and takes others out, as one change to a pk3 (a rename is
    /// the new file written and the old one taken out); returns where the first written went
    /// </summary>
    public static string Change(string modPath, IReadOnlyList<(string EntryPath, byte[] Data)> files, IReadOnlyList<string> deletes)
    {
        // A file both written and taken out is written
        var removed = deletes.Where(path => !files.Any(file => SamePath(file.EntryPath, path))).ToList();

        if (!IsArchive(modPath))
        {
            // Each written beside the old file first, so a failed write leaves the old one whole
            foreach (var (entryPath, data) in files)
            {
                var path = PathIn(modPath, entryPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temp = path + ".tmp";
                File.WriteAllBytes(temp, data);
                File.Move(temp, path, overwrite: true);
            }
            foreach (var entryPath in removed)
            {
                var path = PathIn(modPath, entryPath);
                if (File.Exists(path))
                    File.Delete(path);
            }
            return files.Count > 0 ? PathIn(modPath, files[0].EntryPath) : modPath;
        }

        // The whole pk3 is written again as a copy, which then takes its place. Each file stays
        // where it was in it: the game loads a pk3's files in that order, and a later file of an
        // asset (actordefs, mapdefs) wins over an earlier one, so moving one could change what wins.
        var copy = modPath + ".tmp";
        try
        {
            using (var source = ZipFile.OpenRead(modPath))
            using (var target = ZipFile.Open(copy, ZipArchiveMode.Create))
            {
                var written = new bool[files.Count];
                foreach (var entry in source.Entries)
                {
                    if (removed.Any(path => SamePath(entry.FullName, path)))
                        continue;

                    // Matched with another case or '\' too (some Windows tools write those)
                    int index = IndexOf(files, entry.FullName);
                    if (index < 0)
                    {
                        CopyEntry(entry, target);
                        continue;
                    }
                    if (written[index])
                        continue;   // a second copy of a file replaced already
                    WriteEntry(target, files[index].EntryPath, files[index].Data);
                    written[index] = true;
                }

                // New files after the rest
                for (int i = 0; i < files.Count; i++)
                {
                    if (!written[i] && !files.Take(i).Any(earlier => SamePath(earlier.EntryPath, files[i].EntryPath)))
                        WriteEntry(target, files[i].EntryPath, files[i].Data);
                }
            }
            File.Replace(copy, modPath, modPath + BackupExtension);
        }
        catch
        {
            File.Delete(copy);
            throw;
        }
        return files.Count > 0 ? $"{modPath}: {files[0].EntryPath}" : modPath;
    }

    /// <summary>A new pk3 holding these files (it mustn't be there already)</summary>
    public static void CreateArchive(string path, IEnumerable<(string EntryPath, byte[] Data)> files)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entryPath, data) in files)
        {
            using var stream = archive.CreateEntry(entryPath.Replace('\\', '/'), CompressionLevel.Optimal).Open();
            stream.Write(data);
        }
    }

    // A file's path in a mod folder, from its path in the mod (maps/MAP01.wad)
    private static string PathIn(string modFolder, string entryPath)
        => Path.Combine(modFolder, entryPath.Replace('/', Path.DirectorySeparatorChar));

    private static int IndexOf(IReadOnlyList<(string EntryPath, byte[] Data)> files, string entryPath)
    {
        for (int i = 0; i < files.Count; i++)
        {
            if (SamePath(files[i].EntryPath, entryPath))
                return i;
        }
        return -1;
    }

    /// <summary>Whether two paths in a mod are the same file (case and '\' or '/' aside)</summary>
    public static bool SamePath(string a, string b)
        => a.Replace('\\', '/').Equals(b.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

    private static void WriteEntry(ZipArchive archive, string entryPath, byte[] data)
    {
        using var stream = archive.CreateEntry(entryPath.Replace('\\', '/'), CompressionLevel.Optimal).Open();
        stream.Write(data);
    }

    // An entry as it was: its name (as stored), contents and time
    private static void CopyEntry(ZipArchiveEntry entry, ZipArchive target)
    {
        var copy = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);
        copy.LastWriteTime = entry.LastWriteTime;
        using var from = entry.Open();
        using var to = copy.Open();
        from.CopyTo(to);
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string entryPath)
    {
        var wanted = entryPath.Replace('\\', '/');
        return archive.Entries.FirstOrDefault(entry => entry.FullName.Replace('\\', '/').Equals(wanted, StringComparison.OrdinalIgnoreCase));
    }
}
