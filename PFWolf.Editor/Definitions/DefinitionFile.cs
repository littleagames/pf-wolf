using PFWolf.Loaders;
using PFWolf.Managers;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.Definitions;

/// <summary>
/// One YAML file of definitions: in pfwolf.pk3 (a base file) or in a mod (a folder or a pk3), by
/// its path there, with its text and, when it reads, its document
/// </summary>
public sealed class DefinitionFile
{
    /// <summary>actordefs files directly in actordefs/, which every pack shares, rather than a pack's own</summary>
    public const string SharedLayer = "shared";

    public DefinitionFile(string? modPath, string entryPath, string layer, string text, bool isNew = false)
    {
        ModPath = modPath;
        EntryPath = entryPath;
        Layer = layer;
        Text = text.TrimStart('﻿');
        IsNew = isNew;

        try
        {
            Tree = YamlText.ParseRoot(Text);
        }
        catch (YamlException e)
        {
            Error = $"Line {e.Start.Line}, column {e.Start.Column}: {e.Message}";
        }
        catch (InvalidDataException e)
        {
            Error = e.Message;
        }
    }

    /// <summary>The mod the file is in (a folder or a pk3), or null for pfwolf.pk3's own</summary>
    public string? ModPath { get; }

    /// <summary>Its path in pfwolf.pk3 or the mod ("actordefs/wolf3d/guards.yaml", "game-info.yaml")</summary>
    public string EntryPath { get; }

    /// <summary><see cref="SharedLayer"/> for the shared actordefs, else empty</summary>
    public string Layer { get; }

    public string Text { get; }

    /// <summary>Its document; null when it's empty or doesn't read (see <see cref="Error"/>)</summary>
    public YamlMappingNode? Tree { get; }

    /// <summary>Why the file doesn't read, which leaves it out as it does the game; null when it does</summary>
    public string? Error { get; private set; }

    /// <summary>A file the editor is about to make: it isn't in the mod yet</summary>
    public bool IsNew { get; }

    public bool IsBase => ModPath == null;

    /// <summary>"pfwolf.pk3", or the mod's folder or file name</summary>
    public string SourceName => ModPath == null
        ? AssetManager.BasePk3FileName
        : Path.GetFileName(ModPath.TrimEnd('\\', '/'));

    public string Label => $"{SourceName}: {EntryPath}";

    public override string ToString() => Label;

    /// <summary>The same file with new text (as saved)</summary>
    public DefinitionFile WithText(string text) => new(ModPath, EntryPath, Layer, text);

    /// <summary>Notes why the file, though it's YAML, can't be read as what it holds</summary>
    internal void SetReadError(string error) => Error ??= error;

    /// <summary>Whether this is the file at <paramref name="entryPath"/> in <paramref name="modPath"/> (null: pfwolf.pk3)</summary>
    public bool Is(string? modPath, string entryPath)
        => SameMod(ModPath, modPath) && EntryPath.Replace('\\', '/').Equals(entryPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

    public static bool SameMod(string? a, string? b)
    {
        if (a == null || b == null)
            return a == null && b == null;
        return Path.GetFullPath(a).TrimEnd('\\', '/').Equals(Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Reads a file from pfwolf.pk3 or a mod</summary>
    public static DefinitionFile Read(IAssetSource source, string? modPath, string entryPath, string layer = "")
    {
        string text;
        try
        {
            text = YamlDataEntryLoader.ReadText(source.Open(entryPath));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            var unread = new DefinitionFile(modPath, entryPath, layer, "");
            unread.Error = $"It can't be read: {e.Message}";
            return unread;
        }
        return new DefinitionFile(modPath, entryPath, layer, text);
    }
}
