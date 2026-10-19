using PFWolf.Entities.Actors;
using PFWolf.Enums;
using PFWolf.SaveGames;

namespace PFWolf.Tests;

/// <summary>What a save keeps of the player's gun, latch, ride, stealth and kill chain, the timed switches, and actors' height and suspicion</summary>
public class PlayerSaveStateTests
{
    private static T RoundTrip<T>(Action<BinaryWriter> write, Func<BinaryReader, T> read)
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            write(bw);
        ms.Position = 0;
        using var br = new BinaryReader(ms);
        var value = read(br);
        Assert.That(ms.Position, Is.EqualTo(ms.Length), "read all that was written");
        return value;
    }

    [Test]
    public void The_Player_Comes_Back_On_The_Gun_Latched_And_Riding()
    {
        // Arrange: manning the second actor, the fourth latched on, in a boat, unseen, a chain of two
        var actors = Enumerable.Range(0, 4).Select(i => (Entities.Actors.Actor)new Monster { Name = $"A{i}" }).ToList();
        var player = new Entities.PlayerState
        {
            Mount = actors[1], MountGun = "NestGun", MountFrom = "Pistol", MountMap = "CAMP04",
            Latch = actors[3], LatchGun = "DogStruggle", LatchFrom = "Knife", LatchMap = "CAMP04", LatchStrikes = 2, MoveSlowdown = 4,
            Vehicle = "Boat", VehicleMap = "CAMP01S", VehicleSpeed = 1200, VehicleThrottle = 40,
            Undetected = false, StealthKills = 3, SuspicionMeter = 50, SuspicionPending = 20,
            KillChain = 2, KillChainAt = 345,
        };
        var timed = new Gameplay.TimedSwitch(5, 6, true, 101, 102, 7, ControlDir.North, ["A_CloseDoor"], new ushort[1, 1]) { TicsLeft = 90 };

        // Act: saved, read back, and put on a fresh player over the level's restored actors
        var saved = RoundTrip(bw => PlayerSaveState.Capture(player, actors, [timed]).Write(bw), PlayerSaveState.Read);
        var restoredActors = Enumerable.Range(0, 4).Select(i => (Entities.Actors.Actor)new Monster { Name = $"A{i}" }).ToList();
        var loaded = new Entities.PlayerState();
        saved.ApplyTo(loaded, restoredActors);

        // Assert
        Assert.That(loaded.Mount, Is.SameAs(restoredActors[1]));
        Assert.That((loaded.MountGun, loaded.MountFrom, loaded.MountMap), Is.EqualTo(("NestGun", "Pistol", "CAMP04")));
        Assert.That(loaded.Latch, Is.SameAs(restoredActors[3]));
        Assert.That((loaded.LatchGun, loaded.LatchStrikes, loaded.MoveSlowdown), Is.EqualTo(("DogStruggle", 2, 4)));
        Assert.That((loaded.Vehicle, loaded.VehicleMap, loaded.VehicleSpeed, loaded.VehicleThrottle), Is.EqualTo(("Boat", "CAMP01S", 1200, 40)));
        Assert.That((loaded.Undetected, loaded.StealthKills, loaded.SuspicionMeter, loaded.SuspicionPending), Is.EqualTo((false, 3, 50, 20)));
        Assert.That((loaded.KillChain, loaded.KillChainAt), Is.EqualTo((2, 345)));
        Assert.That(saved.TimedSwitches, Has.Count.EqualTo(1));
        var sw = saved.TimedSwitches[0];
        Assert.That((sw.X, sw.Y, sw.Patch, sw.From, sw.To, sw.Tag, sw.Dir, sw.TicsLeft), Is.EqualTo((5, 6, true, 101, 102, (ushort)7, ControlDir.North, 90)));
        Assert.That(sw.Actions, Is.EqualTo(new[] { "A_CloseDoor" }));
    }

    [Test]
    public void Nothing_Manned_Latched_Or_Ridden_Stays_So()
    {
        var saved = RoundTrip(bw => PlayerSaveState.Capture(new Entities.PlayerState(), [], []).Write(bw), PlayerSaveState.Read);
        var loaded = new Entities.PlayerState { Mount = new Monster { Name = "Old" }, Vehicle = "Boat" };
        saved.ApplyTo(loaded, []);

        Assert.That((loaded.Mount, loaded.MountGun, loaded.Latch, loaded.Vehicle), Is.EqualTo(((Entities.Actors.Actor?)null, (string?)null, (Entities.Actors.Actor?)null, (string?)null)));
        Assert.That(loaded.Undetected, Is.True);
        Assert.That(loaded.MoveSlowdown, Is.EqualTo(1));
    }

    [Test]
    public void An_Actors_Height_Fall_And_Suspicion_Are_Kept()
    {
        // Arrange: a grenade in the air, an officer waiting for papers
        var grenade = new Monster { Name = "ThrownGrenade", Z = 12.5, ZSpeed = -0.75 };
        var officer = new Monster { Name = "Officer", Suspicion = 300 };

        // Act
        var grenadeBack = new Monster { Name = "ThrownGrenade" };
        RoundTrip(bw => ActorSnapshot.Capture(grenade).Write(bw), ActorSnapshot.Read).ApplyTo(grenadeBack);
        var officerBack = new Monster { Name = "Officer" };
        RoundTrip(bw => ActorSnapshot.Capture(officer).Write(bw), ActorSnapshot.Read).ApplyTo(officerBack);

        // Assert
        Assert.That((grenadeBack.Z, grenadeBack.ZSpeed), Is.EqualTo((12.5, -0.75)));
        Assert.That(officerBack.Suspicion, Is.EqualTo(300));
    }
}
