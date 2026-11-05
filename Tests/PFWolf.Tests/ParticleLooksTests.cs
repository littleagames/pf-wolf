using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.Constants;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>How particles look (phase 4): blend tables, sprite frames, and what a particle leaves as it dies</summary>
public class ParticleLooksTests
{
    private const float Tile = GameConstants.TextureSize;

    private Particles _particles = null!;

    [SetUp]
    public void MakeLevel()
    {
        var map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession(), null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        _particles = new Particles(null!, null!, map, null!, null!, null!,
            new ParticleHooks(() => ParticleDetail.High, (_, _) => true, _ => true, () => 1));
    }

    private void Use(string yaml) => _particles.UseDefinitions(YamlDataEntryLoader.Deserialize<ParticlesAsset>(yaml));

    // On a palette of greys (index n is grey n), what laying index src over index dst gives
    private static int Blended(PointDrawing.Blend blend, int src, int dst)
    {
        var points = new PointDrawing(LightingTests.GreyVideo(), null!, half => half);
        return points.BlendTable(blend)![src << 8 | dst];
    }

    [TestCase(100, 60, 160)]
    [TestCase(200, 200, 255)]
    [TestCase(0, 90, 90)]
    public void Additive_Adds_The_Colors_Up_To_Full(int src, int dst, int sum) =>
        Assert.That(Blended(PointDrawing.Blend.Additive, src, dst), Is.EqualTo(sum).Within(8));

    [TestCase(PointDrawing.Blend.Translucent50, 200, 100, 150)]
    [TestCase(PointDrawing.Blend.Translucent75, 200, 0, 150)]
    [TestCase(PointDrawing.Blend.Translucent25, 200, 0, 50)]
    public void Translucent_Mixes_That_Much_Of_It_With_What_Is_Behind(object blend, int src, int dst, int mixed) =>
        Assert.That(Blended((PointDrawing.Blend)blend, src, dst), Is.EqualTo(mixed).Within(8));

    [Test]
    public void Opaque_Has_No_Table() =>
        Assert.That(new PointDrawing(LightingTests.GreyVideo(), null!, half => half).BlendTable(PointDrawing.Blend.Opaque), Is.Null);

    [Test]
    public void The_Frames_List_Grows_For_As_Many_Points_And_Sprites_As_Come()
    {
        // Arrange: a 320x160 view
        var render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.NoShade)
        {
            ViewWidth = 320, ViewHeight = 160, HeightNumerator = 0x100000,
        };
        var points = new PointDrawing(null!, render, half => half);
        var sprite = new SpriteAsset { Width = 2, Height = 2, RawData = new byte[4], OpacityMask = [1, 1, 1, 1] };

        // Act: past the first 256 slots, both ways of adding
        for (int i = 0; i < 300; i++)
        {
            points.Add(10, 10, 10, 10, 1, 15, 0x10000 + i, PointDrawing.Shape.Square);
            points.AddSprite(20, 20, 8, 8, sprite, Lighting.NoShade, 0, 0x10000 + i, PointDrawing.Blend.Translucent50);
        }
        points.Sort();

        // Assert
        Assert.That(points.Count, Is.EqualTo(600));
    }

    [TestCase("A", new[] { "A" })]
    [TestCase("A-D", new[] { "A", "B", "C", "D" })]
    [TestCase("ACE", new[] { "A", "C", "E" })]
    [TestCase("a-c, F", new[] { "A", "B", "C", "F" })]
    public void Frames_Are_Letters_Or_Runs_Of_Them(string text, string[] letters) =>
        Assert.That(Particles.FrameLetters(text), Is.EqualTo(letters));

    [TestCase("")]
    [TestCase("1-3")]
    [TestCase("D-A")]
    public void Anything_Else_Is_No_Frames(string text) => Assert.That(Particles.FrameLetters(text), Is.Null);

    [Test]
    public void What_Is_Wrong_With_How_It_Looks_Is_Found_At_Startup()
    {
        // Arrange
        Use("""
            types:
              Odd: { blend: glowing, frames: 9, on-death: Nothing }
              Faceless: { shape: sprite }
              Fine: { shape: sprite, sprite: SMOK, frames: A-D, blend: additive, on-death: Fine }
            """);

        // Act
        var problems = _particles.CheckDefinitions();

        // Assert
        Assert.That(problems, Has.Some.Contains("blend glowing"));
        Assert.That(problems, Has.Some.Contains("frames 9"));
        Assert.That(problems, Has.Some.Contains("on-death Nothing"));
        Assert.That(problems, Has.Some.Contains("Faceless: shape sprite has no sprite"));
        Assert.That(problems, Has.None.Contains("type Fine"));
    }

    [Test]
    public void A_Particle_That_Dies_Leaves_An_Emitters_Burst_Which_Leaves_Nothing()
    {
        // Arrange: Spark leaves a Pop of 4 Bits, and Bits would leave Pops too, but those left leave nothing
        Use("""
            types:
              Spark: { life: 2, collide: none, on-death: Pop }
              Bit: { life: 2, collide: none, on-death: Pop }
            emitters:
              Pop: { type: Bit, count: 4, speed: 0, z: 0 }
              One: { type: Spark, count: 1, speed: 0, z: 20 }
            """);
        _particles.Burst("One", 10 * Tile, 10 * Tile, 0, 0);

        // Act & Assert
        _particles.Tick(2);
        Assert.That(_particles.Count, Is.EqualTo(4));
        Assert.That(_particles.Position(0), Is.EqualTo((10 * Tile, 10 * Tile, 20f, 0f)), "where the spark died");
        _particles.Tick(2);
        Assert.That(_particles.Count, Is.Zero);
    }

    [Test]
    public void A_Particle_That_Dies_Can_Leave_One_Of_A_Type_Going_On_As_It_Went()
    {
        // Arrange: a spark going east 2 a tic leaves an ember
        Use("""
            types:
              Spark: { life: 3, collide: none, on-death: Ember }
              Ember: { life: 50, collide: none }
            emitters:
              East: { type: Spark, count: 1, speed: 2, spread: 0, z: 10 }
            """);
        _particles.Burst("East", 10 * Tile, 10 * Tile, 0, 0);

        // Act: 2 tics of the spark's moving, then it dies on its third, and the ember goes on a tic
        _particles.Tick(3);
        _particles.Tick(1);

        // Assert
        Assert.That(_particles.Count, Is.EqualTo(1));
        Assert.That(_particles.Position(0).X, Is.EqualTo(10 * Tile + 6).Within(1e-3));
    }

    [Test]
    public void One_That_Dies_On_A_Wall_Leaves_It_Where_It_Was()
    {
        // Arrange
        var map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession(), null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        map.TileMap[11, 10] = 1;
        _particles = new Particles(null!, null!, map, null!, null!, null!,
            new ParticleHooks(() => ParticleDetail.High, (_, _) => true, _ => true, () => 1));
        Use("""
            types:
              Spark: { life: 100, on-death: Chip }
              Chip: { life: 100, collide: none }
            emitters:
              East: { type: Spark, count: 1, speed: 4, spread: 0, z: 10 }
            """);
        _particles.Burst("East", 10.5 * Tile, 10.5 * Tile, 0, 0);

        // Act: it meets the wall on its 8th tic
        _particles.Tick(8);

        // Assert: the chip, in front of the wall
        Assert.That(_particles.Count, Is.EqualTo(1));
        Assert.That(_particles.Position(0).X, Is.LessThan(11 * Tile));
    }
}
