using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Loaders;
using PFWolf.Managers;

namespace PFWolf.Tests;

/// <summary>The ambient loop's two voices and their fades (Managers/AmbientMixer.cs), with no sound device</summary>
public class AmbientMixerTests
{
    private readonly string?[] _playing = new string?[AmbientMixer.Voices];
    private readonly float[] _levels = new float[AmbientMixer.Voices];
    private int _starts;
    private AmbientMixer _mixer = null!;

    [SetUp]
    public void Make()
    {
        Array.Clear(_playing);
        Array.Clear(_levels);
        _starts = 0;
        _mixer = new AmbientMixer(new AmbientMixerHooks(
            (voice, name) =>
            {
                _starts++;
                if (name == "silent")
                    return false;
                _playing[voice] = name;
                _levels[voice] = 0;
                return true;
            },
            voice => _playing[voice] = null,
            voice => _playing[voice] != null,
            (voice, level) => _levels[voice] = level));
    }

    private float LevelOf(string name) => Enumerable.Range(0, AmbientMixer.Voices).Where(v => _playing[v] == name).Select(v => _levels[v]).SingleOrDefault();

    [Test]
    public void With_No_Fade_A_Change_Cuts_Over_At_Once()
    {
        _mixer.Set("wind", 0);
        Assert.That(LevelOf("wind"), Is.EqualTo(1));
        _mixer.Set("hum", 0);
        Assert.That(_playing, Is.EquivalentTo(new[] { "hum", null }));
        Assert.That(LevelOf("hum"), Is.EqualTo(1));
        _mixer.Set(null, 0);
        Assert.That(_playing, Is.All.Null);
    }

    [Test]
    public void A_Fade_Takes_The_Old_Sound_Out_As_The_New_One_Comes_In()
    {
        _mixer.Set("wind", 0);
        _mixer.Set("hum", 70);
        Assert.That(LevelOf("wind"), Is.EqualTo(1));
        Assert.That(LevelOf("hum"), Is.EqualTo(0));

        _mixer.Tick(35);
        Assert.That(LevelOf("wind"), Is.EqualTo(0.5f).Within(0.001));
        Assert.That(LevelOf("hum"), Is.EqualTo(0.5f).Within(0.001));

        _mixer.Tick(35);
        Assert.That(_playing, Is.EquivalentTo(new[] { "hum", null }));     // wind's voice stopped once it was silent
        Assert.That(LevelOf("hum"), Is.EqualTo(1));
    }

    [Test]
    public void Asking_For_The_One_Playing_Changes_Nothing()
    {
        _mixer.Set("wind", 0);
        for (int i = 0; i < 5; i++)
            _mixer.Set("wind", 70);
        Assert.That(_starts, Is.EqualTo(1));
        Assert.That(LevelOf("wind"), Is.EqualTo(1));
    }

    [Test]
    public void A_Sound_Fading_Out_Asked_For_Again_Comes_Back_From_Where_It_Got_To()
    {
        _mixer.Set("wind", 0);
        _mixer.Set("hum", 70);
        _mixer.Tick(14);                    // wind 0.8, hum 0.2
        _mixer.Set("wind", 70);

        Assert.That(_starts, Is.EqualTo(2), "wind wasn't started again");
        Assert.That(_mixer.Name(_mixer.Current), Is.EqualTo("wind"));
        _mixer.Tick(14);
        Assert.That(LevelOf("wind"), Is.EqualTo(1));
        Assert.That(LevelOf("hum"), Is.EqualTo(0).Within(0.001));
    }

    [Test]
    public void Stopping_With_A_Fade_Fades_Out()
    {
        _mixer.Set("wind", 0);
        _mixer.Set(null, 10);
        Assert.That(_mixer.Requested, Is.Null);
        _mixer.Tick(5);
        Assert.That(LevelOf("wind"), Is.EqualTo(0.5f).Within(0.001));
        _mixer.Tick(5);
        Assert.That(_playing, Is.All.Null);
    }

    [Test]
    public void After_Everything_Stopped_The_Sound_Asked_For_Starts_Again()
    {
        _mixer.Set("wind", 0);
        Array.Clear(_playing);              // StopAll
        _mixer.Stopped();
        _mixer.Set("wind", 0);
        Assert.That(_starts, Is.EqualTo(2));
        Assert.That(LevelOf("wind"), Is.EqualTo(1));
    }

    [Test]
    public void A_Sound_With_Nothing_To_Play_Is_Tried_Again_When_Asked()
    {
        _mixer.Set("silent", 0);
        _mixer.Set("silent", 0);
        Assert.That(_starts, Is.EqualTo(2));
        Assert.That(_mixer.Requested, Is.EqualTo("silent"));
        Assert.That(_playing, Is.All.Null);
    }

    [Test]
    public void Sound_Seq_Volume_And_Pitch_Vary_Within_Their_Variance()
    {
        Assert.That(AudioManager.VolumeAndPitch(null, null, null, null, () => 1), Is.EqualTo((1f, 1f)));
        Assert.That(AudioManager.VolumeAndPitch(0.5f, 0.2f, 1.5f, 0.1f, () => 0), Is.EqualTo((0.3f, 1.4f)).Using<(float, float)>(Near));
        Assert.That(AudioManager.VolumeAndPitch(0.5f, 0.2f, 1.5f, 0.1f, () => 1), Is.EqualTo((0.7f, 1.6f)).Using<(float, float)>(Near));
        Assert.That(AudioManager.VolumeAndPitch(0.9f, 0.5f, 10f, null, () => 1), Is.EqualTo((1f, AudioManager.MaxPitch)));   // held to range
    }

    private static bool Near((float, float) a, (float, float) b) => Math.Abs(a.Item1 - b.Item1) < 0.0001 && Math.Abs(a.Item2 - b.Item2) < 0.0001;

    [Test]
    public void Sound_Seq_Reads_Volume_And_Pitch()
    {
        var seq = YamlDataEntryLoader.Deserialize<SoundSequenceAsset>("""
            sound-info:
              drip:
                digitized: DRIP
                volume: 0.4
                volume-variance: 0.1
                pitch: 1.2
                pitch-variance: 0.15
            """);
        var drip = seq.SoundInfo["drip"];
        Assert.That((drip.Volume, drip.VolumeVariance, drip.Pitch, drip.PitchVariance), Is.EqualTo((0.4f, 0.1f, 1.2f, 0.15f)));
    }
}
