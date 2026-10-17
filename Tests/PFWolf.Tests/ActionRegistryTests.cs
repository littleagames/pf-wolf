using PFWolf.Entities;
using PFWolf.Entities.Actors;
// PFWolf.Actor, the legacy wall/door base class, would win over a plain Actor alias
using EntityActor = PFWolf.Entities.Actors.Actor;

namespace PFWolf.Tests;

/// <summary>
/// The registries are static and shared with the game, so each test registers a name of its own
/// (prefixed Test_) rather than clearing them.
/// </summary>
public class ActionRegistryTests
{
    private static string UniqueName() => "Test_" + Guid.NewGuid().ToString("N");

    [TestCase("A_Look", "A_Look", new string[0])]
    [TestCase("  T_Chase  ", "T_Chase", new string[0])]
    [TestCase("A_Jump()", "A_Jump", new string[0])]
    [TestCase("A_GiveInventory(\"Clip\", 25)", "A_GiveInventory", new[] { "Clip", "25" })]
    [TestCase("A_Say(\"Hello, world\")", "A_Say", new[] { "Hello, world" })]
    [TestCase("A_Spawn(Rocket, Offset(1, 2), 3)", "A_Spawn", new[] { "Rocket", "Offset(1, 2)", "3" })]
    [TestCase("A_Say(\"a (b\", c)", "A_Say", new[] { "a (b", "c" })]
    [TestCase("A_Two( 1 ,  2 )", "A_Two", new[] { "1", "2" })]
    public void Parse_Splits_Name_And_Arguments(string call, string name, string[] args)
    {
        // Act
        var (parsedName, parsedArgs) = ActorActionRegistry.Parse(call);

        // Assert
        Assert.That(parsedName, Is.EqualTo(name));
        Assert.That(parsedArgs, Is.EqualTo(args));
    }

    [Test]
    public void Invoke_Runs_The_Registered_Handler_With_Its_Arguments()
    {
        // Arrange
        var name = UniqueName();
        string[]? received = null;
        EntityActor? receivedActor = null;
        ActorActionRegistry.Register(name, (actor, args) => { receivedActor = actor; received = args; });
        var actor = new EntityActor { Name = "Thing" };

        // Act
        ActorActionRegistry.Invoke($"{name}(\"x\", 2)", actor);

        // Assert
        Assert.That(receivedActor, Is.SameAs(actor));
        Assert.That(received, Is.EqualTo(new[] { "x", "2" }));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Test_NeverRegistered")]
    public void Invoke_Of_Nothing_Or_An_Unknown_Name_Does_Nothing(string? call)
    {
        Assert.DoesNotThrow(() => ActorActionRegistry.Invoke(call, new EntityActor { Name = "Thing" }));
    }

    [Test]
    public void RegisterFor_Skips_Actors_Of_Other_Classes()
    {
        // Arrange
        var name = UniqueName();
        var ran = new List<string>();
        ActorActionRegistry.RegisterFor<Monster>(name, monster => ran.Add(monster.Name));

        // Act
        ActorActionRegistry.Invoke(name, new EntityActor { Name = "Lamp" });
        ActorActionRegistry.Invoke(name, new Monster { Name = "Guard" });

        // Assert
        Assert.That(ran, Is.EqualTo(new[] { "Guard" }));
        Assert.That(ActorActionRegistry.NeededClass($"{name}(1)"), Is.EqualTo(typeof(Monster)));
    }

    [Test]
    public void NeededClass_Is_Null_For_Any_Actor_Handlers()
    {
        // Arrange
        var name = UniqueName();
        ActorActionRegistry.Register(name, _ => { });

        // Act / Assert
        Assert.That(ActorActionRegistry.NeededClass(name), Is.Null);
        Assert.That(ActorActionRegistry.NeededClass(null), Is.Null);
    }

    [Test]
    public void Trigger_Invoke_Returns_What_The_Handler_Says()
    {
        // Arrange
        var fires = UniqueName();
        var blocked = UniqueName();
        TriggerActivation? received = null;
        string[]? receivedArgs = null;
        MapTriggerRegistry.Register(fires, (activation, args) => { received = activation; receivedArgs = args; return true; });
        MapTriggerRegistry.Register(blocked, (_, _) => false);
        var activation = new TriggerActivation(3, 4, default, new EntityActor { Name = "Player" }, 7);

        // Act / Assert
        Assert.That(MapTriggerRegistry.Invoke($"{fires}(5)", activation), Is.True);
        Assert.That(received, Is.SameAs(activation));
        Assert.That(receivedArgs, Is.EqualTo(new[] { "5" }));
        Assert.That(MapTriggerRegistry.Invoke(blocked, activation), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Test_NeverRegistered")]
    public void Trigger_Invoke_Of_Nothing_Or_An_Unknown_Name_Is_False(string? call)
    {
        // Arrange
        var activation = new TriggerActivation(0, 0, default, new EntityActor { Name = "Player" }, 0);

        // Act / Assert
        Assert.That(MapTriggerRegistry.Invoke(call, activation), Is.False);
    }
}
