using PFWolf.Entities.Actors;
using PFWolf.Gameplay;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>Sounds looping from actors (Gameplay/LoopSounds.cs): which emitters have the voices, their fades, with no sound device</summary>
public class LoopEmittersTests
{
    private const int Voices = 3;

    private readonly string?[] _playing = new string?[Voices];
    private readonly (float X, float Y, float Level)[] _at = new (float, float, float)[Voices];
    private int _starts;
    private LoopEmitters _emitters = null!;

    [SetUp]
    public void Make()
    {
        Array.Clear(_playing);
        Array.Clear(_at);
        _starts = 0;
        _emitters = new LoopEmitters(new LoopVoiceHooks(
            (voice, sound, x, y, _, level) =>
            {
                _starts++;
                if (sound == "silent")
                    return false;
                _playing[voice] = sound;
                _at[voice] = (x, y, level);
                return true;
            },
            (voice, x, y, level) => _at[voice] = (x, y, level),
            voice => _playing[voice] = null,
            voice => _playing[voice] != null), Voices);
    }

    private static LoopCandidate At(object key, float x, string sound = "hum", float radius = 16) =>
        new(key, "Thing", sound, x, 0, radius);

    // The emitters heard, by their keys
    private List<object> Heard() =>
        [.. Enumerable.Range(0, Voices).Select(_emitters.KeyOf).Where(key => key != null).Cast<object>()];

    private float LevelOf(object key) =>
        Enumerable.Range(0, Voices).Where(v => ReferenceEquals(_emitters.KeyOf(v), key)).Select(_emitters.LevelOf).Single();

    [Test]
    public void The_Nearest_Have_The_Voices()
    {
        // Arrange
        string a = "a", b = "b", c = "c", d = "d", e = "e";

        // Act
        _emitters.Update([At(a, 9), At(b, 2), At(c, 7), At(d, 1), At(e, 5)], 0, 0, 1);

        // Assert
        Assert.That(Heard(), Is.EquivalentTo(new object[] { d, b, e }));
        Assert.That(_starts, Is.EqualTo(3));
    }

    [Test]
    public void One_Out_Of_Its_Radius_Is_Not_Heard()
    {
        string near = "near", far = "far";
        _emitters.Update([At(near, 3, radius: 4), At(far, 5, radius: 4)], 0, 0, 1);
        Assert.That(Heard(), Is.EqualTo(new object[] { near }));
    }

    [Test]
    public void A_Voice_Fades_In_Then_Out_And_Is_Freed()
    {
        // Arrange
        string hum = "hum";

        // Act + Assert: in over FadeTics
        _emitters.Update([At(hum, 1)], 0, 0, LoopEmitters.FadeTics / 2);
        Assert.That(LevelOf(hum), Is.EqualTo(0.5f).Within(0.001));
        _emitters.Update([At(hum, 1)], 0, 0, LoopEmitters.FadeTics);
        Assert.That(LevelOf(hum), Is.EqualTo(1));

        // Gone (removed, or out of reach): out over FadeTics, then the voice is free
        _emitters.Update([], 0, 0, LoopEmitters.FadeTics / 2);
        Assert.That(LevelOf(hum), Is.EqualTo(0.5f).Within(0.001));
        Assert.That(_playing[0], Is.EqualTo("hum"));
        _emitters.Update([], 0, 0, LoopEmitters.FadeTics);
        Assert.That(Heard(), Is.Empty);
        Assert.That(_playing, Is.All.Null);
    }

    [Test]
    public void While_Paused_The_Fades_Stand_Still()
    {
        string hum = "hum";
        _emitters.Update([At(hum, 1)], 0, 0, LoopEmitters.FadeTics / 2);
        _emitters.Update([At(hum, 1)], 0, 0, 0);
        Assert.That(LevelOf(hum), Is.EqualTo(0.5f).Within(0.001));
    }

    [TestCase(8f, 1f)]
    [TestCase(12f, 1f)]
    [TestCase(14f, 0.5f)]
    [TestCase(16f, 0f)]
    public void Toward_Its_Radius_It_Fades_Away(float distance, float edge) =>
        Assert.That(LoopEmitters.Edge(distance, 16), Is.EqualTo(edge).Within(0.001));

    [Test]
    public void A_Voice_Follows_Its_Emitter()
    {
        string robot = "robot";
        _emitters.Update([At(robot, 1)], 0, 0, LoopEmitters.FadeTics);
        _emitters.Update([At(robot, 4)], 0, 0, 1);
        Assert.That(_at[0].X, Is.EqualTo(4));
        Assert.That(_starts, Is.EqualTo(1));
    }

    [Test]
    public void One_Holding_A_Voice_Keeps_It_Until_Another_Is_Clearly_Nearer()
    {
        // Arrange: three voices held, at 1, 2 and 5
        string a = "a", b = "b", c = "c", d = "d";
        _emitters.Update([At(a, 1), At(b, 2), At(c, 5)], 0, 0, LoopEmitters.FadeTics);

        // Act: d comes in a little nearer than c
        _emitters.Update([At(a, 1), At(b, 2), At(c, 5), At(d, 4)], 0, 0, 1);

        // Assert: c keeps its voice
        Assert.That(Heard(), Is.EquivalentTo(new object[] { a, b, c }));

        // Act: d clearly nearer
        _emitters.Update([At(a, 1), At(b, 2), At(c, 5), At(d, 2.5f)], 0, 0, 1);

        // Assert: c gave its voice up, and d took it
        Assert.That(Heard(), Is.EquivalentTo(new object[] { a, b, d }));
    }

    [Test]
    public void Given_Up_Voices_Fade_Out_Until_One_Is_Needed()
    {
        // Arrange: a and b heard, then both out of reach and fading
        string a = "a", b = "b", c = "c";
        _emitters.Update([At(a, 1), At(b, 2)], 0, 0, LoopEmitters.FadeTics);
        _emitters.Update([], 0, 0, 2);
        Assert.That(Heard(), Is.EquivalentTo(new object[] { a, b }));

        // Act: a new one takes the free voice, leaving those fading out alone
        _emitters.Update([At(c, 1)], 0, 0, 1);

        // Assert
        Assert.That(Heard(), Is.EquivalentTo(new object[] { a, b, c }));
    }

    [Test]
    public void Another_Sound_Restarts_The_Same_Voice()
    {
        // Arrange
        string radio = "radio";
        _emitters.Update([At(radio, 1, "tune1")], 0, 0, LoopEmitters.FadeTics);

        // Act
        _emitters.Update([At(radio, 1, "tune2")], 0, 0, 1);

        // Assert
        Assert.That(_playing.Count(p => p != null), Is.EqualTo(1));
        Assert.That(_playing[0], Is.EqualTo("tune2"));
        Assert.That(LevelOf(radio), Is.EqualTo(1));
    }

    [Test]
    public void A_Voice_Stopped_Under_It_Starts_Again_While_Wanted()
    {
        // Arrange: StopAll (a menu, a sound device switched) stops the sources
        string hum = "hum";
        _emitters.Update([At(hum, 1)], 0, 0, LoopEmitters.FadeTics);
        Array.Clear(_playing);

        // Act
        _emitters.Update([At(hum, 1)], 0, 0, 1);

        // Assert
        Assert.That(_playing[0], Is.EqualTo("hum"));
        Assert.That(_starts, Is.EqualTo(2));
    }

    [Test]
    public void Nothing_To_Play_Is_Tried_Again_Only_Now_And_Then()
    {
        // Arrange
        string mute = "mute";
        _emitters.Update([At(mute, 1, "silent")], 0, 0, 1);
        Assert.That(_starts, Is.EqualTo(1));

        // Act: not every tic
        for (int i = 0; i < LoopEmitters.RetryTics - 1; i++)
            _emitters.Update([At(mute, 1, "silent")], 0, 0, 1);
        Assert.That(_starts, Is.EqualTo(1));

        // ...but once the wait is over
        _emitters.Update([At(mute, 1, "silent")], 0, 0, 1);
        _emitters.Update([At(mute, 1, "silent")], 0, 0, 1);
        Assert.That(_starts, Is.EqualTo(2));
        Assert.That(_emitters.Describe().Single(), Does.Contain("nothing to play"));
    }

    [Test]
    public void Clear_Stops_Every_Voice()
    {
        _emitters.Update([At("a", 1), At("b", 2)], 0, 0, 1);
        _emitters.Clear();
        Assert.That(Heard(), Is.Empty);
        Assert.That(_playing, Is.All.Null);
    }

    [Test]
    public void Picking_Ranks_By_Distance_With_Holders_Counted_Nearer()
    {
        // Arrange
        object a = "a", b = "b", c = "c";
        var holding = new HashSet<object>(ReferenceEqualityComparer.Instance) { c };

        // Act
        var picked = LoopEmitters.PickLoopVoices([(a, 3f), (b, 4f), (c, 5f)], holding, 2, 1.5f);

        // Assert: c counts as 3.5, ahead of b
        Assert.That(picked, Is.EqualTo(new[] { 0, 2 }));
    }

    [TestCase("ambience/hum", "ambience/hum")]
    [TestCase(" none ", "")]
    [TestCase("OFF", "")]
    [TestCase(null, "")]
    public void Loop_Sound_Names_Are_A_Sound_Or_None(string? text, string name) =>
        Assert.That(StateDataNodeDeserializer.LoopSoundName(text), Is.EqualTo(name));

    [Test]
    public void A_States_Loop_Sound_Is_Read_From_Yaml()
    {
        // Act
        var states = ActorStateResolver.Resolve(ActorDefs.Parse("""
            Generator:
              properties:
                loopsound: ambience/generator
              states:
                Spawn:
                  - sprite: AMGN
                    frames: [A, B]
                    tics-per-frame: 10
                Loud:
                  - sprite: AMGN
                    frames: [A]
                    tics-per-frame: -1
                    loop-sound: ambience/alarm
                Off:
                  - sprite: AMGN
                    frames: [C]
                    tics-per-frame: -1
                    loop-sound: none
            """)["Generator"].States);

        // Assert
        Assert.That(states["Spawn"].LoopSound, Is.Null);
        Assert.That(states["Spawn"].Next!.LoopSound, Is.Null);
        Assert.That(states["Loud"].LoopSound, Is.EqualTo("ambience/alarm"));
        Assert.That(states["Off"].LoopSound, Is.Empty);
    }
}
