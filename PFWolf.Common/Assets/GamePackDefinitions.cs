using PFWolf.Common.DataLoaders;

namespace PFWolf.Common.Assets;

public record GamePackDefinitions : Asset
{
    public GamePackDefinitions()
    {
    }

    public GamePackDefinitions(Stream data, string pk3Path) : this()
    {
        var packs = YamlDataEntryLoader.Read<Dictionary<string, GamePackDefinitionDataModel>>(data);
        foreach (var pack in packs)
        {
            pack.Value.Name = pack.Key;
        }
        GamePacks = packs.ToDictionary(x => x.Key, x => (Source: pk3Path, Definition: x.Value));
    }

    public Dictionary<string, (string Source, GamePackDefinitionDataModel Definition)> GamePacks { get; } = [];

    internal void AddGamePacks(Dictionary<string, (string Source, GamePackDefinitionDataModel Definitions)> gamePacks)
    {
        foreach (var gamePack in gamePacks)
        {
            GamePacks[gamePack.Key] = gamePack.Value;
        }
    }

    internal bool HasGamePack(string gamePackName, out InitializedGamePack gamePack)
    {
        if (GamePacks.TryGetValue(gamePackName, out var foundGamePack))
        {
            gamePack = new InitializedGamePack(gamePackName, GamePacks);
            return true;
        }

        gamePack = null!;
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
}