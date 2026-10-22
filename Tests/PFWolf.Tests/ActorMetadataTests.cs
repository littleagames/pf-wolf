using PFWolf.Assets;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class ActorMetadataTests
{
    private const string Yaml = """
        Inventory:
          radius: 32
          flags: [PICKUP]
          properties:
            "inventory.maxamount": 1
            "inventory.interhubamount": 1
          states:
            Hide:
              - sprite: TNT1
                frames: [A]
                tics-per-frame: -1
        Key:
          parent: Inventory
          properties:
            "inventory.interhubamount": 0
        GoldKey:
          parent: Key
          flags: [COUNTITEM]
          states:
            Spawn:
              - sprite: GKEY
                frames: [A]
                tics-per-frame: -1
        Monster: {}
        Guard:
          parent: Monster
          properties:
            "monster.hitpoints": 25
          states:
            Spawn:
              - sprite: GARD
                frames: [A]
                tics-per-frame: 5
        """;

    [Test]
    public void CreateActor_Inherits_Properties_Nearest_First()
    {
        // Arrange
        var metadata = ActorDefs.Metadata(Yaml);

        // Act
        var key = metadata.CreateActor("GoldKey", metadata.Actors["GoldKey"]);

        // Assert: Key's interhubamount beats Inventory's
        Assert.That(key.Properties["inventory.interhubamount"].ToString(), Is.EqualTo("0"));
        Assert.That(key.Properties["inventory.maxamount"].ToString(), Is.EqualTo("1"));
    }

    [TestCase("Drum", 32)]
    [TestCase("LyingDrum", 32)]       // none of its own: its parent's
    [TestCase("SmallDrum", 16)]       // its own, over its parent's
    [TestCase("Lamp", 20)]            // its own, over a root's none
    [TestCase("TallLamp", 20)]        // its parent's, the nearest that gives one
    public void CreateActor_Takes_The_Nearest_Radius(string className, int radius)
    {
        // Arrange
        var metadata = ActorDefs.Metadata("""
            Decoration: {}
            Drum:
              radius: 32
            LyingDrum:
              parent: Drum
            SmallDrum:
              parent: Drum
              radius: 16
            Lamp:
              parent: Decoration
              radius: 20
            TallLamp:
              parent: Lamp
            """);

        // Act
        var actor = metadata.CreateActor(className, metadata.Actors[className]);

        // Assert
        Assert.That(actor.Radius, Is.EqualTo(radius));
    }

    [TestCase(false, "B", 5)]          // a jump (A_Jump, A_ReFire, self:jump) lands on the state's first frame
    [TestCase(true, "C", 5 + 7)]       // SetState, as id's NewState from an action: on from its second, with the first's tics
    public void A_State_Changed_From_An_Action_Lands_As_The_Change_Says(bool setState, string frame, int tics)
    {
        // Arrange: Spawn's action sends the actor to Away as its frame ends
        var action = setState ? "Test_SetStateAway" : "Test_JumpAway";
        ActorActionRegistry.Register(action, actor =>
        {
            if (setState)
                actor.SetState("Away");
            else
                actor.JumpTo(actor.ResolvedStates["Away"]);
        });
        var metadata = ActorDefs.Metadata($$"""
            Thing:
              states:
                Spawn:
                  - { sprite: THNG, frames: [A], tics-per-frame: 1, action: {{action}} }
                Away:
                  - { sprite: THNG, frames: [B], tics-per-frame: 5 }
                  - { sprite: THNG, frames: [C], tics-per-frame: 7 }
            """);
        var thing = metadata.CreateActor("Thing", metadata.Actors["Thing"]);

        // Act
        thing.TicCount = 0;
        thing.AdvanceFrames();

        // Assert
        Assert.That((thing.CurrentState!.FrameLetter, (int)thing.TicCount), Is.EqualTo((frame, tics)));
    }

    [Test]
    public void CreateActor_Inherits_Flags_And_States()
    {
        // Arrange
        var metadata = ActorDefs.Metadata(Yaml);

        // Act
        var key = metadata.CreateActor("GoldKey", metadata.Actors["GoldKey"]);

        // Assert
        Assert.That(key.Flags, Is.SupersetOf(new[] { "COUNTITEM", "PICKUP" }));
        Assert.That(key.ResolvedStates.Keys, Is.SupersetOf(new[] { "Spawn", "Hide" }));
        Assert.That(key.Name, Is.EqualTo("GoldKey"));
    }

    [Test]
    public void CreateActor_Starts_On_Spawn()
    {
        // Arrange
        var metadata = ActorDefs.Metadata(Yaml);

        // Act
        var guard = metadata.CreateActor("Guard", metadata.Actors["Guard"]);

        // Assert
        Assert.That(guard.CurrentState, Is.SameAs(guard.ResolvedStates["Spawn"]));
        Assert.That(guard.TicCount, Is.EqualTo(5));
    }

    [Test]
    public void CreateActor_Makes_The_Nearest_Csharp_Class()
    {
        // Arrange
        var metadata = ActorDefs.Metadata(Yaml);

        // Act
        var guard = metadata.CreateActor("Guard", metadata.Actors["Guard"]);
        var key = metadata.CreateActor("Key", metadata.Actors["Key"]);

        // Assert
        Assert.That(guard, Is.InstanceOf<Monster>());
        Assert.That(key, Is.InstanceOf<Key>());
    }

    [Test]
    public void CreateActor_Refuses_A_Missing_Parent()
    {
        // Arrange
        var metadata = ActorDefs.Metadata("Orphan:\n  parent: Nobody\n");

        // Act / Assert
        Assert.Throws<Exception>(() => metadata.CreateActor("Orphan", metadata.Actors["Orphan"]));
    }

    [Test]
    public void CreateActor_Refuses_A_Parent_Cycle()
    {
        // Arrange
        var metadata = ActorDefs.Metadata("A:\n  parent: B\nB:\n  parent: A\n");

        // Act / Assert
        Assert.Throws<Exception>(() => metadata.CreateActor("A", metadata.Actors["A"]));
    }

    [Test]
    public void A_Later_Class_Replaces_One_Of_The_Same_Name()
    {
        // Arrange
        var metadata = ActorDefs.Metadata(Yaml, """
            Guard:
              parent: Monster
              properties:
                "monster.speed": 3
            """);

        // Act / Assert
        Assert.That(metadata.TryGetProperty("Guard", "monster.hitpoints", out _), Is.False);
        Assert.That(metadata.GetIntProperty("Guard", "monster.speed", 0), Is.EqualTo(3));
    }

    [Test]
    public void Extend_Changes_The_Class_Loaded_Before_It()
    {
        // Arrange
        var metadata = ActorDefs.Metadata(Yaml, """
            Guard:
              extend: true
              flags: [AMBUSH]
              properties:
                "monster.speed": 3
                "monster.hitpoints": 50
            """);

        // Act
        var guard = metadata.Actors["Guard"];

        // Assert
        Assert.That(guard.Parent, Is.EqualTo("Monster"));
        Assert.That(guard.States.Keys, Does.Contain("Spawn"));
        Assert.That(guard.Flags, Does.Contain("AMBUSH"));
        Assert.That(metadata.GetIntProperty("Guard", "monster.hitpoints", 0), Is.EqualTo(50));
        Assert.That(metadata.GetIntProperty("Guard", "monster.speed", 0), Is.EqualTo(3));
    }

    [Test]
    public void Extend_Of_A_New_Class_Just_Adds_It()
    {
        // Arrange
        var metadata = ActorDefs.Metadata("New:\n  extend: true\n  radius: 10\n");

        // Act / Assert
        Assert.That(metadata.Actors["New"].Radius, Is.EqualTo(10));
    }
}
