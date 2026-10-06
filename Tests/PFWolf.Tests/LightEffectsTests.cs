namespace PFWolf.Tests;

public class LightEffectsTests
{
    [TestCase("flicker", ZoneEffect.Flicker)]
    [TestCase("PULSE", ZoneEffect.Pulse)]
    [TestCase("Strobe", ZoneEffect.Strobe)]
    [TestCase("none", ZoneEffect.None)]
    public void TryParseZoneEffect_Reads_Names(string name, object expected)
    {
        // Act
        var parsed = Program.TryParseZoneEffect(name, out var effect);

        // Assert
        Assert.That(parsed, Is.True);
        Assert.That(effect, Is.EqualTo((ZoneEffect)expected));
    }

    [TestCase("sparkle")]
    [TestCase("9")]
    public void TryParseZoneEffect_Rejects_Others(string name)
    {
        Assert.That(Program.TryParseZoneEffect(name, out _), Is.False);
    }

    [Test]
    public void None_Is_Always_The_Light()
    {
        Assert.That(LightEffects.Apply(ZoneEffect.None, 200, 50, 35, 5, 17, 256), Is.EqualTo(200));
    }

    [TestCase(0, 200)]
    [TestCase(256, 50)]
    [TestCase(128, 125)]
    public void Flicker_Goes_Toward_Low_By_Its_Amount(int amount, int expected)
    {
        Assert.That(LightEffects.Apply(ZoneEffect.Flicker, 200, 50, 8, 5, 0, amount), Is.EqualTo(expected));
    }

    [TestCase(0, 200)]
    [TestCase(35, 50)]
    [TestCase(70, 200)]
    [TestCase(17, 128)]
    public void Pulse_Goes_Down_To_Low_And_Back_Over_Its_Cycle(int phase, int expected)
    {
        // light 200, low 50, a 70 tic cycle: halfway through it's at low
        Assert.That(LightEffects.Apply(ZoneEffect.Pulse, 200, 50, 70, 5, phase, 0), Is.EqualTo(expected));
    }

    [TestCase(0, 200)]
    [TestCase(4, 200)]
    [TestCase(5, 50)]
    [TestCase(34, 50)]
    public void Strobe_Is_Bright_For_Its_Bright_Tics(int phase, int expected)
    {
        Assert.That(LightEffects.Apply(ZoneEffect.Strobe, 200, 50, 35, 5, phase, 0), Is.EqualTo(expected));
    }

    [Test]
    public void Pulse_Tick_Wraps_Its_Phase()
    {
        // Arrange
        int phase = 60, amount = 0;

        // Act
        LightEffects.Tick(ZoneEffect.Pulse, 15, 70, ref phase, ref amount, new Random(1));

        // Assert
        Assert.That(phase, Is.EqualTo(5));
    }

    [Test]
    public void Flicker_Tick_Jumps_Within_Range_When_Its_Phase_Runs_Out()
    {
        // Arrange
        int phase = 1, amount = -1;

        // Act
        LightEffects.Tick(ZoneEffect.Flicker, 1, 8, ref phase, ref amount, new Random(1));

        // Assert
        Assert.That(phase, Is.InRange(1, 8));
        Assert.That(amount, Is.InRange(0, 256));
    }

    [Test]
    public void Flicker_Tick_Holds_While_Its_Phase_Lasts()
    {
        // Arrange
        int phase = 5, amount = 100;

        // Act
        LightEffects.Tick(ZoneEffect.Flicker, 2, 8, ref phase, ref amount, new Random(1));

        // Assert
        Assert.That(phase, Is.EqualTo(3));
        Assert.That(amount, Is.EqualTo(100));
    }

    [Test]
    public void ZoneState_Fades_From_Its_Old_Light()
    {
        // Arrange: fading to 0 from 200, half way there
        var zone = new ZoneState { Light = 0, FadeFrom = 200, FadeLeft = 10, FadeTotal = 20 };

        // Act / Assert
        Assert.That(zone.BaseLight, Is.EqualTo(100));
        zone.Tick(10, new Random(1));
        Assert.That(zone.BaseLight, Is.EqualTo(0));
    }
}
