using PFWolf.Common.Loaders;

namespace PFWolf.Common.Assets;

public record GamePackDefinitions : Asset
{
    public GamePackDefinitions(string name, Stream data)
    {
        Name = name;
        GamePacks = new YamlDataEntryLoader().Read<Dictionary<string, GamePackDefinitionDataModel>>(data);
        //Title = model.Name;
        //GamePalette = model.GamePalette;
        //StartingScene = model.StartingScene;

        //// These will load in as separate assets
        //MapDefinitions = model.MapDefinitions;
        //GamePackAssetMapping = model.GamePackAssetMapping;
    }

    public Dictionary<string, GamePackDefinitionDataModel> GamePacks { get; } = [];
}

public record GamePackDefinitionDataModel
{
    /// <summary>
    /// Title of the game pack (will be displayed on title and in other places)
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Identifier that inherits its properties from another Game Pack
    /// </summary>
    public string? BasePack { get; set; }

    /// <summary>
    /// Default game palette used for the game pack
    /// </summary>
    public string? GamePalette { get; init; }

    /// <summary>
    /// Scene that is first loaded in for the game to start
    /// </summary>
    public string? StartingScene { get; init; }

    /// <summary>
    /// Path to the asset that maps map plane values to walls, objects, floors/ceilings, etc
    /// </summary>
    public string? MapDefinitions { get; init; }

    /// <summary>
    /// Path to the asset map that defines names of the asset within a game pack
    /// </summary>
    public string? GamePackAssetMapping { get; init; }
}