using System.Text;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>A pk3 or mod held in memory: entry paths to their text, and to their bytes for binary files</summary>
internal sealed class MemoryAssetSource(string name, Dictionary<string, string> files, Dictionary<string, byte[]>? binaryFiles = null) : IAssetSource
{
    public string Name { get; } = name;

    public IReadOnlyList<string> EntryPaths { get; } = [.. files.Keys, .. binaryFiles?.Keys ?? Enumerable.Empty<string>()];

    public MemoryStream Open(string entryPath)
        => binaryFiles != null && binaryFiles.TryGetValue(entryPath, out var bytes)
            ? new(bytes)
            : new(Encoding.UTF8.GetBytes(files[entryPath]));
}
