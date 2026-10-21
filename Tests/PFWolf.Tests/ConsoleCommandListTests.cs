using System.Text.RegularExpressions;
using PFWolf.DocGen;
using PFWolf.Managers;

namespace PFWolf.Tests;

/// <summary>
/// The console commands as GameHost registers them, which are the documentation's too
/// (PFWolf.DocGen's console page): each says what it does and how it's written, and each name
/// and alias reaches its own command
/// </summary>
public class ConsoleCommandListTests
{
    private static readonly IReadOnlyList<ConsoleCommand> Commands = GameHost.ConsoleCommandList();

    [Test]
    public void There_Are_Commands()
    {
        // Assert
        Assert.That(Commands, Has.Count.GreaterThan(100));
    }

    [Test]
    public void Each_Has_Help_As_A_Sentence()
    {
        // Act
        var bad = Commands.Where(c => !Regex.IsMatch(c.Help, @"^[A-Z0-9`""].*[.)]$")).Select(c => $"{c.Name}: {c.Help}");

        // Assert
        Assert.That(bad, Is.Empty, "help starts with a capital and ends with a full stop");
    }

    [Test]
    public void Each_Usage_Starts_With_Its_Name_And_Its_Brackets_Pair_Up()
    {
        // Act
        var bad = Commands.Where(c => !(c.Usage == c.Name || c.Usage.StartsWith(c.Name + " ", StringComparison.Ordinal)) || !Paired(c.Usage))
            .Select(c => $"{c.Name}: {c.Usage}");

        // Assert
        Assert.That(bad, Is.Empty);
    }

    [Test]
    public void Names_Are_Lower_Case()
    {
        // Act
        var bad = Commands.SelectMany(c => (c.Aliases ?? []).Prepend(c.Name)).Where(name => name != name.ToLowerInvariant());

        // Assert
        Assert.That(bad, Is.Empty);
    }

    [Test]
    public void Each_Name_And_Alias_Reaches_Its_Own_Command()
    {
        // Arrange: a command registered later under another's name or alias takes it over
        var names = Commands.SelectMany(c => (c.Aliases ?? []).Prepend(c.Name).Select(name => (Name: name, Command: c))).ToList();

        // Act
        var taken = names.GroupBy(n => n.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(n => n.Command.Name))}");

        // Assert
        Assert.That(taken, Is.Empty);
    }

    [Test]
    public void The_Documentation_Says_When_A_Command_Can_Run()
    {
        // Arrange
        var map = Commands.Single(c => c.Name == "map");

        // Act
        var text = ConsoleCommandsSchema.Describe(map);

        // Assert
        Assert.That(text, Is.EqualTo("Warps to a level. Also `warp`. A cheat. Only in a level."));
    }

    // Every [ and < closed, in order
    private static bool Paired(string usage)
    {
        var open = new Stack<char>();
        foreach (var c in usage)
        {
            if (c is '[' or '<')
                open.Push(c);
            else if (c is ']' or '>' && (open.Count == 0 || open.Pop() != (c == ']' ? '[' : '<')))
                return false;
        }
        return open.Count == 0;
    }
}
