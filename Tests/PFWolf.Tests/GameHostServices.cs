namespace PFWolf.Tests;

/// <summary>
/// GameHost's managers, built once before any test (in the tests' namespace, so before every one). The
/// console commands' tests need them (GameHost.ConsoleCommandList), and once built they're the
/// ones GameHost's other statics are made from (GameHost.InstallScriptApi's scripts, say): built
/// here, every test sees the same, whichever run or in whatever order.
/// </summary>
[SetUpFixture]
public class GameHostServices
{
    [OneTimeSetUp]
    public void BuildThem() => GameHost.ConsoleCommandList();
}
