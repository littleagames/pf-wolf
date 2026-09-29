namespace Wolf3D.Loaders;

/// <summary>
/// A mod's modinfo.yaml, at the root of its pk3 or folder. Every field is optional.
/// </summary>
internal record ModInfo
{
    public string? Name { get; init; }
    public string? Version { get; init; }
    public string? Author { get; init; }
    public string? Description { get; init; }

    /// <summary>Game packs the mod is made for ("wolf3d", "spear"); it loads in any when unset</summary>
    public List<string>? GamePacks { get; init; }
}

/// <summary>
/// A mod picked to load over pfwolf.pk3: its pk3, zip or folder, and its modinfo.yaml
/// </summary>
internal class ModSource
{
    private const string ModInfoFileName = "modinfo.yaml";

    private ModSource(string fullPath, IAssetSource source, ModInfo info)
    {
        FullPath = fullPath;
        Source = source;
        Info = info;
    }

    public string FullPath { get; }
    public IAssetSource Source { get; }
    public ModInfo Info { get; }

    /// <summary>modinfo.yaml's name, or else the file or folder name</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Info.Name) ? Source.Name : Info.Name;

    /// <summary>The mods/ folder beside the exe</summary>
    public static string ModsFolder => Path.Combine(AppContext.BaseDirectory, "mods");

    /// <summary>
    /// The full path of a mod named on the command line, when it's there as given. Called before
    /// the working folder can change, since a relative path means the folder the game started in.
    /// Anything else is kept as it is, to be looked for in mods/.
    /// </summary>
    public static string FullPathIfExists(string path)
        => File.Exists(path) || Directory.Exists(path) ? Path.GetFullPath(path) : path;

    /// <summary>
    /// Where a named mod is: as given, then in mods/, then in mods/ with .pk3 added
    /// (so "--file mymod" finds mods/mymod.pk3). Null when it's in none of them.
    /// </summary>
    public static string? ResolvePath(string path)
    {
        var candidates = new List<string> { path };
        if (!Path.IsPathRooted(path))
        {
            candidates.Add(Path.Combine(ModsFolder, path));
            candidates.Add(Path.Combine(ModsFolder, path + ".pk3"));
        }

        var found = candidates.FirstOrDefault(candidate => File.Exists(candidate) || Directory.Exists(candidate));
        return found == null ? null : Path.GetFullPath(found);
    }

    /// <summary>
    /// Opens a mod, or gives null (with the reason in warnings) when it can't be read or its
    /// modinfo.yaml says it's for other game packs
    /// </summary>
    public static ModSource? TryOpen(string fullPath, string gamePackId, List<string> warnings)
    {
        IAssetSource source;
        try
        {
            source = Directory.Exists(fullPath) ? new DirectoryAssetSource(fullPath) : new Pk3AssetSource(fullPath);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Mod '{fullPath}' can't be read, so it isn't loaded: {e.Message}");
            return null;
        }

        var info = new ModInfo();
        var infoPath = source.EntryPaths.FirstOrDefault(path => path.Equals(ModInfoFileName, StringComparison.OrdinalIgnoreCase));
        if (infoPath != null)
        {
            try
            {
                info = YamlDataEntryLoader.Read<ModInfo>(source.Open(infoPath));
            }
            catch (Exception e)
            {
                warnings.Add($"Mod '{source.Name}': {ModInfoFileName} can't be read ({e.Message}); loading it without");
            }
        }

        var mod = new ModSource(fullPath, source, info);
        if (info.GamePacks is { Count: > 0 } packs && !packs.Contains(gamePackId, StringComparer.OrdinalIgnoreCase))
        {
            warnings.Add($"Mod '{mod.DisplayName}' is for {string.Join(", ", packs)}, not {gamePackId}, so it isn't loaded");
            return null;
        }

        return mod;
    }
}
