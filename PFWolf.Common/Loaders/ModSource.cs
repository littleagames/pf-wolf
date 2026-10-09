using PFWolf.Assets;

namespace PFWolf.Loaders;

/// <summary>
/// A mod's modinfo.yaml, at the root of its pk3 or folder. Every field is optional.
/// </summary>
public record ModInfo
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
public class ModSource
{
    private const string ModInfoFileName = "modinfo.yaml";

    private const string GamePackInfoFileName = "gamepack-info.yaml";

    private ModSource(string fullPath, IAssetSource source, ModInfo info, GamePackInfoAsset? games)
    {
        FullPath = fullPath;
        Source = source;
        Info = info;
        Games = games;
    }

    public string FullPath { get; }
    public IAssetSource Source { get; }
    public ModInfo Info { get; }

    /// <summary>
    /// The games the mod adds, from the gamepack-info.yaml at its root (a standalone game is one
    /// with base-pack: standalone), or null when it adds none
    /// </summary>
    public GamePackInfoAsset? Games { get; }

    /// <summary>Whether the mod adds a game of its own</summary>
    public bool HasGames => Games is { GamePacks.Count: > 0 };

    /// <summary>The game packs the mod loads in: modinfo's game-packs, else the games it adds, else null (any)</summary>
    public IReadOnlyCollection<string>? ForGamePacks
        => Info.GamePacks is { Count: > 0 } ? Info.GamePacks : HasGames ? Games!.GamePacks.Keys : null;

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
    /// Whether the mod can load in this game pack: its modinfo.yaml names it or a pack it's built
    /// on (see GamePackList.Includes), or names none. A mod that adds games loads only in them
    /// (unless its modinfo says otherwise): a standalone game's files would make a mess of another's.
    /// </summary>
    public bool IsForGamePack(string gamePackId, IReadOnlyList<string> basePackIds)
        => GamePackList.Includes(ForGamePacks, gamePackId, basePackIds);

    /// <summary>
    /// Every pk3, zip and folder in the mods folder, by name
    /// </summary>
    public static IEnumerable<string> FindInModsFolder()
    {
        if (!Directory.Exists(ModsFolder))
            return [];

        return Directory.EnumerateDirectories(ModsFolder)
            .Concat(Directory.EnumerateFiles(ModsFolder).Where(file =>
                Path.GetExtension(file).Equals(".pk3", StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Opens a mod, or gives null (with the reason in warnings) when it can't be read
    /// </summary>
    public static ModSource? TryOpen(string fullPath, List<string> warnings)
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

        return new ModSource(fullPath, source, info, ReadGames(source, warnings));
    }

    /// <summary>The games a mod's root gamepack-info.yaml adds, or null when it has none or it can't be read</summary>
    public static GamePackInfoAsset? ReadGames(IAssetSource source, List<string> warnings)
    {
        var path = source.EntryPaths.FirstOrDefault(entry => entry.Equals(GamePackInfoFileName, StringComparison.OrdinalIgnoreCase));
        if (path == null)
            return null;

        try
        {
            return new GamePackInfoAsset(YamlDataEntryLoader.Read<Dictionary<string, GamePack>>(source.Open(path)) ?? []);
        }
        catch (Exception e)
        {
            warnings.Add($"Mod '{source.Name}': {GamePackInfoFileName} can't be read ({e.Message}); it adds no games");
            return null;
        }
    }

    /// <summary>
    /// Opens the mods named on the command line that add games, quietly leaving out any that
    /// can't be opened (loading them later says why)
    /// </summary>
    public static List<ModSource> OpenGameMods(IEnumerable<string> modPaths)
    {
        var mods = new List<ModSource>();
        foreach (var modPath in modPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            if (ResolvePath(modPath) is { } fullPath && TryOpen(fullPath, []) is { HasGames: true } mod)
                mods.Add(mod);
        }
        return mods;
    }
}
