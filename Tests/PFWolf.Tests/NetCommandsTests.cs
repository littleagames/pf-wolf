namespace PFWolf.Tests;

public class NetCommandsTests
{
    [Test]
    public void Net_Status_Says_When_Not_Playing_With_Others()
    {
        // Arrange
        var console = new ConsoleManager();
        new NetCommands(console, null!, null!, null!, new GameSession()).Register();

        // Act
        console.Execute("net_status");

        // Assert
        Assert.That(console.Scrollback[^1], Is.EqualTo("Not playing with others"));
    }

    [TestCase("net_find")]
    [TestCase("net_portcheck")]
    [TestCase("scoreboard")]
    public void Each_Command_Is_There(string name)
    {
        var console = new ConsoleManager();
        new NetCommands(console, null!, null!, null!, new GameSession()).Register();

        Assert.That(console.TryGetCommand(name, out _), Is.True);
    }
}