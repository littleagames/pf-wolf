using System.Text;
using PFWolf.Extensions;

namespace PFWolf.SaveGames;

/*
=============================================================================

                                SAVE GAME FILES

Every *.dat in the save folder is a save; there are no slots. A save file is:

    byte[4]   "PFWS"
    int       format version
    int       header length, then the header (SaveInfo: name, when, where, progress)
              -- the load/save menus list saves from this alone
    int, int  thumbnail width and height, then that many palette indices
    int       body length, then the body
    int       checksum of the body

This reads and writes the file around the body; the body itself (the game, the
level as it stands, the weapon and the hubs) is Program.SaveGame.cs's.

=============================================================================
*/

internal static class SaveFile
{
    internal const string SaveExtension = ".dat";
    internal const string QuickSaveFile = "quicksave" + SaveExtension;
    internal const string AutoSaveFile = "autosave" + SaveExtension;

    private static readonly byte[] SaveSignature = "PFWS"u8.ToArray();
    // Still in development, so a layout change bumps SaveVersion and older saves are refused
    // rather than converted. 6: the header (SaveInfo) and thumbnail ahead of the body, no slots.
    // 7: the mods loaded when it was saved, at the end of the header.
    // 8: a fourth map plane, wall heights moved from plane 2 to it (plane 2 is flats).
    // 9: the area count (mapdefs floors) ahead of the area tables.
    // 10: the player class, after the skill in gamestate.
    // 11: armor points and percent, after health in gamestate.
    // 12: a fifth map plane (tags), each actor's tag after its tile, and whether each door is
    //     held open by a switch.
    // 13: a sixth map plane (light zones).
    // 14: the light zones' lights, tints, fades and effects, at the end.
    // 15: each actor's light as A_SetLight / A_LightOff left it, after its runtime flags.
    // 16: whether each door has been unlocked (doors.yaml takes-key), after held.
    // 17: actors' Temp3, Ammo, SeekX/Y and TryDir; the weapon charge.
    // 18: the light zones before the weapon; the hub state (levels kept, floors visited) at the end.
    // 19: each actor's carried drops, death link and cloak (mapdefs floors actor-codes), after its light.
    // 20: a count of moving pushwalls and each one's state, in place of the one pushwall.
    // 21: after the actors, when tiles last changed (animation starts) and A_ChangeTexture's swaps.
    // 22: each actor's faction, after its cloak; after the actors, each one's target and shooter.
    // 23: each actor's own translation, after its faction.
    // 24: each tile's wall patch, at the end of the level (plane 0 is saved without them).
    internal const int SaveVersion = 24;
    private const int OldestLoadableSaveVersion = SaveVersion;

    // Anything claiming more than this is garbage
    private const int MaxThumbnailPixels = 1024 * 1024;

    /// <summary>
    /// A path in <paramref name="directory"/> for a new save, named for when it was made (the
    /// save's own name is inside the file, so it can be anything).
    /// </summary>
    internal static string NewPath(string directory, DateTime now)
    {
        var stem = Path.Combine(directory, $"save_{now:yyyyMMdd_HHmmss}");
        var path = stem + SaveExtension;
        for (int i = 2; File.Exists(path); i++)
            path = $"{stem}_{i}{SaveExtension}";
        return path;
    }

    /// <summary>A time in tics as m:ss, or h:mm:ss from an hour up.</summary>
    internal static string FormatPlayTime(int tics)
    {
        var time = TimeSpan.FromSeconds(Math.Max(tics, 0) / 70);
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time:mm\\:ss}" : $"{time.Minutes}:{time:ss}";
    }

    /// <summary>Every save in <paramref name="directory"/> that this build can read, newest first.</summary>
    internal static List<SaveInfo> List(string directory)
    {
        var saves = new List<SaveInfo>();
        try
        {
            if (!Directory.Exists(directory))
                return saves;

            foreach (var path in Directory.EnumerateFiles(directory, "*" + SaveExtension))
            {
                if (ReadInfo(path) is { } info)
                    saves.Add(info);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't list the saves in {directory}: {e.Message}");
        }

        saves.Sort((a, b) => b.SavedAt.CompareTo(a.SavedAt));
        return saves;
    }

    /// <summary>The save's header, or null (having said why) if it isn't a save this build reads.</summary>
    internal static SaveInfo? ReadInfo(string path)
    {
        try
        {
            using var br = new BinaryReader(File.OpenRead(path));
            ReadSaveVersion(br);
            using var header = new BinaryReader(new MemoryStream(ReadExactly(br, br.ReadCount())));
            return SaveInfo.Read(header) with { Path = path };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            Console.WriteLine($"Skipping {path}: {e.Message}");
            return null;
        }
    }

    /// <summary>Checks the signature and reads the format version, refusing one this build can't read.</summary>
    private static int ReadSaveVersion(BinaryReader br)
    {
        if (!br.ReadBytes(SaveSignature.Length).AsSpan().SequenceEqual(SaveSignature))
            throw new InvalidDataException("It isn't a PFWolf save game.");

        var version = br.ReadInt32();
        if (version < OldestLoadableSaveVersion || version > SaveVersion)
            throw new InvalidDataException($"It was saved in format {version}; this build reads {OldestLoadableSaveVersion} to {SaveVersion}.");
        return version;
    }

    /// <summary>
    /// Writes a save. Written to a temporary file first, so a failed save never destroys the one
    /// it was replacing; the failure (an IOException or UnauthorizedAccessException) is thrown.
    /// </summary>
    internal static void Write(string path, SaveInfo info, SaveThumbnail? thumbnail, byte[] body)
    {
        using var header = new MemoryStream();
        using (var bw = new BinaryWriter(header, Encoding.UTF8, leaveOpen: true))
            info.Write(bw);
        var headerBytes = header.ToArray();

        var tempPath = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var bw = new BinaryWriter(File.Create(tempPath)))
            {
                bw.Write(SaveSignature);
                bw.Write(SaveVersion);
                bw.Write(headerBytes.Length);
                bw.Write(headerBytes);
                bw.Write(thumbnail?.Width ?? 0);
                bw.Write(thumbnail?.Height ?? 0);
                if (thumbnail != null)
                    bw.Write(thumbnail.Pixels);
                bw.Write(body.Length);
                bw.Write(body);
                bw.Write(DoChecksum(body, 0));
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(tempPath); } catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    /// <summary>
    /// Reads a save's body, and whether its checksum matched. <paramref name="info"/> is the
    /// header as soon as it's been read, so it's there even when the rest can't be. Throws
    /// IOException (EndOfStreamException for a truncated file), InvalidDataException or
    /// FormatException (a garbled string length) for a save that can't be read.
    /// </summary>
    internal static (byte[] Body, bool ChecksumOk) ReadBody(string path, out SaveInfo? info)
    {
        info = null;
        using var br = new BinaryReader(File.OpenRead(path));
        ReadSaveVersion(br);
        // The header is for the menus, and for saying whether it was saved with other mods
        using (var header = new BinaryReader(new MemoryStream(ReadExactly(br, br.ReadCount()))))
            info = SaveInfo.Read(header);
        ReadThumbnail(br);

        var body = br.ReadBytes(br.ReadCount());
        bool checksumOk = br.BaseStream.Length - br.BaseStream.Position >= sizeof(int)
            && br.ReadInt32() == DoChecksum(body, 0);
        return (body, checksumOk);
    }

    // BinaryReader.ReadBytes quietly returns fewer bytes at the end of the stream.
    internal static byte[] ReadExactly(BinaryReader br, int count)
    {
        var bytes = br.ReadBytes(count);
        if (bytes.Length != count)
            throw new EndOfStreamException();
        return bytes;
    }

    private static SaveThumbnail? ReadThumbnail(BinaryReader br)
    {
        int width = br.ReadInt32(), height = br.ReadInt32();
        if (width < 0 || height < 0 || (long)width * height > MaxThumbnailPixels)
            throw new InvalidDataException($"Bad thumbnail size {width}x{height}.");
        var pixels = ReadExactly(br, width * height);
        return width > 0 && height > 0 ? new SaveThumbnail(width, height, pixels) : null;
    }

    /// <summary>
    /// The picture of the game kept with a save, read only when the menu shows it; null if it
    /// has none or can't be read.
    /// </summary>
    internal static SaveThumbnail? ReadThumbnail(SaveInfo save)
    {
        try
        {
            using var br = new BinaryReader(File.OpenRead(save.Path));
            ReadSaveVersion(br);
            ReadExactly(br, br.ReadCount());
            return ReadThumbnail(br);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Console.WriteLine($"Couldn't read the picture in {save.Path}: {e.Message}");
            return null;
        }
    }

    internal static int DoChecksum(byte[] source, int checksum)
    {
        for (int i = 0; i < source.Length - 1; i++)
            checksum += source[i] ^ source[i + 1];

        return checksum;
    }
}
