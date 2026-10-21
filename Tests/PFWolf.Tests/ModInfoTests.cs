using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>modinfo.yaml's id, pfwolf range and dependencies: what a mod is known by, and when it loads</summary>
public class ModInfoTests
{
    private string _folder = "";

    [SetUp]
    public void MakeFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), "pfwolf-modinfo-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void RemoveFolder()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    // A mod folder with this modinfo.yaml
    private ModSource Mod(string folderName, string modinfo)
    {
        var path = Path.Combine(_folder, folderName);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "modinfo.yaml"), modinfo);
        return ModSource.TryOpen(path, [])!;
    }

    [TestCase("0.8.0", "0.8.0", true)]
    [TestCase("0.8.0", "0.9.1", true)]
    [TestCase("0.8.0", "0.7.9", false)]
    [TestCase(">=0.8.0 <0.9.0", "0.8.5", true)]
    [TestCase(">=0.8.0 <0.9.0", "0.9.0", false)]
    [TestCase(">0.8 <=1", "1.0.0", true)]
    [TestCase(">0.8 <=1", "0.8.0", false)]
    [TestCase("=0.8.0", "0.8.0", true)]
    [TestCase("=0.8.0", "0.8.1", false)]
    [TestCase("v0.8.0-alpha", "0.8.0", true)]
    [TestCase(">= 0.8.0 < 0.9.0", "0.8.5", true)]
    [TestCase(">= 0.8.0 < 0.9.0", "0.9.0", false)]
    public void A_Range_Includes_The_Versions_It_Should(string range, string version, bool included)
    {
        // Act
        var read = VersionRange.TryParse(range, out var parsed);

        // Assert
        Assert.That(read, Is.True);
        Assert.That(parsed.Includes(VersionRange.ParseVersion(version)!), Is.EqualTo(included));
    }

    [TestCase("")]
    [TestCase("latest")]
    [TestCase(">=0.8.0 <")]
    [TestCase(">= <0.9")]
    [TestCase("0.8.0.1")]
    [TestCase("~0.8")]
    public void A_Range_That_Isnt_One_Isnt_Read(string range)
    {
        // Act / Assert
        Assert.That(VersionRange.TryParse(range, out _), Is.False);
    }

    [Test]
    public void An_Assemblys_Four_Part_Version_Compares_As_Three()
    {
        // Arrange
        VersionRange.TryParse("=0.8.0", out var range);

        // Act / Assert
        Assert.That(range.Includes(new Version(0, 8, 0, 0)), Is.True);
    }

    [Test]
    public void The_Id_Is_Modinfos_Else_The_File_Or_Folder_Name()
    {
        // Act
        var named = Mod("folder-one", "id: moody-lights\n");
        var unnamed = Mod("moody-two", "name: Moody Two\n");

        // Assert
        Assert.That(named.Id, Is.EqualTo("moody-lights"));
        Assert.That(unnamed.Id, Is.EqualTo("moody-two"));
    }

    [Test]
    public void A_Mod_For_Other_Versions_Warns_And_One_For_This_One_Doesnt()
    {
        // Arrange
        var old = Mod("old", "name: Old\npfwolf: \">=0.5.0 <0.6.0\"\n");
        var current = Mod("current", "pfwolf: 0.8.0\n");
        var unset = Mod("unset", "name: Unset\n");
        var unreadable = Mod("unreadable", "name: Odd\npfwolf: soon\n");
        var engine = new Version(0, 8, 0, 0);

        // Act / Assert
        Assert.That(old.EngineVersionWarning(engine), Is.EqualTo("Mod 'Old' is made for PFWolf >=0.5.0 <0.6.0, not 0.8.0; loading it anyway, but it may not work"));
        Assert.That(current.EngineVersionWarning(engine), Is.Null);
        Assert.That(unset.EngineVersionWarning(engine), Is.Null);
        Assert.That(unreadable.EngineVersionWarning(engine), Does.StartWith("Mod 'Odd': its pfwolf range \"soon\" can't be read"));
    }

    [Test]
    public void A_Mod_Whose_Dependencies_Are_Loaded_Loads_In_Its_Place()
    {
        // Arrange: the dependency after the mod: the order is the player's
        var addon = Mod("addon", "dependencies: [base-mod]\n");
        var baseMod = Mod("base", "id: Base-Mod\n");
        var warnings = new List<string>();

        // Act
        var kept = ModSource.WithDependenciesMet([addon, baseMod], warnings);

        // Assert: ids match whatever their case
        Assert.That(kept, Is.EqualTo(new[] { addon, baseMod }));
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void In_The_Editor_A_Mod_Missing_A_Dependency_Is_Kept_And_Warned_About()
    {
        // Arrange: an add-on opened without the mod it builds on
        var addon = Mod("addon", "name: Addon\ndependencies: [base-mod]\n");
        var warnings = new List<string>();

        // Act
        var kept = ModSource.WithDependenciesMet([addon], warnings, leaveOut: false);

        // Assert
        Assert.That(kept, Is.EqualTo(new[] { addon }));
        Assert.That(warnings, Is.EqualTo(new[] { "Mod 'Addon' needs 'base-mod', which isn't loaded" }));
    }

    [Test]
    public void Two_Mods_With_One_Id_Are_Warned_About()
    {
        // Arrange: an old folder and a new pk3 of the same mod, by its id
        var old = Mod("elite-old", "name: Elite Old\nid: elite-guards\n");
        var current = Mod("elite-new", "name: Elite New\nid: Elite-Guards\n");
        var warnings = new List<string>();

        // Act
        var kept = ModSource.WithDependenciesMet([old, current], warnings);

        // Assert: both load, as before
        Assert.That(kept, Is.EqualTo(new[] { old, current }));
        Assert.That(warnings, Is.EqualTo(new[] { "Mods 'Elite Old' and 'Elite New' all have the id 'elite-guards', so a mod that needs it can't tell them apart" }));
    }

    [Test]
    public void A_Mod_Missing_A_Dependency_Is_Left_Out_And_So_Is_One_That_Needs_It()
    {
        // Arrange
        var first = Mod("first", "name: First\ndependencies: [nowhere, nor-here]\n");
        var second = Mod("second", "name: Second\ndependencies: [first]\n");
        var other = Mod("other", "name: Other\n");
        var warnings = new List<string>();

        // Act
        var kept = ModSource.WithDependenciesMet([second, first, other], warnings);

        // Assert
        Assert.That(kept, Is.EqualTo(new[] { other }));
        Assert.That(warnings, Is.EqualTo(new[]
        {
            "Mod 'First' needs 'nowhere' and 'nor-here', which aren't loaded, so it isn't loaded either",
            "Mod 'Second' needs 'first', which isn't loaded, so it isn't loaded either",
        }));
    }
}
