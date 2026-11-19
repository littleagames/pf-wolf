using PFWolf.Assets.Sounds;
using PFWolf.Managers;

namespace PFWolf.Tests;

/// <summary>The reverb's two slots and their crossfades (Managers/ReverbMixer.cs), and the preset names, with no sound device</summary>
public class ReverbMixerTests
{
    private readonly string?[] _loaded = new string?[ReverbMixer.Slots];
    private readonly float[] _gains = new float[ReverbMixer.Slots];
    private readonly List<int> _routes = [];
    private int _loads;
    private ReverbMixer _mixer = null!;

    [SetUp]
    public void Make()
    {
        Array.Clear(_loaded);
        Array.Clear(_gains);
        _routes.Clear();
        _loads = 0;
        _mixer = new ReverbMixer(new ReverbMixerHooks(
            (slot, name) =>
            {
                _loads++;
                if (ReverbNames.Find(name) == null)
                    return false;
                _loaded[slot] = name;
                return true;
            },
            (slot, gain) => _gains[slot] = gain,
            slot => _routes.Add(slot)));
    }

    private int SlotOf(string name) => Array.IndexOf(_loaded, name);

    [Test]
    public void With_No_Fade_A_Preset_Is_Heard_At_Once_And_Sounds_Go_To_It()
    {
        _mixer.Set("cave", 0);
        Assert.That(_gains[SlotOf("cave")], Is.EqualTo(1));
        Assert.That(_routes, Is.EqualTo(new[] { SlotOf("cave") }));
        Assert.That(_mixer.Describe(), Is.EqualTo(new[] { "cave 100%" }));
    }

    [Test]
    public void A_Change_Crossfades_And_Sounds_Move_To_The_New_Slot()
    {
        _mixer.Set("cave", 0);
        _mixer.Set("hallway", 70);
        int cave = SlotOf("cave"), hall = SlotOf("hallway");
        Assert.That(hall, Is.Not.EqualTo(cave));
        Assert.That(_routes[^1], Is.EqualTo(hall));
        Assert.That(_gains[hall], Is.EqualTo(0));

        _mixer.Tick(35);
        Assert.That(_gains[hall], Is.EqualTo(0.5f).Within(0.001));
        Assert.That(_gains[cave], Is.EqualTo(0.5f).Within(0.001));       // its tail still ringing, going

        _mixer.Tick(35);
        Assert.That(_gains[hall], Is.EqualTo(1));
        Assert.That(_gains[cave], Is.EqualTo(0));
        Assert.That(_mixer.Describe(), Is.EqualTo(new[] { "hallway 100%" }));
    }

    [Test]
    public void Asking_For_The_One_There_Is_Changes_Nothing()
    {
        _mixer.Set("cave", 0);
        _mixer.Set("cave", 35);
        _mixer.Set("Cave", 35);
        Assert.That(_loads, Is.EqualTo(1));
        Assert.That(_routes, Has.Count.EqualTo(1));
    }

    [Test]
    public void One_Fading_Out_Asked_For_Again_Fades_Back_In_From_Where_It_Got_To()
    {
        _mixer.Set("cave", 0);
        _mixer.Set("hallway", 70);
        _mixer.Tick(35);
        _mixer.Set("cave", 70);
        Assert.That(_loads, Is.EqualTo(2));                                // not loaded again
        Assert.That(_routes[^1], Is.EqualTo(SlotOf("cave")));
        _mixer.Tick(35);
        Assert.That(_gains[SlotOf("cave")], Is.EqualTo(1).Within(0.001));
    }

    [Test]
    public void None_Is_Dry_At_Once_For_New_Sounds_And_The_Tail_Fades()
    {
        _mixer.Set("cave", 0);
        _mixer.Set("none", 70);
        Assert.That(_routes[^1], Is.EqualTo(-1));
        Assert.That(_mixer.Requested, Is.Null);
        _mixer.Tick(70);
        Assert.That(_gains, Is.All.EqualTo(0));
        Assert.That(_mixer.Describe(), Is.Empty);
    }

    [Test]
    public void Master_Scales_Both_Slots_And_Paused_Is_Silent()
    {
        _mixer.Set("cave", 0);
        _mixer.Master = 0;
        Assert.That(_gains[SlotOf("cave")], Is.EqualTo(0));
        _mixer.Master = 1;
        Assert.That(_gains[SlotOf("cave")], Is.EqualTo(1));
    }

    [Test]
    public void After_StopAll_Nothing_Rings_And_The_Next_Ask_Starts_Afresh()
    {
        _mixer.Set("cave", 0);
        _mixer.Stopped();
        Assert.That(_gains, Is.All.EqualTo(0));
        Assert.That(_routes[^1], Is.EqualTo(-1));

        _mixer.Set("cave", 0);
        Assert.That(_loads, Is.EqualTo(2));
        Assert.That(_gains[SlotOf("cave")], Is.EqualTo(1));
    }

    [Test]
    public void One_That_Cant_Be_Loaded_Is_Dry()
    {
        _mixer.Set("no-such-room", 0);
        Assert.That(_routes, Is.Empty.Or.EqualTo(new[] { -1 }));
        Assert.That(_mixer.Describe(), Is.Empty);
    }

    [Test]
    public void Every_Name_Is_One_Of_OpenTKs_Presets_And_Every_Preset_Has_A_Name()
    {
        var names = ReverbNames.All.Select(ReverbNames.Key).ToHashSet();
        Assert.That(ReverbNames.All.Where(name => AudioEffects.Preset(name) == null), Is.Empty);
        Assert.That(AudioEffects.PresetKeys.Where(key => !names.Contains(key)), Is.Empty);
        Assert.That(ReverbNames.All, Is.Unique);
    }

    [TestCase("sewer-pipe", "sewer-pipe")]
    [TestCase("Sewerpipe", "sewer-pipe")]
    [TestCase("STONE_ROOM", "stone-room")]
    [TestCase("space station hall", "space-station-hall")]
    [TestCase("nowhere", null)]
    public void A_Name_Is_Found_Written_Any_Way(string written, string? found) =>
        Assert.That(ReverbNames.Find(written), Is.EqualTo(found));
}
