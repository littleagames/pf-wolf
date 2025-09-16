using PFWolf.Common.Loaders;

namespace PFWolf.Common.Assets;

public record MapDefinition : Asset
{
    public MapDefinition(string name, Stream data)
    {
        Name = name;
        Definitions = YamlDataEntryLoader.Read<MapDefinitionDataModel>(data);
    }

    public MapDefinitionDataModel Definitions { get; } = null!;

    public void Include(MapDefinitionDataModel model)
    {
        foreach (var item in model.Walls)
            Definitions.Walls[item.Key] = item.Value;

        foreach (var item in model.Doors)
            Definitions.Doors[item.Key] = item.Value;

        foreach (var item in model.Player)
            Definitions.Player[item.Key] = item.Value;

        foreach (var item in model.MultiPlayer)
            Definitions.MultiPlayer[item.Key] = item.Value;

        foreach (var item in model.Actors)
            Definitions.Actors[item.Key] = item.Value;
    }
}

public record MapDefinitionDataModel
{
    public Dictionary<string, WallDefinitionModel> Walls { get; set; } = [];
    public Dictionary<string, WallDefinitionModel> Doors { get; set; } = [];
    public Dictionary<string, PlayerDefinitionModel> Player { get; set; } = [];
    public Dictionary<string, PlayerDefinitionModel> MultiPlayer { get; set; } = [];
    public Dictionary<string, ActorDefinitionModel> Actors { get; set; } = [];
}

public record WallDefinitionModel
{
    public string? North { get; set; } = null;
    public string? South { get; set; } = null;
    public string? East { get; set; } = null;
    public string? West { get; set; } = null;
}

public record PlayerDefinitionModel : ActorDefinitionModel
{
    public int Health { get; set; } = 0;
    public List<string> Weapons { get; set; } = [];
    public List<int> Ammo { get; set; } = [];
}

public record ActorDefinitionModel
{
    /// <summary>
    /// Reference to the actor definition found in /actordefs
    /// </summary>
    public string Actor { get; set; } = null!;
    public string? State { get; set; } = null;
    public int Angle { get; set; } = 0;
    public string? Direction { get; set; } = null;
    /// <summary>
    /// What skill levels this actor spawn is valid in (e.g. "sk_baby", "sk_easy", "sk_medium", "sk_hard")
    /// </summary>
    public List<string> Skills { get; set; } = [];
    public List<string> Flags { get; set; } = [];
}