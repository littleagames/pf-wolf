using PFWolf.Assets;
using PFWolf.Editor.Data;

namespace PFWolf.Editor.Editing;

/// <summary>
/// A palette being edited: its 256 colors, undo and redo, and the colors last saved (or loaded),
/// so it knows when it has changes
/// </summary>
public sealed class PaletteDocument
{
    private readonly List<(string Label, PaletteColor[] Before)> _undo = [];
    private readonly List<(string Label, PaletteColor[] Before)> _redo = [];
    private PaletteColor[] _saved;
    // An edit that carries on the one before (a slider being dragged) rather than making its own undo step
    private string? _runningEdit;

    public PaletteDocument(string name, PaletteColor[] colors, string? saveFolder = null)
    {
        Name = name;
        Colors = (PaletteColor[])colors.Clone();
        Loaded = (PaletteColor[])colors.Clone();
        _saved = Loaded;
        SaveFolder = saveFolder;
    }

    public string Name { get; }
    public PaletteColor[] Colors { get; private set; }

    /// <summary>The colors as the game loaded them</summary>
    public PaletteColor[] Loaded { get; }

    /// <summary>The mod (a folder or a pk3) it was loaded from or last saved to; null until one is picked</summary>
    public string? SaveFolder { get; set; }

    public bool IsDirty => !Same(Colors, _saved);
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? UndoLabel => _undo.Count > 0 ? _undo[^1].Label : null;
    public string? RedoLabel => _redo.Count > 0 ? _redo[^1].Label : null;

    /// <summary>Raised after every edit, undo and redo</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Changes the colors. An edit with the same <paramref name="running"/> key as the one just
    /// before it joins that one's undo step (a slider dragged through many values is one step).
    /// </summary>
    public void Edit(string label, Action<PaletteColor[]> edit, string? running = null)
    {
        var before = (PaletteColor[])Colors.Clone();
        edit(Colors);
        if (Same(Colors, before))
            return;

        if (running == null || running != _runningEdit || _undo.Count == 0)
            _undo.Add((label, before));
        _runningEdit = running;
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The next edit starts its own undo step, even with the same running key</summary>
    public void EndRunningEdit() => _runningEdit = null;

    public void Undo()
    {
        if (_undo.Count == 0)
            return;
        var (label, before) = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add((label, Colors));
        Colors = before;
        _runningEdit = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_redo.Count == 0)
            return;
        var (label, after) = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add((label, Colors));
        Colors = after;
        _runningEdit = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Whether two palettes have the same colors</summary>
    public static bool Same(PaletteColor[] a, PaletteColor[] b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Red != b[i].Red || a[i].Green != b[i].Green || a[i].Blue != b[i].Blue)
                return false;
        }
        return true;
    }

    /// <summary>Writes it to {folder}/palettes/NAME.pal and marks it saved; returns the file's path</summary>
    public string Save(string folder)
    {
        var path = PaletteCatalog.Save(folder, Name, Colors);
        SaveFolder = folder;
        _saved = (PaletteColor[])Colors.Clone();
        _runningEdit = null;
        Changed?.Invoke(this, EventArgs.Empty);
        return path;
    }
}
