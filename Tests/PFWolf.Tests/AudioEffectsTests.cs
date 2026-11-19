using PFWolf.Managers;

namespace PFWolf.Tests;

/// <summary>EFX's low-pass on sounds muffled through shut doors (Managers/AudioEffects.cs), with no sound device</summary>
public class AudioEffectsTests
{
    [TestCase(1f)]
    [TestCase(0.9995f)]
    public void An_Unmuffled_Sound_Has_No_Filter(float muffle) =>
        Assert.That(AudioEffects.LowpassFor(muffle), Is.Null);

    [TestCase(0.35f, 0.2071f)]              // one door: its treble well under its loudness
    [TestCase(0.1225f, 0.0429f)]            // two
    [TestCase(0f, 0f)]
    [TestCase(-1f, 0f)]                     // held to 0..1
    public void A_Muffled_Sound_Loses_More_Treble_Than_Loudness(float muffle, float gainHF)
    {
        var hf = AudioEffects.LowpassFor(muffle);
        Assert.That(hf, Is.EqualTo(gainHF).Within(0.0005));
        Assert.That(hf, Is.LessThanOrEqualTo(Math.Max(muffle, 0)));
    }

    [Test]
    public void Without_A_Device_Nothing_Is_Available_And_Nothing_Throws()
    {
        var effects = new AudioEffects();
        effects.SetDirectFilter(1, 0.5f);
        effects.Shutdown();
        Assert.That(effects.Available, Is.False);
        Assert.That(effects.Description, Does.Contain("sound is off"));
    }
}
