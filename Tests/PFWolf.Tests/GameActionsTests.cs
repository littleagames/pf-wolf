using PFWolf.Definitions;
using PFWolf.Entities;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

/// <summary>
/// The actions registry (Definitions/GameActions.cs) against what the engine registers: every
/// think and action an actor state can run, and every switch action, is listed there, where it's
/// run from and the class it needs as the engine has them
/// </summary>
public class GameActionsTests
{
    // Less the Test_ names other tests register (the registries are shared, ActionRegistryTests)
    private static List<string> Engine(IEnumerable<string> names)
    {
        GameHost.RegisterActorActions();
        return [.. names.Where(name => !name.StartsWith("Test_", StringComparison.Ordinal))];
    }

    [Test]
    public void The_Actor_Actions_Are_The_Engines()
    {
        // Act
        var engine = Engine(ActorActionRegistry.Names);
        var listed = GameActions.All.Where(a => a.Use.HasFlag(ActionUse.Actor)).Select(a => a.Name);

        // Assert
        Assert.That(listed, Is.EquivalentTo(engine), "Definitions/GameActions.cs's actor actions (ActionUse.Actor)");
    }

    [Test]
    public void The_Switch_Actions_Are_The_Engines()
    {
        // Act
        var engine = Engine(MapTriggerRegistry.Names);
        var listed = GameActions.All.Where(a => a.Use.HasFlag(ActionUse.Switch)).Select(a => a.Name);

        // Assert
        Assert.That(listed, Is.EquivalentTo(engine), "Definitions/GameActions.cs's switch actions (ActionUse.Switch)");
    }

    [Test]
    public void Each_Needs_The_Class_The_Engine_Does()
    {
        // Act
        var engine = Engine(ActorActionRegistry.Names);
        var wrong = engine
            .Select(name => (Name: name, Engine: ActorActionRegistry.NeededClass(name)?.Name, Listed: GameActions.Find(name)?.Needs))
            .Where(a => a.Engine != a.Listed)
            .Select(a => $"{a.Name}: the engine needs {a.Engine ?? "any actor"}, listed {a.Listed ?? "any actor"}")
            .ToList();

        // Assert
        Assert.That(wrong, Is.Empty);
    }

    [Test]
    public void Only_Actor_Actions_Need_A_Class()
    {
        // Act
        var wrong = GameActions.All.Where(a => a.Needs != null && !a.Use.HasFlag(ActionUse.Actor)).Select(a => a.Name);

        // Assert
        Assert.That(wrong, Is.Empty);
    }

    [Test]
    public void Names_Are_Unique_And_Signatures_Start_With_Them()
    {
        // Act
        var names = GameActions.All.Select(a => a.Name).ToList();
        var badSignatures = GameActions.All.Where(a => a.Signature != a.Name && !a.Signature.StartsWith(a.Name + "(", StringComparison.Ordinal)).Select(a => a.Name);

        // Assert
        Assert.That(names, Is.Unique);
        Assert.That(badSignatures, Is.Empty);
    }

    [Test]
    public void The_Scripts_Trigger_Actions_Are_The_Listed_Switch_Actions()
    {
        // Act
        var listed = GameActions.All.Where(a => a.Use.HasFlag(ActionUse.Switch)).Select(a => a.Name);

        // Assert
        Assert.That(Scripting.ScriptApi.TriggerActions.Concat(Scripting.ScriptApi.OwnTileActions), Is.EquivalentTo(listed));
    }
}
