namespace Wolf3D.Loaders;

/// <summary>
/// An unzipped folder laid out like a pk3, so a mod can be tried out without zipping it each time
/// </summary>
internal class DirectoryAssetSource : IAssetSource
{
    private readonly string _rootPath;

    public DirectoryAssetSource(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath);

        EntryPaths = Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories)
            .Where(file => new FileInfo(file).Length > 0)
            .Select(file => Path.GetRelativePath(_rootPath, file).Replace('\\', '/'))
            .ToList();
    }

    public string Name => Path.GetFileName(_rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public IReadOnlyList<string> EntryPaths { get; }

    public MemoryStream Open(string entryPath)
    {
        var filePath = Path.Combine(_rootPath, entryPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Entry {entryPath} not found in {_rootPath}");

        return new MemoryStream(File.ReadAllBytes(filePath));
    }
}
