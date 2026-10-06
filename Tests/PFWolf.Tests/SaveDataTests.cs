using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

/// <summary>The pieces of a save game that write and read themselves</summary>
public class SaveDataTests
{
    private static T RoundTrip<T>(Action<BinaryWriter> write, Func<BinaryReader, T> read)
    {
        using var stream = new MemoryStream();
        using (var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            write(bw);
        stream.Position = 0;
        using var br = new BinaryReader(stream);
        var result = read(br);
        Assert.That(stream.Position, Is.EqualTo(stream.Length), "everything written should be read");
        return result;
    }

    private static ActorSnapshot FullSnapshot() => new()
    {
        ClassName = "Guard",
        IsPlayer = false,
        StateName = "Chase",
        StateFrame = 2,
        TicCount = 7,
        Active = activetypes.ac_yes,
        Dir = objdirtypes.northwest,
        Angle = 135,
        Hitpoints = 25,
        Speed = 512,
        Distance = -1,
        Temp1 = 1,
        Temp2 = -2,
        Temp3 = 3,
        Ammo = 4,
        SeekX = 5,
        SeekY = 6,
        TryDir = 7,
        Hidden = true,
        AreaNumber = 12,
        X = 0x123456,
        Y = 0x654321,
        TileX = 18,
        TileY = 101 & 63,
        Tag = 9,
        RuntimeFlags = Program.objflags.FL_SHOOTABLE | Program.objflags.FL_ATTACKMODE,
        Light = new LightOverride(false, 200, 2.5, "#FFD080"),
        CarriedDrops = ["GoldKey", "Clip"],
        DeathLink = 3,
        Cloaked = true,
    };

    [Test]
    public void ActorSnapshot_Round_Trips()
    {
        // Arrange
        var snapshot = FullSnapshot();

        // Act
        var read = RoundTrip(snapshot.Write, ActorSnapshot.Read);

        // Assert
        Assert.That(read.CarriedDrops, Is.EqualTo(snapshot.CarriedDrops));
        Assert.That(read with { CarriedDrops = snapshot.CarriedDrops }, Is.EqualTo(snapshot));
    }

    [Test]
    public void ActorSnapshot_Round_Trips_Without_State_Or_Light()
    {
        // Arrange
        var snapshot = FullSnapshot() with { StateName = null, Light = null, CarriedDrops = [] };

        // Act
        var read = RoundTrip(snapshot.Write, ActorSnapshot.Read);

        // Assert
        Assert.That(read.StateName, Is.Null);
        Assert.That(read.Light, Is.Null);
        Assert.That(read.CarriedDrops, Is.Empty);
    }

    [Test]
    public void LightOverride_Round_Trips_With_No_Color()
    {
        // Arrange
        var light = new LightOverride(true, 0, 0, null);

        // Act
        var read = RoundTrip(light.Write, LightOverride.Read);

        // Assert
        Assert.That(read, Is.EqualTo(light));
    }

    [TestCase(-1, 1.0)]
    [TestCase(256, 1.0)]
    [TestCase(100, -0.5)]
    [TestCase(100, double.NaN)]
    public void LightOverride_Read_Rejects_Out_Of_Range_Values(int intensity, double radius)
    {
        // Arrange
        var bytes = Bytes(bw =>
        {
            bw.Write(false);
            bw.Write(intensity);
            bw.Write(radius);
            bw.Write("");
        });

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => LightOverride.Read(new BinaryReader(new MemoryStream(bytes))));
    }

    [Test]
    public void ZoneState_Round_Trips()
    {
        // Arrange
        var zone = new ZoneState
        {
            Light = 120,
            Color = "#203040",
            Effect = ZoneEffect.Pulse,
            LowSetting = 30,
            TicsSetting = null,
            BrightTics = 9,
            FadeFrom = 255,
            FadeLeft = 5,
            FadeTotal = 10,
            Phase = 11,
            FlickerAmount = 12,
        };

        // Act
        var read = RoundTrip(zone.Write, ZoneState.Read);

        // Assert
        Assert.That(
            (read.Light, read.Color, read.Effect, read.LowSetting, read.TicsSetting, read.BrightTics,
             read.FadeFrom, read.FadeLeft, read.FadeTotal, read.Phase, read.FlickerAmount),
            Is.EqualTo((120, "#203040", ZoneEffect.Pulse, (int?)30, (int?)null, 9, 255, 5, 10, 11, 12)));
    }

    [Test]
    public void ZoneState_Read_Rejects_An_Unknown_Effect()
    {
        // Arrange
        var bytes = Bytes(new ZoneState().Write);
        bytes[sizeof(int) + 1] = 99;    // after the light and the "has a color" flag

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => ZoneState.Read(new BinaryReader(new MemoryStream(bytes))));
    }

    [Test]
    public void ZoneState_Read_Rejects_A_Fade_With_More_Left_Than_Total()
    {
        // Arrange
        var bytes = Bytes(new ZoneState { FadeLeft = 10, FadeTotal = 5 }.Write);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => ZoneState.Read(new BinaryReader(new MemoryStream(bytes))));
    }

    private static byte[] Bytes(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var bw = new BinaryWriter(stream))
            write(bw);
        return stream.ToArray();
    }
}
