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

    /// <summary>The repository's pfwolf-pk3 folder, found by walking up from the test output</summary>
    public static string Pk3SourceFolder()
    {
        for (var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "pfwolf-pk3");
            if (Directory.Exists(candidate))
                return candidate;
        }

        Assert.Ignore("pfwolf-pk3 isn't above the test output folder");
        return "";
    }
}
