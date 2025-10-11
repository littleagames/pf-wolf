using CSharpFunctionalExtensions;
using PFWolf.Common.Assets;

namespace PFWolf.Common;

public class InitializedGamePack
{
    public InitializedGamePack(
        Maybe<string> selectedGamePack,
        Dictionary<string, (string Source, GamePackDefinitionDataModel Definition)> gamePacks)
    {
        if (selectedGamePack.HasNoValue)
        {
            throw new ArgumentNullException(nameof(selectedGamePack), "A game pack must be selected.");
        }

        if (!gamePacks.TryGetValue(selectedGamePack.Value, out var gamePackDefinition))
        {
            throw new InvalidDataException($"The selected game pack '{selectedGamePack}' was not found in any gamepack-info.");
        }

        var definition = gamePackDefinition.Definition;

        if (!string.IsNullOrWhiteSpace(definition.BasePack))
        {
            if (definition.BasePack.Equals(selectedGamePack.Value, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new InvalidDataException($"'{selectedGamePack.Value}' contains base pack '{definition.BasePack}' which is a circular reference to itself.");
            }

            // Initialize the game pack without its own base pack to avoid infinite recursion
            _basePack = new InitializedGamePack(definition.BasePack, gamePacks.Where(x => x.Key != selectedGamePack.Value).ToDictionary());
        }

        Name = definition.Name;
        Title = definition.Title;
        _pk3FilePath = gamePackDefinition.Source;

        _gamePalette = definition.GamePalette;
        _startingScene = definition.StartingScene;
        _gamePackAssetReference = definition.GamePackAssetReference;

        // map definitions are paths to assets, so we also want to have the pk3 source with it
        _mapDefinitions = definition.MapDefinitions
            .Select(md => new KeyValuePair<string, string>(gamePackDefinition.Source, md))
            .ToList();
    }



    public string Name { get; set; } = null!;
    /// <summary>
    /// Title of the game pack (will be displayed on title and in other places)
    /// </summary>
    public string? Title { get; init; }
    private string _pk3FilePath = null!;
    private InitializedGamePack? _basePack = null!;

    public (string Source, string PackName) FindPack(string gamePackName)
    {
        if (string.Equals(Name, gamePackName, StringComparison.InvariantCultureIgnoreCase))
        {
            return (_pk3FilePath, Name);
        }

        if (_basePack is not null)
        {
            return _basePack.FindPack(gamePackName);
        }

        throw new InvalidDataException($"Game pack '{gamePackName}' was not found in the inheritance chain.");
    }

    public string FindNameFromAssetReferenceMap(string assetReferenceMap)
    {
        // Check if the current pack's GamePackAssetReference matches
        if (!string.IsNullOrWhiteSpace(_gamePackAssetReference) &&
            string.Equals(_gamePackAssetReference, assetReferenceMap, StringComparison.InvariantCultureIgnoreCase))
        {
            return Name;
        }

        // Recursively check the base pack if it exists
        if (_basePack is not null)
        {
            return _basePack.FindNameFromAssetReferenceMap(assetReferenceMap);
        }

        // Not found in this pack or any base pack
        throw new InvalidDataException($"Asset reference map '{assetReferenceMap}' was not found in the inheritance chain.");
    }

    private string? _gamePalette = null;
    public string? GamePalette
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_gamePalette))
                return _gamePalette;
            return _basePack?.GamePalette;
        }
    }

    private string? _startingScene = null;
    public string? StartingScene
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_startingScene))
                return _startingScene;
            return _basePack?.StartingScene;
        }
    }

    private List<KeyValuePair<string, string>> _mapDefinitions = [];
    public List<KeyValuePair<string, string>> MapDefinitions
    {
        get
        {
            if (_mapDefinitions.Count > 0)
                return _mapDefinitions;

            return _basePack?.MapDefinitions ?? [];
        }
    }

    private string? _gamePackAssetReference = null;
    public string? GamePackAssetReference
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_gamePackAssetReference))
                return _gamePackAssetReference;
            return _basePack?.GamePackAssetReference;
        }
    }
}
