using System.Security.Cryptography;

namespace PFWolf.Networking;

/// <summary>
/// What this machine's files hold, for the host to check a joining player against: pfwolf.pk3
/// and the mods, by content. Zips are hashed by what's in them, not their bytes: each build zips
/// pfwolf.pk3 afresh, with that machine's file times, so the same commit built twice differs.
/// </summary>
internal static class ContentHash
{
    /// <summary>The MD5 of the base pk3 and then each mod (a folder's files in name order, each with its path), as hex</summary>
    internal static string Of(string basePk3, IEnumerable<string> modPaths)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        HashFile(md5, basePk3);
        foreach (var modPath in modPaths)
        {
            if (Directory.Exists(modPath))
            {
                foreach (var file in Directory.EnumerateFiles(modPath, "*", SearchOption.AllDirectories)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    md5.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetRelativePath(modPath, file).ToLowerInvariant()));
                    HashFile(md5, file);
                }
            }
            else
                HashFile(md5, modPath);
        }

        return Convert.ToHexString(md5.GetHashAndReset());
    }

    static void HashFile(IncrementalHash hash, string path)
    {
        try
        {
            if (TryHashZip(hash, path))
                return;
            using var stream = File.OpenRead(path);
            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                hash.AppendData(buffer, 0, read);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes($"unreadable {Path.GetFileName(path)}"));
        }
    }

    /// <summary>
    /// Hashes a zip (.pk3/.zip) by its files' names and contents, in name order, leaving out
    /// times, compression and order in the archive: false if the file isn't one
    /// </summary>
    static bool TryHashZip(IncrementalHash hash, string path)
    {
        var ext = Path.GetExtension(path);
        if (!ext.Equals(".pk3", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return false;

        System.IO.Compression.ZipArchive zip;
        try
        {
            zip = System.IO.Compression.ZipFile.OpenRead(path);
        }
        catch (InvalidDataException)
        {
            return false;
        }

        using (zip)
        {
            var buffer = new byte[81920];
            foreach (var entry in zip.Entries
                .Where(e => !e.FullName.EndsWith('/') && !e.FullName.EndsWith('\\'))
                .OrderBy(e => e.FullName.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase))
            {
                hash.AppendData(System.Text.Encoding.UTF8.GetBytes(entry.FullName.Replace('\\', '/').ToLowerInvariant()));
                using var stream = entry.Open();
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    hash.AppendData(buffer, 0, read);
            }
        }
        return true;
    }
}
