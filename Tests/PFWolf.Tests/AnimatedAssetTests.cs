using PFWolf.Assets;
using PFWolf.Loaders;
using PFWolf.Managers;

namespace PFWolf.Tests;

/// <summary>animdefs: the YAML, frame timing, and animations found by name like the assets they animate</summary>
public class AnimatedAssetTests
{
    private static TextureAsset Texture(byte pixel) => new() { RawData = Enumerable.Repeat(pixel, 64 * 64).ToArray() };

    private static GraphicAsset Picture(byte pixel) => new([pixel], 1, 1);

    private static AnimationDefinitionsAsset Definitions(string yaml)
        => new(YamlDataEntryLoader.Deserialize<Dictionary<string, AnimationDefinition>>(yaml));

    private static AnimatedAsset<TextureAsset> Animation(bool loops, params int[] tics)
        => new(tics.Select((t, i) => new AnimationFrame<TextureAsset>($"F{i}", Texture((byte)i), t)).ToList(), loops);

    private static AssetManager Assets(string yaml, string pack = "alpha", string? packYaml = null)
    {
        var assets = new AssetManager();
        var list = new List<(string, Asset)>
        {
            ("SLIME1", Texture(1)), ("SLIME2", Texture(2)), ("SLIME3", Texture(3)),
            ("PIC1", Picture(1)), ("PIC2", Picture(2)),
            ("animdefs", Definitions(yaml)),
        };
        if (packYaml != null)
            list.Add(($"{pack}/animdefs", Definitions(packYaml)));
        assets.UseAssets(pack, list);
        return assets;
    }

    [Test]
    public void Frames_Are_Read_With_Their_Own_Or_The_Animation_Tics()
    {
        // Arrange
        var definitions = Definitions("""
            SLIME:
              tics: 5
              frames:
                - SLIME1
                - { name: SLIME2, tics: 12 }
                - stop
            """);
        var warnings = new List<string>();

        // Act
        var (frames, loops) = definitions.Animations["slime"].Read(warnings.Add);

        // Assert
        Assert.That(frames, Is.EqualTo(new[] { ("SLIME1", 5), ("SLIME2", 12) }));
        Assert.That(loops, Is.False);
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void Tics_Default_To_Eight_And_Animations_Loop()
    {
        // Act
        var (frames, loops) = Definitions("A:\n  frames: [X, Y]\n").Animations["A"].Read(_ => { });

        // Assert
        Assert.That(frames.Select(f => f.Tics), Is.All.EqualTo(AnimationDefinition.DefaultTics));
        Assert.That(loops, Is.True);
    }

    [Test]
    public void Stop_Before_The_End_Leaves_Out_What_Follows_With_A_Warning()
    {
        // Arrange
        var warnings = new List<string>();

        // Act
        var (frames, loops) = Definitions("A:\n  frames: [X, stop, Y]\n").Animations["A"].Read(warnings.Add);

        // Assert
        Assert.That(frames.Select(f => f.Name), Is.EqualTo(new[] { "X" }));
        Assert.That(loops, Is.False);
        Assert.That(warnings, Has.Count.EqualTo(1));
    }

    [Test]
    public void A_Looping_Animation_Starts_Over()
    {
        // Arrange: 4, 2 and 4 tics
        var animation = Animation(loops: true, 4, 2, 4);

        // Act / Assert
        Assert.That(animation.TotalTics, Is.EqualTo(10));
        Assert.That(new[] { 0, 3, 4, 5, 6, 9, 10, 14, 23 }.Select(animation.FrameIndexAt),
            Is.EqualTo(new[] { 0, 0, 1, 1, 2, 2, 0, 1, 0 }));
        Assert.That(animation.FrameIndexAt(-3), Is.EqualTo(0));
    }

    [Test]
    public void A_Play_Once_Animation_Holds_Its_Last_Frame()
    {
        // Arrange
        var animation = Animation(loops: false, 4, 2, 4);

        // Act / Assert
        Assert.That(new[] { 0, 5, 9, 10, 1000 }.Select(animation.FrameIndexAt), Is.EqualTo(new[] { 0, 1, 2, 2, 2 }));
    }

    [Test]
    public void An_Animation_Is_Found_By_Name_As_Its_Frame_At_The_Time_Asked()
    {
        // Arrange
        var assets = Assets("SLIME:\n  tics: 10\n  frames: [SLIME1, SLIME2, SLIME3]\n");

        // Act / Assert
        Assert.That(assets.Find<TextureAsset>("SLIME", 0)!.RawData[0], Is.EqualTo(1));
        Assert.That(assets.Find<TextureAsset>("SLIME", 15)!.RawData[0], Is.EqualTo(2));
        Assert.That(assets.Find<TextureAsset>("slime", 25)!.RawData[0], Is.EqualTo(3));
        Assert.That(assets.Find<TextureAsset>("SLIME", 30)!.RawData[0], Is.EqualTo(1));
        Assert.That(assets.Exists<TextureAsset>("SLIME"), Is.True);
        Assert.That(assets.FindAnimation<TextureAsset>("SLIME"), Is.Not.Null);
        Assert.That(assets.FindAnimation<TextureAsset>("SLIME1"), Is.Null);
    }

    [Test]
    public void Find_Without_A_Time_Goes_By_The_Animation_Clock_Or_Shows_The_First_Frame()
    {
        // Arrange
        var assets = Assets("SLIME:\n  tics: 10\n  frames: [SLIME1, SLIME2]\n");

        // Act / Assert
        Assert.That(assets.Find<TextureAsset>("SLIME")!.RawData[0], Is.EqualTo(1));
        assets.AnimationClock = () => 12;
        Assert.That(assets.Find<TextureAsset>("SLIME")!.RawData[0], Is.EqualTo(2));
    }

    [Test]
    public void Pictures_Animate_As_Pictures_And_Textures_As_Textures()
    {
        // Arrange
        var assets = Assets("""
            BLINK:
              frames: [PIC1, PIC2]
            MIXED:
              frames: [PIC1, SLIME1]
            """);

        // Act / Assert
        Assert.That(assets.Find<GraphicAsset>("BLINK", 8)!.RawData[0], Is.EqualTo(2));
        Assert.That(assets.Exists<TextureAsset>("BLINK"), Is.False);
        Assert.That(assets.Animations.ContainsKey("MIXED"), Is.False);
    }

    [Test]
    public void An_Animation_Can_Take_The_Name_Of_Its_Own_First_Frame()
    {
        // Arrange: SLIME1 becomes the animation, and its first frame is still the texture SLIME1
        var assets = Assets("""
            SLIME1:
              tics: 1
              frames: [SLIME1, SLIME2]
            OTHER:
              tics: 1
              frames: [SLIME1, SLIME3]
            """);

        // Act / Assert
        Assert.That(assets.Find<TextureAsset>("SLIME1", 0)!.RawData[0], Is.EqualTo(1));
        Assert.That(assets.Find<TextureAsset>("SLIME1", 1)!.RawData[0], Is.EqualTo(2));
        // Another animation's frame named SLIME1 is the texture too, not the animation
        Assert.That(assets.FindAnimation<TextureAsset>("OTHER")!.Frames[0].Asset, Is.Not.InstanceOf<IAnimatedAsset>());
    }

    [Test]
    public void Missing_Frames_Leave_The_Animation_Out()
    {
        // Act
        var assets = Assets("BROKEN:\n  frames: [SLIME1, NOPE]\n");

        // Assert
        Assert.That(assets.Animations.ContainsKey("BROKEN"), Is.False);
        Assert.That(assets.Exists<TextureAsset>("BROKEN"), Is.False);
        Assert.That(assets.AnimationProblems["BROKEN"], Does.Contain("NOPE"));
        Assert.That(assets.AnimationProblems.ContainsKey("SLIME"), Is.False);
    }

    [Test]
    public void The_Running_Packs_Animations_Replace_The_Shared_Ones()
    {
        // Act
        var assets = Assets("SLIME:\n  tics: 1\n  frames: [SLIME1, SLIME2]\n",
            packYaml: "SLIME:\n  tics: 1\n  frames: [SLIME3, SLIME2]\n");

        // Assert
        Assert.That(assets.Find<TextureAsset>("SLIME", 0)!.RawData[0], Is.EqualTo(3));
    }

    [Test]
    public void Mods_Animdefs_Merge_Into_The_Packs_And_Can_Remove_One()
    {
        // Arrange
        var basePk3 = new MemoryAssetSource("pfwolf.pk3", new()
        {
            ["gamepacks/gamepack-info.yaml"] = "alpha:\n  title: Alpha\n  game-palette: pal\n",
            ["animdefs/alpha/animdefs.yaml"] = "SLIME:\n  frames: [A, B]\nLAVA:\n  frames: [C, D]\n",
        });
        var mod = new MemoryAssetSource("mod", new()
        {
            ["animdefs/mine.yaml"] = "SLIME: !remove\nWATER:\n  frames: [E, F]\n",
        });

        // Act
        var loader = new PfWolfPk3Loader([basePk3], "alpha", "alpha", [mod]);

        // Assert
        Assert.That(loader.Load<AnimationDefinitionsAsset>("alpha/animdefs").Animations.Keys,
            Is.EquivalentTo(new[] { "LAVA", "WATER" }));
    }
}
