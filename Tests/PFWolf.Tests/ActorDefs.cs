using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>Actordefs for tests, read from YAML the way the pk3 loader reads them</summary>
internal static class ActorDefs
{
    public static Dictionary<string, ActorData> Parse(string yaml) =>
        YamlDataEntryLoader.Deserialize<Dictionary<string, ActorData>>(yaml);

    public static ActorMetadata Metadata(params string[] yamlFiles)
    {
        var metadata = new ActorMetadata();
        foreach (var yaml in yamlFiles)
            metadata.AddActors(Parse(yaml));
        return metadata;
    }
}
