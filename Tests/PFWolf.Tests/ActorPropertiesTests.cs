using System.Text.RegularExpressions;
using PFWolf.Definitions;

namespace PFWolf.Tests;

/// <summary>
/// The actor properties registry (Definitions/ActorProperties.cs) against the engine's source: a
/// property the code reads is listed there, and one listed there is read. Properties are
/// free-form in the YAML, so the registry is the only list of them the documentation has.
/// </summary>
[Category("Content")]
public class ActorPropertiesTests
{
    // A property name passed to one of the ways the code reads properties (the first string
    // argument), or a constant holding one (FactionManager.PropertyName)
    private static readonly Regex PropertyRead = new(
        @"(?:(?<![\w.])Int|GetIntProperty|GetStringProperty|GetProperty|TryGetProperty|PropertyInts?|PropertyStrings|PropertyBool|PropertyDouble|PropertyText|RuntimeActorProperty|PlayActorSound|playActorSound|ClassFace|(?<!Has)Flag|PlayerFactor|PickupText|PickupInt|PickupFlag|Properties\s*\.\s*(?:TryGetValue|ContainsKey|GetValueOrDefault|Remove))\s*\([^;""]*?""(?<name>[A-Za-z][\w.\-]*)""" +
        @"|const string \w*Propert\w*\s*=\s*""(?<name>[\w.\-]+)""");

    // Names those calls take that aren't actor properties: Lua's actor methods (GameScripts.cs),
    // a frame modifier, and a save's "Load"
    private static readonly HashSet<string> NotProperties = new(StringComparer.OrdinalIgnoreCase) { "hasflag", "distanceto", "bright", "Load" };

    private static IEnumerable<(string File, string Text)> EngineSource()
    {
        foreach (var project in new[] { "PFWolf", "PFWolf.Common" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(TestPaths.RepoRoot(), project), "*.cs", SearchOption.AllDirectories))
            {
                var path = file.Replace('\\', '/');
                if (!path.Contains("/bin/") && !path.Contains("/obj/") && !path.EndsWith("/ActorProperties.cs"))
                    yield return (Path.GetFileName(file), File.ReadAllText(file));
            }
        }
    }

    [Test]
    public void Every_Property_The_Code_Reads_Is_Listed()
    {
        // Act
        var unlisted = EngineSource()
            .SelectMany(source => PropertyRead.Matches(source.Text).Select(m => (source.File, Name: m.Groups["name"].Value)))
            .Where(read => read.Name.Any(char.IsLower) && !NotProperties.Contains(read.Name))     // all capitals: an actor flag
            .Where(read => ActorProperties.Find(read.Name) == null)
            .Select(read => $"{read.Name} ({read.File})")
            .Distinct()
            .ToList();

        // Assert
        Assert.That(unlisted, Is.Empty, "add these to Definitions/ActorProperties.cs");
    }

    [Test]
    public void Every_Listed_Property_Is_Read_By_The_Code()
    {
        // Arrange
        var source = string.Concat(EngineSource().Select(s => s.Text));

        // Act: each name appears in the source as a string ("light.intensity"); a pattern's names are made up as it runs
        var unread = ActorProperties.All
            .Where(p => !p.IsPattern && !source.Contains($"\"{p.Name}\"", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .ToList();

        // Assert
        Assert.That(unread, Is.Empty, "nothing reads these: take them out of Definitions/ActorProperties.cs");
    }

    [Test]
    public void Property_Names_Are_Unique()
    {
        // Act
        var names = ActorProperties.All.Select(p => p.Name.ToLowerInvariant()).ToList();

        // Assert
        Assert.That(names, Is.Unique);
    }

    [TestCase("health.normal", "health.SKILL")]
    [TestCase("HEALTH.hard", "health.SKILL")]
    [TestCase("health", "health")]
    [TestCase("Light.Intensity", "light.intensity")]
    public void Find_Matches_Names_And_Patterns_Ignoring_Case(string name, string found)
    {
        // Act
        var property = ActorProperties.Find(name);

        // Assert
        Assert.That(property?.Name, Is.EqualTo(found));
    }
}
