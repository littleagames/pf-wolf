using PFWolf.Managers;

namespace PFWolf.Tests;

public class ConsoleTokenizeTests
{
    [Test]
    public void Splits_Arguments_On_Whitespace()
    {
        // Act
        var commands = ConsoleManager.Tokenize("give  ammo\t50");

        // Assert
        Assert.That(commands, Has.Count.EqualTo(1));
        Assert.That(commands[0], Is.EqualTo(new[] { "give", "ammo", "50" }));
    }

    [Test]
    public void Splits_Commands_On_Semicolons()
    {
        // Act
        var commands = ConsoleManager.Tokenize("vid_mode; quit");

        // Assert
        Assert.That(commands, Has.Count.EqualTo(2));
        Assert.That(commands[0], Is.EqualTo(new[] { "vid_mode" }));
        Assert.That(commands[1], Is.EqualTo(new[] { "quit" }));
    }

    [Test]
    public void Quotes_Group_Words_And_Are_Removed()
    {
        // Act
        var commands = ConsoleManager.Tokenize("bind \"Left Ctrl\" attack");

        // Assert
        Assert.That(commands[0], Is.EqualTo(new[] { "bind", "Left Ctrl", "attack" }));
    }

    [Test]
    public void Semicolon_Inside_Quotes_Does_Not_Split()
    {
        // Act
        var commands = ConsoleManager.Tokenize("bind F11 \"save 9 x; quit\"");

        // Assert
        Assert.That(commands, Has.Count.EqualTo(1));
        Assert.That(commands[0], Is.EqualTo(new[] { "bind", "F11", "save 9 x; quit" }));
    }

    [Test]
    public void Empty_Quotes_Are_An_Empty_Argument()
    {
        // Act
        var commands = ConsoleManager.Tokenize("name \"\"");

        // Assert
        Assert.That(commands[0], Is.EqualTo(new[] { "name", "" }));
    }

    [Test]
    public void Quotes_Join_With_Adjacent_Text()
    {
        // Act
        var commands = ConsoleManager.Tokenize("say a\"b c\"d");

        // Assert
        Assert.That(commands[0], Is.EqualTo(new[] { "say", "ab cd" }));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(";;")]
    [TestCase(" ; ; ")]
    public void Blank_Lines_And_Empty_Commands_Are_Dropped(string line)
    {
        // Act
        var commands = ConsoleManager.Tokenize(line);

        // Assert
        Assert.That(commands, Is.Empty);
    }

    [Test]
    public void Unclosed_Quote_Runs_To_The_End()
    {
        // Act
        var commands = ConsoleManager.Tokenize("say \"hello; there");

        // Assert
        Assert.That(commands, Has.Count.EqualTo(1));
        Assert.That(commands[0], Is.EqualTo(new[] { "say", "hello; there" }));
    }
}
