using System.Text;
using PFWolf.Editor.Editing;
using PFWolf.Managers;

namespace PFWolf.Editor.Definitions;

/// <summary>
/// Saves definition files: into a mod (a folder or a pk3), or in base mode into pfwolf.pk3 itself,
/// in the game folder, so the game has it now, and into the pfwolf-pk3 folder it's built from,
/// when there's one, so the next build doesn't put the old file back
/// </summary>
public static class DefinitionSaver
{
    /// <summary>The folder pfwolf.pk3 is built from (PFWolf.csproj zips it), named so in the repository</summary>
    public const string BaseSourceFolderName = "pfwolf-pk3";

    /// <summary>
    /// The pfwolf-pk3 folder above the game folder (a build's bin folder in the repository), or
    /// null when there's none
    /// </summary>
    public static string? FindBaseSourceFolder(string gameFolder)
    {
        if (string.IsNullOrWhiteSpace(gameFolder))
            return null;
        for (var dir = new DirectoryInfo(gameFolder); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, BaseSourceFolderName);
            if (IsBaseSourceFolder(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>Whether a folder looks like pfwolf.pk3's source (it has gamepacks/gamepack-info.yaml)</summary>
    public static bool IsBaseSourceFolder(string folder)
        => File.Exists(Path.Combine(folder, "gamepacks", "gamepack-info.yaml"));

    /// <summary>Saves a change worked out by DefinitionSet.PlanWrite; returns where it went</summary>
    public static IReadOnlyList<string> Save(DefinitionWrite write, string gameFolder, string? baseSourceFolder)
    {
        var file = write.File;
        if (!file.IsBase)
            return [ModFiles.Write(file.ModPath!, (file.EntryPath, Encode(write.NewText)))];

        var places = new List<string> { ModFiles.Write(Path.Combine(gameFolder, AssetManager.BasePk3FileName), (file.EntryPath, Encode(write.NewText))) };

        // The source file gets the same change made to its own text, which may not be just the same
        if (SourcePath(baseSourceFolder, file.EntryPath) is { } sourcePath && File.Exists(sourcePath))
        {
            var text = File.ReadAllText(sourcePath);
            WriteFile(sourcePath, YamlText.Sync(text, write.Path, write.Value));
            places.Add(sourcePath);
        }
        return places;
    }

    /// <summary>Saves a file's whole text (from the YAML editor); returns where it went</summary>
    public static IReadOnlyList<string> SaveText(DefinitionFile file, string text, string gameFolder, string? baseSourceFolder)
    {
        if (!file.IsBase)
            return [ModFiles.Write(file.ModPath!, (file.EntryPath, Encode(text)))];

        var places = new List<string> { ModFiles.Write(Path.Combine(gameFolder, AssetManager.BasePk3FileName), (file.EntryPath, Encode(text))) };
        if (SourcePath(baseSourceFolder, file.EntryPath) is { } sourcePath && File.Exists(sourcePath))
        {
            WriteFile(sourcePath, text);
            places.Add(sourcePath);
        }
        return places;
    }

    /// <summary>A pfwolf.pk3 file's text in the source folder, or null when it isn't there</summary>
    public static string? ReadSource(string? baseSourceFolder, string entryPath)
        => SourcePath(baseSourceFolder, entryPath) is { } path && File.Exists(path) ? File.ReadAllText(path).TrimStart('﻿') : null;

    private static string? SourcePath(string? baseSourceFolder, string entryPath)
        => string.IsNullOrWhiteSpace(baseSourceFolder) ? null : Path.Combine(baseSourceFolder, entryPath.Replace('/', Path.DirectorySeparatorChar));

    private static byte[] Encode(string text) => new UTF8Encoding(false).GetBytes(text);

    // Beside the old file first, so a failed write leaves it whole
    private static void WriteFile(string path, string text)
    {
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, Encode(text));
        File.Move(temp, path, overwrite: true);
    }
}
