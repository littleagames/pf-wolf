using System.Text;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>A pk3 or mod held in memory: entry paths to their text</summary>
internal sealed class MemoryAssetSource(string name, Dictionary<string, string> files) : IAssetSource
{
    public string Name { get; } = name;

    public IReadOnlyList<string> EntryPaths { get; } = [.. files.Keys];

    public MemoryStream Open(string entryPath) => new(Encoding.UTF8.GetBytes(files[entryPath]));
}
