using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.Constants;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>Particles (Rendering/Particles.cs) and particles.yaml: reading, bursts, moving and meeting the level</summary>
public class ParticlesTests
{
    private const float Tile = GameConstants.TextureSize;     // particles are placed in texels

    private MapManager _map = null!;
    private ParticleDetail _detail;
    private bool _sky, _doorOpen;
    private Particles _particles = null!;

    [SetUp]
    public void MakeLevel()
    {
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession(), null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        _detail = ParticleDetail.High;
        _sky = false;
        _doorOpen = false;
        _particles = new Particles(null!, null!, _map, null!, null!, null!,
            new ParticleHooks(() => _detail, (_, _) => _sky, _ => _doorOpen, () => 1));
    }

    // particles.yaml with one type (its fields as given) and an emitter Still that throws it straight east at speed, from z
    private void Use(string type, double speed = 0, double z = 10, int count = 1) =>
        _particles.UseDefinitions(YamlDataEntryLoader.Deserialize<ParticlesAsset>($$"""
            types:
              Bit: { {{type}} }
            emitters:
              Still: { type: Bit, count: {{count}}, speed: "{{speed}}", spread: 0, z: {{z}} }
            """));

    private int Burst(double tilex = 10.5, double tiley = 10.5) => _particles.Burst("Still", tilex * Tile, tiley * Tile, 0, 0);

    [Test]
    public void Particles_Yaml_Reads_Its_Types_And_Emitters()
    {
        // Act
        var asset = YamlDataEntryLoader.Deserialize<ParticlesAsset>("""
            types:
              Spark:
                shape: streak
                size: 0.5
                size-end: 2
                colors: ["#FFFFC0", "#C04000"]
                life: 20-35
                gravity: 0.15
                drag: 0.02
                bright: true
                collide: bounce
                bounce: 0.4
                trail: 3
            emitters:
              Sparks: { type: Spark, count: 12, rate: 4, angle: 90, pitch: 45, spread: 30, speed: 1-2.5, radius: 4, z: 48, range: 12 }
            """);

        // Assert
        var spark = asset.Types["Spark"];
        Assert.That((spark.Shape, spark.Size, spark.SizeEnd, spark.Life, spark.Collide, spark.Trail), Is.EqualTo(("streak", 0.5, (double?)2, "20-35", "bounce", 3.0)));
        Assert.That(spark.Colors, Is.EqualTo(new[] { "#FFFFC0", "#C04000" }));
        Assert.That((spark.Gravity, spark.Drag, spark.Bright, spark.Bounce), Is.EqualTo((0.15, 0.02, true, 0.4)));
        var sparks = asset.Emitters["Sparks"];
        Assert.That((sparks.Type, sparks.Count, sparks.Rate, sparks.Angle, sparks.Pitch, sparks.Spread, sparks.Speed, sparks.Radius, sparks.Z, sparks.Range),
            Is.EqualTo(("Spark", 12, 4.0, 90.0, 45.0, 30.0, "1-2.5", 4.0, 48.0, 12.0)));
    }

    [TestCase("35", 35, 35)]
    [TestCase("20-35", 20, 35)]
    [TestCase(" 0.5 - 1.5 ", 0.5, 1.5)]
    [TestCase("-2--1", -2, -1)]
    [TestCase("-3-4", -3, 4)]
    [TestCase("9-2", 2, 9)]
    [TestCase("1e-1", 0.1, 0.1)]
    public void A_Range_Is_A_Number_Or_Two_With_A_Dash_Between(string text, double min, double max)
    {
        Assert.That(ParticleRange.TryParse(text, out var range), Is.True);
        Assert.That(range, Is.EqualTo(new ParticleRange(min, max)));
    }

    [TestCase("")]
    [TestCase("fast")]
    [TestCase("1-")]
    [TestCase("1-2-3")]
    public void Anything_Else_Is_No_Range(string text) => Assert.That(ParticleRange.TryParse(text, out _), Is.False);

    [Test]
    public void A_Packs_Particles_Merge_Over_Its_Base_Packs_And_A_Mods_Over_Both()
    {
        // Arrange
        var pk3 = new MemoryAssetSource("pfwolf.pk3", new()
        {
            ["gamepacks/gamepack-info.yaml"] = "alpha:\n  title: Alpha\n  game-palette: pal\nbeta:\n  title: Beta\n  game-palette: pal\n  base-pack: alpha\n",
            ["gamepacks/alpha/particles.yaml"] = "types:\n  Spark: { color: \"#FFFF00\", life: 20 }\n  Smoke: {}\n",
            ["gamepacks/beta/particles.yaml"] = "emitters:\n  Sparks: { type: Spark }\n",
        });
        var mod = new MemoryAssetSource("mod.pk3", new()
        {
            ["modinfo.yaml"] = "name: Mod\nversion: \"1\"\n",
            ["particles.yaml"] = "types:\n  Spark: { life: 40 }\n  Smoke: !remove\n",
        });

        // Act
        var plain = new PfWolfPk3Loader([pk3], "beta", "beta").Load<ParticlesAsset>("beta/particles");
        var modded = new PfWolfPk3Loader([pk3], "beta", "beta", [mod]).Load<ParticlesAsset>("beta/particles");

        // Assert: beta keeps alpha's types and adds its emitter; the mod changes one field of one and takes one out
        Assert.That(plain.Types.Keys, Is.EquivalentTo(new[] { "Spark", "Smoke" }));
        Assert.That(plain.Emitters.Keys, Is.EquivalentTo(new[] { "Sparks" }));
        Assert.That(modded.Types.Keys, Is.EquivalentTo(new[] { "Spark" }));
        Assert.That((modded.Types["Spark"].Color, modded.Types["Spark"].Life), Is.EqualTo(("#FFFF00", "40")));
    }

    [Test]
    public void What_Is_Wrong_In_Particles_Yaml_Is_Found_At_Startup()
    {
        // Arrange
        _particles.UseDefinitions(YamlDataEntryLoader.Deserialize<ParticlesAsset>("""
            types:
              Odd: { shape: star, collide: stick, life: soon, color: pink, colors: [] }
              Fine: {}
            emitters:
              Lost: { type: Nothing }
              Slow: { type: Fine, speed: quick }
              Empty: {}
            """));

        // Act
        var problems = _particles.CheckDefinitions();

        // Assert
        Assert.That(problems, Has.Some.Contains("shape star"));
        Assert.That(problems, Has.Some.Contains("collide stick"));
        Assert.That(problems, Has.Some.Contains("life soon"));
        Assert.That(problems, Has.Some.Contains("color pink"));
        Assert.That(problems, Has.Some.Contains("type Nothing isn't in particles.yaml types"));
        Assert.That(problems, Has.Some.Contains("speed quick"));
        Assert.That(problems, Has.Some.Contains("emitter Empty has no type"));
        Assert.That(problems, Has.None.Contains("type Fine"));
    }

    [Test]
    public void A_Burst_Throws_Out_Its_Count_And_An_Unknown_Name_None()
    {
        // Arrange
        Use("life: 100", count: 7);

        // Act & Assert
        Assert.That(Burst(), Is.EqualTo(7));
        Assert.That(_particles.Count, Is.EqualTo(7));
        Assert.That(_particles.Burst("Bit", 10 * Tile, 10 * Tile, 0, 0, 3), Is.EqualTo(3), "a type on its own, with a count");
        Assert.That(_particles.Burst("Nothing", 10 * Tile, 10 * Tile, 0, 0), Is.EqualTo(-1));
        Assert.That(_particles.Count, Is.EqualTo(10));
    }

    // detail: a ParticleDetail (internal, so not a public test's parameter type)
    [TestCase(0, 0)]
    [TestCase(1, 25)]
    [TestCase(2, 50)]
    [TestCase(3, 100)]
    public void The_Particles_Setting_Scales_A_Burst(int detail, int made)
    {
        // Arrange
        Use("life: 100", count: 100);
        _detail = (ParticleDetail)detail;

        // Act & Assert
        Assert.That(Burst(), Is.EqualTo(made));
    }

    [Test]
    public void A_Burst_Of_One_Still_Throws_One_Out_At_Low() =>
        Assert.That(Particles.ScaledCount(1, ParticleDetail.Low), Is.EqualTo(1));

    [Test]
    public void There_Are_Never_More_Than_The_Setting_Allows()
    {
        // Arrange
        Use("life: 100", count: 3000);
        _detail = ParticleDetail.Low;

        // Act
        Burst();
        Burst();

        // Assert: 750 a burst at Low, up to its 1024
        Assert.That(_particles.Count, Is.EqualTo(Particles.MaxFor(ParticleDetail.Low)));
    }

    [Test]
    public void Turning_Particles_Off_Clears_Them()
    {
        // Arrange
        Use("life: 100", count: 5);
        Burst();

        // Act
        _detail = ParticleDetail.Off;
        _particles.Tick(1);

        // Assert
        Assert.That(_particles.Count, Is.Zero);
        Assert.That(_particles.Active, Is.False);
    }

    [Test]
    public void A_Particle_Lasts_Its_Life_In_Tics()
    {
        // Arrange
        Use("life: 5, collide: none");
        Burst();

        // Act & Assert
        _particles.Tick(4);
        Assert.That(_particles.Count, Is.EqualTo(1));
        _particles.Tick(1);
        Assert.That(_particles.Count, Is.Zero);
    }

    [Test]
    public void Gravity_Pulls_It_Down_Faster_And_Faster()
    {
        // Arrange
        Use("life: 100, gravity: 0.5, collide: none", z: 50);
        Burst();

        // Act
        _particles.Tick(1);
        float after1 = _particles.Position(0).Z;
        _particles.Tick(1);
        float after2 = _particles.Position(0).Z;

        // Assert: 0.5 the first tic, 1 the next
        Assert.That(after1, Is.EqualTo(49.5f).Within(1e-4));
        Assert.That(after2, Is.EqualTo(48.5f).Within(1e-4));
    }

    [Test]
    public void Drag_Slows_It()
    {
        // Arrange
        Use("life: 100, drag: 0.5, collide: none", speed: 8);
        Burst();
        float start = _particles.Position(0).X;

        // Act
        _particles.Tick(2);

        // Assert: 4 the first tic, 2 the next
        Assert.That(_particles.Position(0).X - start, Is.EqualTo(6f).Within(1e-4));
    }

    [Test]
    public void One_That_Dies_On_Meeting_The_Floor_Does()
    {
        // Arrange
        Use("life: 100, gravity: 1", z: 3);
        Burst();

        // Act
        _particles.Tick(3);

        // Assert
        Assert.That(_particles.Count, Is.Zero);
    }

    [Test]
    public void One_That_Bounces_Comes_To_Rest_On_The_Floor()
    {
        // Arrange
        Use("life: 500, gravity: 0.5, collide: bounce, bounce: 0.5", z: 20);
        Burst();

        // Act
        _particles.Tick(200);

        // Assert
        Assert.That(_particles.Count, Is.EqualTo(1));
        Assert.That((_particles.Position(0).Z, _particles.Position(0).VZ), Is.EqualTo((0f, 0f)));
    }

    [Test]
    public void A_Wall_Stops_One_That_Dies_On_It()
    {
        // Arrange: a wall a tile east, the particle going east 4 texels a tic from the middle of its tile
        _map.TileMap[11, 10] = 1;
        Use("life: 100", speed: 4);
        Burst();

        // Act
        _particles.Tick(7);
        int before = _particles.Count;
        _particles.Tick(1);

        // Assert: 32 texels to the wall
        Assert.That((before, _particles.Count), Is.EqualTo((1, 0)));
    }

    [Test]
    public void One_That_Bounces_Comes_Back_Off_A_Wall()
    {
        // Arrange
        _map.TileMap[11, 10] = 1;
        Use("life: 100, collide: bounce, bounce: 1", speed: 4);
        Burst();

        // Act
        _particles.Tick(16);

        // Assert: it's on its way back west, still in its own tile
        Assert.That(_particles.Count, Is.EqualTo(1));
        Assert.That(_particles.Position(0).X, Is.LessThan(11 * Tile).And.GreaterThan(10 * Tile));
    }

    [Test]
    public void It_Goes_Over_A_Wall_Lower_Than_It()
    {
        // Arrange: a one story wall, the particle above it
        _map.TileMap[11, 10] = 1;
        _sky = true;
        Use("life: 100", speed: 4, z: 70);
        Burst();

        // Act
        _particles.Tick(30);

        // Assert
        Assert.That(_particles.Count, Is.EqualTo(1));
        Assert.That(_particles.Position(0).X, Is.GreaterThan(12 * Tile));
    }

    [TestCase(false, 0)]
    [TestCase(true, 1)]
    public void A_Shut_Door_Stops_It_And_An_Open_One_Lets_It_Through(bool open, int left)
    {
        // Arrange
        _map.TileMap[11, 10] = (ushort)(0 | GameConstants.BitDoor);
        _doorOpen = open;
        Use("life: 100", speed: 4);
        Burst();

        // Act
        _particles.Tick(20);

        // Assert
        Assert.That(_particles.Count, Is.EqualTo(left));
    }

    [Test]
    public void One_That_Stops_Stays_Under_The_Ceiling()
    {
        // Arrange: rising, under a one story ceiling
        Use("life: 100, gravity: -1, collide: stop", z: 50);
        Burst();

        // Act
        _particles.Tick(30);

        // Assert
        Assert.That(_particles.Count, Is.EqualTo(1));
        Assert.That(_particles.Position(0).Z, Is.EqualTo(Tile));
    }

    [Test]
    public void Under_The_Sky_There_Is_No_Ceiling()
    {
        // Arrange
        _sky = true;
        Use("life: 100, gravity: -1", z: 50);
        Burst();

        // Act
        _particles.Tick(30);

        // Assert
        Assert.That(_particles.Count, Is.EqualTo(1));
        Assert.That(_particles.Position(0).Z, Is.GreaterThan(2 * Tile));
    }

    [Test]
    public void Off_The_Map_It_Dies()
    {
        // Arrange
        Use("life: 100", speed: 8);

        // Act
        _particles.Burst("Still", (MapConstants.ClassicMapSize - 0.5) * Tile, 10 * Tile, 0, 0);
        _particles.Tick(10);

        // Assert
        Assert.That(_particles.Count, Is.Zero);
    }

    [Test]
    public void A_New_Level_Starts_With_None()
    {
        // Arrange
        Use("life: 100", count: 5);
        Burst();

        // Act
        _particles.Reset();

        // Assert
        Assert.That(_particles.Count, Is.Zero);
    }

    [Test]
    public void A_Spread_Of_Every_Way_Goes_Every_Way()
    {
        // Arrange: no gravity, speed 1, every way, out in the open with a high ceiling
        _sky = true;
        _particles.UseDefinitions(YamlDataEntryLoader.Deserialize<ParticlesAsset>("""
            types:
              Bit: { life: 100, collide: none }
            emitters:
              Ball: { type: Bit, count: 400, speed: 1, spread: 180, z: 32 }
            """));
        _particles.Burst("Ball", 30 * Tile, 30 * Tile, 0, 0);

        // Act
        _particles.Tick(10);

        // Assert: all 10 texels out, some each way on every axis
        var spots = Enumerable.Range(0, _particles.Count).Select(_particles.Position).ToList();
        foreach (var (x, y, z, _) in spots)
        {
            double dx = x - 30 * Tile, dy = y - 30 * Tile, dz = z - 32;
            Assert.That(Math.Sqrt(dx * dx + dy * dy + dz * dz), Is.EqualTo(10).Within(1e-3));
        }
        Assert.That(spots.Count(s => s.X > 30 * Tile), Is.InRange(120, 280));
        Assert.That(spots.Count(s => s.Y > 30 * Tile), Is.InRange(120, 280));
        Assert.That(spots.Count(s => s.Z > 32), Is.InRange(120, 280));
    }

    [Test]
    public void No_Spread_Goes_The_Way_It_Faces_And_Pitch_Tilts_It_Up()
    {
        // Arrange: facing north (90), pitched up 90 - so straight up
        _sky = true;
        _particles.UseDefinitions(YamlDataEntryLoader.Deserialize<ParticlesAsset>("""
            types:
              Bit: { life: 100, collide: none }
            emitters:
              North: { type: Bit, count: 1, speed: 1, spread: 0, z: 0 }
              Up: { type: Bit, count: 1, speed: 1, spread: 0, z: 0, pitch: 90 }
            """));

        // Act
        _particles.Burst("North", 30 * Tile, 30 * Tile, 0, 90);
        _particles.Burst("Up", 30 * Tile, 30 * Tile, 0, 90);
        _particles.Tick(10);

        // Assert: north is up the map (y smaller)
        var north = _particles.Position(0);
        var up = _particles.Position(1);
        Assert.That((north.X, north.Y, north.Z), Is.EqualTo((30 * Tile, 30 * Tile - 10, 0f)).Using<(float, float, float)>(Close));
        Assert.That((up.X, up.Y, up.Z), Is.EqualTo((30 * Tile, 30 * Tile, 10f)).Using<(float, float, float)>(Close));
    }

    private static bool Close((float, float, float) a, (float, float, float) b) =>
        Math.Abs(a.Item1 - b.Item1) < 1e-3 && Math.Abs(a.Item2 - b.Item2) < 1e-3 && Math.Abs(a.Item3 - b.Item3) < 1e-3;
}
