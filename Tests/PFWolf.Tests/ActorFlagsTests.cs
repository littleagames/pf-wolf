using System.Text.RegularExpressions;
using PFWolf.Definitions;

namespace PFWolf.Tests;

/// <summary>
/// The actor flags registry (Definitions/ActorFlags.cs) against the engine's source: a flag the
/// code checks is listed there, and one listed there is checked. Flags are free-form in the YAML,
/// so the registry is the only list of them the documentation has.
/// </summary>
[Category("Content")]
public class ActorFlagsTests
{
    // A flag checked by name, HasFlag("SOLID") or Flags.Contains("SOLID"), or through a constant
    // (HasFlag(WallSprites.WallSpriteFlag))
    private static readonly Regex ByName = new(@"(?:HasFlag|Flags\s*\.\s*Contains)\s*\(\s*""(?<name>[A-Za-z][\w.]*)""");
    private static readonly Regex ByConstant = new(@"(?:HasFlag|Flags\s*\.\s*Contains)\s*\(\s*(?:\w+\.)*(?<id>[A-Za-z_]\w*)\s*[,)]");
    private static readonly Regex Constant = new(@"const\s+string\s+(?<id>\w+)\s*=\s*""(?<value>[^""]+)""");

    private static List<(string File, string Text)> EngineSource() =>
        [.. new[] { "PFWolf", "PFWolf.Common" }
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(TestPaths.RepoRoot(), project), "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Replace('\\', '/').Contains("/bin/") && !file.Replace('\\', '/').Contains("/obj/") && !file.EndsWith("ActorFlags.cs"))
            .Select(file => (Path.GetFileName(file), File.ReadAllText(file)))];

    [Test]
    public void Every_Flag_The_Code_Checks_Is_Listed()
    {
        // Arrange: the string constants, which a check can name from any file
        var source = EngineSource();
        var constants = new Dictionary<string, string>();
        foreach (var (_, text) in source)
            foreach (Match m in Constant.Matches(text))
                constants.TryAdd(m.Groups["id"].Value, m.Groups["value"].Value);

        // Act: a variable (a weapon's weapon.spares) isn't a constant, and is left alone
        var checks = source.SelectMany(s => ByName.Matches(s.Text).Select(m => (s.File, Name: m.Groups["name"].Value))
            .Concat(ByConstant.Matches(s.Text)
                .Where(m => constants.ContainsKey(m.Groups["id"].Value))
                .Select(m => (s.File, Name: constants[m.Groups["id"].Value]))));
        var unlisted = checks.Where(c => ActorFlags.Find(c.Name) == null).Select(c => $"{c.Name} ({c.File})").Distinct().ToList();

        // Assert
        Assert.That(unlisted, Is.Empty, "add these to Definitions/ActorFlags.cs");
    }

    [Test]
    public void Every_Listed_Flag_Is_Checked_By_The_Code()
    {
        // Arrange
        var source = string.Concat(EngineSource().Select(s => s.Text));

        // Act
        var unchecked_ = ActorFlags.All.Where(f => !source.Contains($"\"{f.Name}\"", StringComparison.OrdinalIgnoreCase)).Select(f => f.Name).ToList();

        // Assert
        Assert.That(unchecked_, Is.Empty, "nothing checks these: take them out of Definitions/ActorFlags.cs");
    }

    [Test]
    public void Flag_Names_Are_Unique()
    {
        // Act
        var names = ActorFlags.All.Select(f => f.Name.ToUpperInvariant()).ToList();

        // Assert
        Assert.That(names, Is.Unique);
    }

    [TestCase("solid", "SOLID")]
    [TestCase("~AMBUSH", "AMBUSH")]
    [TestCase("bright", "BRIGHT")]
    public void Find_Ignores_Case_And_A_Leading_Tilde(string name, string found)
    {
        // Act
        var flag = ActorFlags.Find(name);

        // Assert
        Assert.That(flag?.Name, Is.EqualTo(found));
    }
}
