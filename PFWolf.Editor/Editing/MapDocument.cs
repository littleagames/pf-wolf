using PFWolf.Assets;

namespace PFWolf.Editor.Editing;

/// <summary>One tile of one plane changing value</summary>
public readonly record struct TileChange(int Plane, int Index, ushort Before, ushort After);

/// <summary>The level's size and planes before and after a resize</summary>
public sealed record MapResize(ushort OldWidth, ushort OldHeight, ushort[][] OldPlanes, ushort NewWidth, ushort NewHeight, ushort[][] NewPlanes);

/// <summary>The level's extra things (MapAsset.ExtraThings) before and after a change to them</summary>
public sealed record ExtraThingsChange(IReadOnlyList<MapThing> Before, IReadOnlyList<MapThing> After);

/// <summary>
/// Changes made together, undone together: a paint stroke, a fill, a paste; a resize, which
/// swaps the whole level's size and planes; or a change to its extra things
/// </summary>
public sealed record EditStep(string Description, IReadOnlyList<TileChange> Changes, MapResize? Resize = null, ExtraThingsChange? Extras = null);

/// <summary>
/// A level being edited: its own copy of the planes, with undo and redo. Nothing changes the
/// level the assets loaded; saving writes the copy out.
/// </summary>
public sealed class MapDocument
{
    private readonly List<EditStep> _undo = [];
    private readonly List<EditStep> _redo = [];

    // How many steps the undo list held when the level was last saved (or loaded); -1 when no
    // amount of undoing gets back there
    private int _savedAt;

    /// <param name="isNew">A level made in the editor, which is unsaved until it's written out</param>
    /// <param name="properties">Its game-info entry as loaded; null when game-info doesn't list it</param>
    public MapDocument(string assetName, MapAsset map, bool isNew = false, MapProperties? properties = null)
    {
        AssetName = assetName;
        Map = map.DeepCopy();
        if (isNew)
            _savedAt = -1;

        InGameInfo = properties != null;
        LoadedProperties = SavedProperties = Properties = properties ?? new MapProperties();
    }

    /// <summary>Whether game-info lists the level (the game can't finish one it doesn't)</summary>
    public bool InGameInfo { get; private set; }

    /// <summary>The level's game-info entry as the game loaded it, before any editing</summary>
    public MapProperties LoadedProperties { get; }

    /// <summary>The level's game-info entry as last saved (or loaded)</summary>
    public MapProperties SavedProperties { get; private set; }

    /// <summary>The level's game-info entry as edited</summary>
    public MapProperties Properties { get; private set; }

    public void SetProperties(MapProperties properties)
    {
        Properties = properties;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The game-info keys saving writes: what differs from the loaded entry or the last save;
    /// for a level game-info doesn't list yet, its name too, so that it does
    /// </summary>
    public IReadOnlyList<string> PropertyKeysToWrite()
    {
        var keys = Properties.ChangedKeys(LoadedProperties).Union(Properties.ChangedKeys(SavedProperties)).ToList();
        if (!InGameInfo && !keys.Contains("name"))
            keys.Add("name");
        return keys;
    }

    public bool PropertiesDirty => Properties.ChangedKeys(SavedProperties).Count > 0;

    internal void MarkPropertiesSaved()
    {
        SavedProperties = Properties;
        InGameInfo = true;
    }

    /// <summary>The level's asset name ("map01"): its file is maps/MAP01.wad</summary>
    public string AssetName { get; }

    public MapAsset Map { get; }

    public int Width => Map.Width;
    public int Height => Map.Height;
    public int Planes => Map.MapData.Length;

    /// <summary>The mod (a folder or a pk3) the level was last saved to (or came from), or null for none yet</summary>
    public string? SaveFolder { get; set; }

    public bool IsDirty => _savedAt != _undo.Count || PropertiesDirty || !InGameInfo || _formatChanged;

    // The save format was changed since the last save
    private bool _formatChanged;

    /// <summary>The format the level saves in (MapFiles.Save): UWMF text, or binary planes</summary>
    public void SetFormat(MapFormat format)
    {
        if (Map.Format == format)
            return;
        Map.Format = format;
        _formatChanged = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? UndoDescription => _undo.Count > 0 ? _undo[^1].Description : null;
    public string? RedoDescription => _redo.Count > 0 ? _redo[^1].Description : null;

    /// <summary>The planes changed: an edit, an undo or a redo</summary>
    public event EventHandler? Changed;

    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public ushort this[int plane, int x, int y] => Map.MapData[plane][y * Width + x];

    /// <summary>Starts an edit; its changes show as they're made and become one undo step when it's committed</summary>
    public MapEdit BeginEdit(string description) => new(this, description);

    internal void Apply(int plane, int index, ushort value)
    {
        Map.MapData[plane][index] = value;
    }

    internal void Commit(EditStep step)
    {
        if (step.Changes.Count == 0 && step.Resize == null && step.Extras == null)
            return;

        // A step after undoing can never get back to a save made before the undo
        if (_savedAt > _undo.Count)
            _savedAt = -1;
        _undo.Add(step);
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Makes the level <paramref name="width"/> x <paramref name="height"/> tiles, its planes now
    /// <paramref name="planes"/>, as one undo step
    /// </summary>
    /// <param name="extras">The extra things as they are on the resized level (moved with it); null: as they were</param>
    public void Resize(string description, int width, int height, ushort[][] planes, IReadOnlyList<MapThing>? extras = null)
    {
        var resize = new MapResize(Map.Width, Map.Height, Map.MapData, (ushort)width, (ushort)height, planes);
        var extrasChange = extras == null ? null : new ExtraThingsChange([.. Map.ExtraThings], [.. extras]);
        SetSize(resize.NewWidth, resize.NewHeight, resize.NewPlanes);
        if (extras != null)
            Map.ExtraThings = [.. extras];
        Commit(new EditStep(description, [], resize, extrasChange));
    }

    /// <summary>
    /// The level's extra things (things the object plane can't hold: off a tile's middle, a
    /// second on a tile) are now <paramref name="after"/>, as one undo step back to
    /// <paramref name="before"/>; nothing when they're the same. A level with any saves as UWMF.
    /// </summary>
    public void ChangeExtraThings(string description, IReadOnlyList<MapThing> before, IReadOnlyList<MapThing> after)
    {
        Map.ExtraThings = [.. after];
        if (before.SequenceEqual(after))
        {
            RaiseChanged();
            return;
        }
        Commit(new EditStep(description, [], Extras: new ExtraThingsChange([.. before], [.. after])));
    }

    /// <summary>Shows the extra things as <paramref name="things"/> without an undo step (a drag in progress)</summary>
    public void PreviewExtraThings(IReadOnlyList<MapThing> things)
    {
        Map.ExtraThings = [.. things];
        RaiseChanged();
    }

    private void SetSize(ushort width, ushort height, ushort[][] planes)
    {
        Map.Width = width;
        Map.Height = height;
        Map.MapData = planes;
    }

    public void Undo()
    {
        if (_undo.Count == 0)
            return;

        var step = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        for (int i = step.Changes.Count - 1; i >= 0; i--)
            Map.MapData[step.Changes[i].Plane][step.Changes[i].Index] = step.Changes[i].Before;
        if (step.Resize is { } resize)
            SetSize(resize.OldWidth, resize.OldHeight, resize.OldPlanes);
        if (step.Extras is { } extras)
            Map.ExtraThings = [.. extras.Before];
        _redo.Add(step);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_redo.Count == 0)
            return;

        var step = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        if (step.Resize is { } resize)
            SetSize(resize.NewWidth, resize.NewHeight, resize.NewPlanes);
        if (step.Extras is { } extras)
            Map.ExtraThings = [.. extras.After];
        foreach (var change in step.Changes)
            Map.MapData[change.Plane][change.Index] = change.After;
        _undo.Add(step);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The level as it is now is what's on disk</summary>
    public void MarkSaved()
    {
        _savedAt = _undo.Count;
        _formatChanged = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// An edit in progress (see <see cref="MapDocument.BeginEdit"/>). Setting a tile twice keeps its
/// first value as the one to undo to, so a stroke over its own path undoes cleanly.
/// </summary>
public sealed class MapEdit : IDisposable
{
    private readonly MapDocument _document;
    private readonly string _description;
    private readonly Dictionary<(int Plane, int Index), (ushort Before, ushort After)> _changes = [];
    private readonly List<(int Plane, int Index)> _order = [];
    private bool _done;

    internal MapEdit(MapDocument document, string description)
    {
        _document = document;
        _description = description;
    }

    public MapDocument Document => _document;

    /// <summary>Sets a tile; one off the map is ignored</summary>
    public void Set(int plane, int x, int y, ushort value)
    {
        if (_done || !_document.Contains(x, y) || plane < 0 || plane >= _document.Planes)
            return;

        int index = y * _document.Width + x;
        var before = _document.Map.MapData[plane][index];
        if (_changes.TryGetValue((plane, index), out var change))
            _changes[(plane, index)] = (change.Before, value);
        else if (before != value)
        {
            _changes[(plane, index)] = (before, value);
            _order.Add((plane, index));
        }
        else
            return;

        _document.Apply(plane, index, value);
    }

    /// <summary>Puts every tile set so far back, leaving the edit open (a line or box being redrawn)</summary>
    public void Revert()
    {
        foreach (var key in _order)
            _document.Apply(key.Plane, key.Index, _changes[key].Before);
        _changes.Clear();
        _order.Clear();
    }

    /// <summary>Shows the changes made so far</summary>
    public void Refresh() => _document.RaiseChanged();

    /// <summary>Makes the changes one undo step</summary>
    public void Commit()
    {
        if (_done)
            return;
        _done = true;

        var changes = _order
            .Select(key => new TileChange(key.Plane, key.Index, _changes[key].Before, _changes[key].After))
            .Where(change => change.Before != change.After)
            .ToList();
        _document.Commit(new EditStep(_description, changes));
        if (changes.Count == 0)
            _document.RaiseChanged();
    }

    /// <summary>Takes the changes back without leaving an undo step</summary>
    public void Cancel()
    {
        if (_done)
            return;
        Revert();
        _done = true;
        _document.RaiseChanged();
    }

    public void Dispose() => Commit();
}
