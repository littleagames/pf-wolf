using System.Diagnostics;
using PFWolf.Editor.Data;

namespace PFWolf.Editor.Editing;

/// <summary>A picture being edited in another program: its file, and what it's in place of</summary>
public sealed record ExternalArt(ArtKind Kind, string AssetName, string FilePath)
{
    public string FileName => Path.GetFileName(FilePath);
}

/// <summary>
/// Pictures opened in a paint program: each written to a PNG in a folder of the editor's own
/// (in the temp folder), opened there, and watched, so a save in that program can be brought
/// into the mod. Paint programs save in several ways (over the file, or a new one renamed over
/// it), several times in a row, so a picture is reported once things go quiet.
/// </summary>
public sealed class ExternalArtEdits : IDisposable
{
    private readonly Dictionary<string, (ExternalArt Art, FileSystemWatcher Watcher)> _watched = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastWrites = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<Action> _onUiThread;
    private readonly object _lock = new();
    private readonly Dictionary<string, Timer> _quiet = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="onUiThread">Runs an action on the UI thread (the watchers report on their own)</param>
    public ExternalArtEdits(string folder, Action<Action> onUiThread)
    {
        Folder = folder;
        _onUiThread = onUiThread;
    }

    /// <summary>How long a file has to be left alone before its save is reported</summary>
    public static readonly TimeSpan QuietTime = TimeSpan.FromMilliseconds(600);

    /// <summary>The folder the pictures are written in</summary>
    public string Folder { get; }

    /// <summary>A picture was saved in the other program (on the UI thread)</summary>
    public event EventHandler<ExternalArt>? Saved;

    public IReadOnlyCollection<ExternalArt> Watched => _watched.Values.Select(watch => watch.Art).ToList();

    /// <summary>The file a picture of this kind and name is written to: KIND-NAME.png</summary>
    public string PathFor(ArtKind kind, string assetName)
        => Path.Combine(Folder, $"{kind.ToString().ToLowerInvariant()}-{assetName.ToUpperInvariant()}.png");

    /// <summary>
    /// Writes a picture's PNG (over one left from before) and watches it; returns where it is. A
    /// picture already being watched keeps its file as it is: what's been drawn there isn't lost.
    /// </summary>
    public ExternalArt Start(ArtKind kind, string assetName, Func<byte[]> png)
    {
        var path = PathFor(kind, assetName);
        if (_watched.TryGetValue(path, out var watched) && File.Exists(path))
            return watched.Art;

        Directory.CreateDirectory(Folder);
        File.WriteAllBytes(path, png());
        var art = new ExternalArt(kind, assetName, path);
        Watch(art);
        return art;
    }

    /// <summary>Opens a file in a program, or else in the one Windows edits its kind of file with (or opens it with)</summary>
    public static void Open(string filePath, string? program)
    {
        if (!string.IsNullOrWhiteSpace(program))
        {
            Process.Start(new ProcessStartInfo(program) { ArgumentList = { filePath }, UseShellExecute = false })?.Dispose();
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(filePath) { Verb = "edit", UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No program edits PNGs: one that opens them
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true })?.Dispose();
        }
    }

    /// <summary>Stops watching a picture, and removes its file</summary>
    public void Stop(ExternalArt art)
    {
        if (!_watched.Remove(art.FilePath, out var watched))
            return;
        watched.Watcher.Dispose();
        try
        {
            File.Delete(art.FilePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Still open in the other program: it's left in the temp folder
        }
    }

    /// <summary>The file's contents, waiting a little while the other program still has it open</summary>
    public static byte[] Read(ExternalArt art)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return File.ReadAllBytes(art.FilePath);
            }
            catch (IOException) when (attempt < 10 && File.Exists(art.FilePath))
            {
                Thread.Sleep(100);
            }
        }
    }

    private void Watch(ExternalArt art)
    {
        var watcher = new FileSystemWatcher(Folder, Path.GetFileName(art.FilePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        watcher.Changed += (_, _) => Touched(art);
        watcher.Created += (_, _) => Touched(art);
        watcher.Renamed += (_, _) => Touched(art);
        watcher.EnableRaisingEvents = true;
        _watched[art.FilePath] = (art, watcher);
        _lastWrites[art.FilePath] = File.GetLastWriteTimeUtc(art.FilePath);
    }

    // A change: reported once the file's been left alone a while
    private void Touched(ExternalArt art)
    {
        lock (_lock)
        {
            if (_quiet.Remove(art.FilePath, out var old))
                old.Dispose();
            _quiet[art.FilePath] = new Timer(_ => _onUiThread(() => Quiet(art)), null, QuietTime, Timeout.InfiniteTimeSpan);
        }
    }

    private void Quiet(ExternalArt art)
    {
        lock (_lock)
        {
            if (_quiet.Remove(art.FilePath, out var timer))
                timer.Dispose();
        }
        if (!_watched.ContainsKey(art.FilePath) || !File.Exists(art.FilePath))
            return;

        // Only a real change (a program touching the file as it opens it isn't one)
        var written = File.GetLastWriteTimeUtc(art.FilePath);
        if (_lastWrites.TryGetValue(art.FilePath, out var last) && written == last)
            return;
        _lastWrites[art.FilePath] = written;
        Saved?.Invoke(this, art);
    }

    public void Dispose()
    {
        foreach (var (_, watcher) in _watched.Values)
            watcher.Dispose();
        _watched.Clear();
        lock (_lock)
        {
            foreach (var timer in _quiet.Values)
                timer.Dispose();
            _quiet.Clear();
        }
    }
}
