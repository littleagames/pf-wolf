namespace PFWolf.Loaders;

/// <summary>
/// Somewhere assets are read from: pfwolf.pk3, a mod's pk3/zip, or a mod's folder.
/// Entry paths use '/' and are relative to the source's root ("graphics/title.png").
/// </summary>
internal interface IAssetSource
{
    /// <summary>File or folder name, for messages ("pfwolf.pk3")</summary>
    string Name { get; }

    /// <summary>Every non-empty file in the source</summary>
    IReadOnlyList<string> EntryPaths { get; }

    /// <summary>Reads one entry into memory, positioned at its start</summary>
    MemoryStream Open(string entryPath);
}

/// <summary>
/// One file in an asset source
/// </summary>
internal record AssetSourceEntry(IAssetSource Source, string FullName)
{
    /// <summary>File name without its folders ("title.png")</summary>
    public string Name => FullName.Substring(FullName.LastIndexOf('/') + 1);

    public MemoryStream Open() => Source.Open(FullName);
}
