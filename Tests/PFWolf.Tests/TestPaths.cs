namespace PFWolf.Tests;

/// <summary>Folders in the repository, found by walking up from the test output</summary>
internal static class TestPaths
{
    /// <summary>The repository's root: the folder holding pfwolf-pk3</summary>
    public static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); dir != null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "pfwolf-pk3")))
                return dir.FullName;
        }

        Assert.Ignore("pfwolf-pk3 isn't above the test output folder");
        return "";
    }

    /// <summary>The pk3's source folder, which the build zips into pfwolf.pk3</summary>
    public static string Pk3SourceFolder() => Path.Combine(RepoRoot(), "pfwolf-pk3");
}
