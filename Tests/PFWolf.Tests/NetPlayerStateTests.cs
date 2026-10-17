using PFWolf.Entities;
using PFWolf.Entities.Actors;
using PFWolf.Networking;

namespace PFWolf.Tests;

public class NetPlayerStateTests
{
    private static readonly Monster Guard = new() { Name = "Guard" };

    private static PlayerState Played()
    {
        var p = new PlayerState
        {
            Name = "Drew",
            Gone = false,
            PlayerClass = "Soldier",
            Color = "red",
            OldScore = 100,
            Score = 2500,
            NextExtra = 40000,
            Lives = 2,
            Health = 64,
            Armor = 30,
            ArmorPercent = 33,
            Weapon = "Knife",
            ChosenWeapon = "Pistol",
            FaceFrame = 1,
            KillX = 123456,
            KillY = 654321,
            Frags = 7,
            Kills = 12,
            ThrustSpeed = 5000,
            PlUX = 1234,
            PlUY = 4321,
            AngleFrac = 13,
            LastAttacker = Guard,
            FaceCount = 22,
            FaceTimes = 3,
            WeaponCharge = 9,
            Pitch = -12.5,
            EyeZ = 40,
            DeadTics = 15,
            DeathAngle = 270,
        };
        p.Items["Clip"] = 25;
        p.Items["GoldKey"] = 1;
        p.Input.RestoreButtons(0b1010);
        return p;
    }

    // Every color here but none (an empty one), as PlayerColor has it
    private static (PlayerState Read, int LastAttacker, ActorSnapshot? Weapon) RoundTrip(PlayerState p, Func<string, string?>? color = null)
    {
        using var stream = new MemoryStream();
        NetPlayerState.Write(new BinaryWriter(stream), p, actor => ReferenceEquals(actor, Guard) ? 4 : -1);
        stream.Position = 0;
        var read = new PlayerState();
        NetPlayerState.Read(new BinaryReader(stream), read, color ?? (c => c.Length == 0 ? null : c), out var lastAttacker, out var weapon);
        Assert.That(stream.Position, Is.EqualTo(stream.Length), "read all of it");
        return (read, lastAttacker, weapon);
    }

    [Test]
    public void A_Player_Reads_Back_As_They_Were()
    {
        // Arrange
        var played = Played();

        // Act
        var (read, lastAttacker, weapon) = RoundTrip(played);

        // Assert
        Assert.That((read.Name, read.Gone, read.PlayerClass, read.Color), Is.EqualTo(("Drew", false, "Soldier", "red")));
        Assert.That((read.OldScore, read.Score, read.NextExtra, read.Lives), Is.EqualTo((100, 2500, 40000, (short)2)));
        Assert.That((read.Health, read.Armor, read.ArmorPercent), Is.EqualTo(((short)64, (short)30, (short)33)));
        Assert.That((read.Weapon, read.ChosenWeapon, read.FaceFrame), Is.EqualTo(("Knife", "Pistol", (short)1)));
        Assert.That((read.KillX, read.KillY, read.Frags, read.Kills), Is.EqualTo((123456, 654321, 7, 12)));
        Assert.That(read.Items, Is.EquivalentTo(played.Items));
        Assert.That((read.ThrustSpeed, read.PlUX, read.PlUY, read.AngleFrac), Is.EqualTo((5000, (ushort)1234, (ushort)4321, (short)13)));
        Assert.That((read.FaceCount, read.FaceTimes, read.WeaponCharge), Is.EqualTo((22, 3, 9)));
        Assert.That((read.Pitch, read.EyeZ, read.DeadTics, read.DeathAngle), Is.EqualTo((-12.5, 40, 15, (short)270)));
        Assert.That(read.Input.Buttons, Is.EqualTo(0b1010u));
        Assert.That(lastAttacker, Is.EqualTo(4));
        Assert.That(weapon, Is.Null);
    }

    [Test]
    public void Nothing_Left_Out_Reads_Back_As_Nothing()
    {
        // Arrange
        var p = new PlayerState { PlayerClass = "Player" };

        // Act
        var (read, lastAttacker, _) = RoundTrip(p);

        // Assert
        Assert.That((read.Name, read.Color, read.Weapon, read.ChosenWeapon), Is.EqualTo(((string?)null, (string?)null, (string?)null, (string?)null)));
        Assert.That(lastAttacker, Is.EqualTo(-1));
        Assert.That(read.Items, Is.Empty);
    }

    [Test]
    public void A_Color_This_Machine_Doesnt_Have_Is_None()
    {
        var (read, _, _) = RoundTrip(Played(), color => color == "blue" ? color : null);
        Assert.That(read.Color, Is.Null);
    }

    [Test]
    public void The_Weapon_In_Hand_Goes_With_Them()
    {
        // Arrange
        var p = Played();
        p.WeaponSprite = new Monster { Name = "Pistol", TicCount = 5 };

        // Act
        var (_, _, weapon) = RoundTrip(p);

        // Assert
        Assert.That(weapon, Is.Not.Null);
        Assert.That(weapon!.ClassName, Is.EqualTo("Pistol"));
    }
}
