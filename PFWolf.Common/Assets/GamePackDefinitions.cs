using PFWolf.Common.DataLoaders;

namespace PFWolf.Common.Assets;

public record GamePackDefinitions : Asset
{
    public GamePackDefinitions()
    {
    }

    public GamePackDefinitions(Stream data) : this()
    {
        GamePacks = YamlDataEntryLoader.Read<Dictionary<string, GamePackDefinitionDataModel>>(data);
    }

    public Dictionary<string, GamePackDefinitionDataModel> GamePacks { get; } = [];

    internal void AddGamePacks(Dictionary<string, GamePackDefinitionDataModel> gamePacks)
    {
        // todo: add or overwrite
        foreach (var gamePack in gamePacks)
        {
            gamePack.Value.Name = gamePack.Key;
            GamePacks[gamePack.Key] = gamePack.Value;
        }
    }

    internal bool HasGamePack(string gamePackName, out GamePackDefinitionDataModel foundGamePack)
    {
        if (GamePacks.TryGetValue(gamePackName, out foundGamePack!))
        {
            return true;
        }

        foundGamePack = null!;
        return false;
    }
}

public record GamePackDefinitionDataModel
{
    public string Name { get; set; } = null!;
    /// <summary>
    /// Title of the game pack (will be displayed on title and in other places)
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Identifier that inherits its properties from another Game Pack
    /// </summary>
    public string? BasePack { get; set; }

    /// <summary>
    /// Default game palette used for the game pack
    /// </summary>
    public string? GamePalette { get; private set; }

    /// <summary>
    /// Scene that is first loaded in for the game to start
    /// </summary>
    public string? StartingScene { get; private set; }

    /// <summary>
    /// Path to the asset that maps map plane values to walls, objects, floors/ceilings, etc
    /// </summary>
    public List<string> MapDefinitions { get; init; } = [];

    /// <summary>
    /// Path to the asset map that defines names of the asset within a game pack
    /// </summary>
    public string? GamePackAssetReference { get; private set; } = null;

    internal void DetermineBasePack(
        Dictionary<string, GamePackDefinitionDataModel> gamePacks,
        HashSet<string>? visitedBasePacks = null)
    {
        if (string.IsNullOrWhiteSpace(BasePack))
        {
            return;
        }

        visitedBasePacks ??= new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        if (!visitedBasePacks.Add(BasePack!))
        {
            throw new InvalidDataException(
                $"Circular base pack reference detected: '{Name}' -> '{BasePack}'. Chain: {string.Join(" -> ", visitedBasePacks)}");
        }

        if (BasePack.Equals(Name, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new InvalidDataException($"'{Name}' contains base pack '{BasePack}' which is a circular reference to itself.");
        }

        if (!gamePacks.TryGetValue(BasePack, out var basePack))
        {
            throw new InvalidDataException($"'{Name}' contains base pack '{BasePack}' which was not defined in any gamepack-info");
        }

        // recursively determine base pack
        if (!string.IsNullOrWhiteSpace(basePack.BasePack))
        {
            basePack.DetermineBasePack(gamePacks, visitedBasePacks);
        }

        if (basePack.MapDefinitions.Count > 0)
        {
            // add to beginning of list, so it is read first
            MapDefinitions.InsertRange(0, basePack.MapDefinitions);
        }

        if (string.IsNullOrWhiteSpace(GamePalette))
        {
            GamePalette = basePack.GamePalette;
        }

        if (string.IsNullOrWhiteSpace(StartingScene))
        {
            StartingScene = basePack.StartingScene;
        }

        if (string.IsNullOrWhiteSpace(GamePackAssetReference))
        {
            this.GamePackAssetReference = basePack.GamePackAssetReference;
        }
    }
}