using System.Text;
using PFWolf.Editor.Data;
using PFWolf.Editor.Definitions;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.Editing;

/// <summary>A menu's file worked out, ready to save: the mod and file, its new text, and what the editor should say about it</summary>
public sealed record MenuWrite(string ModPath, string EntryPath, string Text, IReadOnlyList<string> Notes);

/// <summary>
/// Saves an edited menu into a mod as a menudefs file laid over the menu (MenuOverlay): only what
/// the mod changes, so the menu takes in what later pfwolf.pk3s change. pfwolf.pk3 is read-only.
/// </summary>
public static class MenuSaver
{
    /// <summary>Where a mod keeps a menu of this name: directly in menudefs/ (a mod has no pack folders)</summary>
    public static string EntryPathFor(string name) => $"menudefs/{name}.yaml";

    /// <summary>
    /// The file that makes the menu <paramref name="edited"/> (menudefs' form) when it's saved into
    /// <paramref name="modPath"/>, laid over the files before that mod's; checked by merging it as the
    /// game does. Throws InvalidDataException when the result wouldn't be the edited menu.
    /// </summary>
    /// <param name="layers">The menu's files as the game loads them (empty for a new menu)</param>
    public static MenuWrite Plan(GameContent content, string name, IReadOnlyList<MenuLayer> layers, YamlMappingNode edited, string modPath)
    {
        if (ModFiles.WhyReadOnly(modPath) is { } why)
            throw new UnauthorizedAccessException(why);

        // The mod's own file of it, where it has one; else a new one
        int at = -1;
        for (int i = layers.Count - 1; i >= 0; i--)
            if (!layers[i].IsBase && SameMod(layers[i].ModPath!, modPath))
            {
                at = i;
                break;
            }
        var entryPath = at >= 0 ? layers[at].EntryPath : EntryPathFor(name);
        var before = (at >= 0 ? layers.Take(at) : layers).ToList();
        var notes = new List<string>();
        if (at >= 0 && at < layers.Count - 1)
            notes.Add($"{string.Join(", ", layers.Skip(at + 1).Select(layer => layer.SourceName))} change the menu after it, so the game may not show it just as saved.");

        YamlMappingNode? under = null;
        if (before.Count > 0)
        {
            var built = MenuCatalog.Merge(content, before);
            under = built.Tree ?? throw new InvalidDataException($"The menu beneath doesn't read: {built.Error}");
        }

        var (overlay, overlayNotes) = MenuOverlay.Plan(under, edited);
        notes.AddRange(overlayNotes);

        var existing = ModFiles.ReadText(modPath, entryPath);
        var text = Header(existing, name, under != null) + MenuOverlay.ToText(overlay);

        // As the game would read it
        var mine = new MenuLayer(Path.GetFileName(modPath.TrimEnd('\\', '/')), modPath, entryPath);
        var check = MenuCatalog.Merge(content, [.. before, mine], layer => layer == mine ? text : null);
        if (check.Tree == null || !YamlNodes.Equal(check.Tree, edited))
            throw new InvalidDataException($"It wouldn't read back as the menu edited{(check.Error != null ? $": {check.Error}" : "")}");
        if (under != null && overlay.Children.Count == 0)
            notes.Add("Nothing differs from the menu beneath, so the file says nothing.");
        return new MenuWrite(modPath, entryPath, text, notes);
    }

    /// <summary>Writes the file into its mod; returns where it went</summary>
    public static string Save(MenuWrite write)
        => ModFiles.Write(write.ModPath, (write.EntryPath, new UTF8Encoding(false).GetBytes(write.Text)));

    // The file's own comments at its top, kept; a new one's says how it's read
    private static string Header(string? existing, string name, bool overlays)
    {
        if (existing != null)
        {
            var lines = existing.TrimStart('﻿').Replace("\r\n", "\n").Split('\n').TakeWhile(line => line.StartsWith('#')).ToList();
            return lines.Count == 0 ? "" : string.Join("\n", lines) + "\n";
        }
        return overlays
            ? $"# Laid over the game's {name} menu: only what this mod changes. Entries of menu-items and\n"
              + "# components are matched by id (remove: true takes one out, before:/after: an id places it).\n"
            : $"# The {name} menu, this mod's own: a CP_Menu item (menu: {name}) opens it.\n";
    }

    private static bool SameMod(string a, string b)
        => string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
}
