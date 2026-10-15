using System.Reflection;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class LightingTests
{
    private const long Tile = MapConstants.TILEGLOBAL;

    private readonly List<uint> _actorTicks = [];

    // A VideoManager with a grey ramp for its palette (set up by SDL in the game), so light rows can be made
    internal static VideoManager GreyVideo()
    {
        var video = new VideoManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")));
        var property = typeof(VideoManager).GetProperty("gamepal", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var colorType = property.PropertyType.GetElementType()!;
        var palette = Array.CreateInstance(colorType, 256);
        for (int i = 0; i < 256; i++)
        {
            object color = Activator.CreateInstance(colorType)!;
            foreach (var channel in new[] { "r", "g", "b" })
                colorType.GetField(channel)!.SetValue(color, (byte)i);
            palette.SetValue(color, i);
        }
        property.SetValue(video, palette);
        return video;
    }

    private Lighting Lighting(VideoManager? video = null) =>
        new(null!, video ?? GreyVideo(), null!, new LightingHooks(() => 0, () => { }, _actorTicks.Add));

    [SetUp]
    public void Clear() => _actorTicks.Clear();

    [Test]
    public void A_Maps_Shading_Takes_What_It_Leaves_Out_From_The_Default_Map()
    {
        // Arrange
        var map = new MapInfo { Shading = new ShadingInfo { FadeEnd = 8, Light = 128 } };
        var defaults = new DefaultMapInfo { Shading = new ShadingInfo { FadeColor = "#102030", FadeEnd = 20, FadeCurve = "inverse" } };

        // Act
        var settings = Rendering.Lighting.ResolveShading(map, defaults);

        // Assert
        Assert.That(settings, Is.EqualTo(new ShadingSettings("#102030", 0, 8, 100, 128, InverseFade: true)));
    }

    [Test]
    public void Without_Shading_Anywhere_A_Level_Isnt_Shaded()
    {
        Assert.That(Rendering.Lighting.ResolveShading(new MapInfo(), new DefaultMapInfo()), Is.Null);
    }

    [Test]
    public void Unshaded_Every_Point_Gets_The_Untouched_Row_And_No_Fade()
    {
        // Arrange
        var lighting = Lighting();

        // Act
        lighting.SetShading(null);

        // Assert
        Assert.That(lighting.shading, Is.False);
        Assert.That(lighting.LightAt(Rendering.Lighting.TileIndex(5, 5), 5 * Tile, 5 * Tile), Is.SameAs(Rendering.Lighting.noshade));
        Assert.That(lighting.ShadeOffset(100 * Tile), Is.Zero);
    }

    [TestCase(1L, 0)]           // nearer than fade-start: none
    [TestCase(2L, 0)]
    [TestCase(6L, 15)]          // half way: half of the 31 steps, rounded down
    [TestCase(10L, 31)]         // at fade-end: all of it
    [TestCase(50L, 31)]
    public void Fading_Goes_Evenly_From_Fade_Start_To_Fade_End(long tiles, int step)
    {
        // Arrange
        var lighting = Lighting();
        lighting.SetShading(new ShadingSettings("#000000", 2, 10, 100, 255));

        // Act / Assert: a step is 256 entries of the row
        Assert.That(lighting.ShadeOffset(tiles * Tile), Is.EqualTo(step * 256));
    }

    [Test]
    public void The_Inverse_Curve_Fades_Fast_Then_Slow_Toward_The_Most()
    {
        // Arrange
        var lighting = Lighting();
        lighting.SetShading(new ShadingSettings("#000000", 2, 10, 100, 255, InverseFade: true));

        // Act
        int near = lighting.ShadeOffset(4 * Tile) / 256, far = lighting.ShadeOffset(1000 * Tile) / 256;

        // Assert: about half the fade by twice fade-start (31 - 31 * 2 / 4), and all of it far enough off
        // (31 - 31 * 2 / 1000 is 31 in whole steps)
        Assert.That(near, Is.EqualTo(16));
        Assert.That(far, Is.EqualTo(31));
    }

    [Test]
    public void Off_The_Map_A_Point_Has_The_Levels_Light()
    {
        // Arrange
        var lighting = Lighting();
        lighting.SetShading(new ShadingSettings("#000000", 0, 16, 0, 128));

        // Act / Assert
        Assert.That(lighting.LightAt(-1, 0, 0), Is.SameAs(lighting.lightrow));
        Assert.That(Rendering.Lighting.TileIndex(-1, 0), Is.EqualTo(-1));
        Assert.That(Rendering.Lighting.TileIndex(MapManager.MAPSIZE, 0), Is.EqualTo(-1));
    }

    [Test]
    public void A_Zone_Fades_To_Its_New_Light_Over_The_Tics_Given()
    {
        // Arrange
        var lighting = Lighting();
        lighting.SetShading(null);

        // Act
        lighting.FadeZoneLight(3, 0, 10);
        lighting.TickZoneLights(5);
        int halfway = lighting.levelzones[3].Current;
        lighting.TickZoneLights(5);

        // Assert: a zone turns shading on, and the actors' lights tick along with the zones
        Assert.That(lighting.shading, Is.True);
        Assert.That(halfway, Is.InRange(100, 155));
        Assert.That(lighting.levelzones[3].Current, Is.Zero);
        Assert.That(_actorTicks, Is.EqualTo(new uint[] { 5, 5 }));
    }

    [Test]
    public void Removing_A_Zone_Gives_Its_Tiles_The_Levels_Light_And_Can_Turn_Shading_Off()
    {
        // Arrange
        var lighting = Lighting();
        lighting.SetShading(null);
        lighting.FadeZoneLight(3, 64, 0);

        // Act
        lighting.RemoveZone(3);

        // Assert
        Assert.That(lighting.levelzones, Is.Empty);
        Assert.That(lighting.shading, Is.False);
    }

    [TestCase(new[] { "0", "128" }, false)]          // zone 0 is no zone
    [TestCase(new[] { "3", "300" }, false)]          // light past 255
    [TestCase(new[] { "3", "128", "-1" }, false)]    // tics below 0
    [TestCase(new[] { "3", "128", "", "blue" }, false)]
    [TestCase(new[] { "3", "128", "", "#4060FF" }, true)]
    [TestCase(new[] { "3", "128" }, true)]
    public void The_Set_Zone_Light_Action_Checks_Its_Arguments(string[] args, bool ok)
    {
        var lighting = Lighting();
        lighting.SetShading(null);
        Assert.That(lighting.SetZoneLightAction("A_SetZoneLight", args), Is.EqualTo(ok));
    }

    [Test]
    public void Toggling_Goes_To_The_Other_Light()
    {
        // Arrange
        var lighting = Lighting();
        lighting.SetShading(null);
        lighting.FadeZoneLight(2, 40, 0);

        // Act / Assert
        lighting.ToggleZoneLightAction("A_ToggleZoneLight", ["2", "40", "200"]);
        Assert.That(lighting.levelzones[2].Light, Is.EqualTo(200));
        lighting.ToggleZoneLightAction("A_ToggleZoneLight", ["2", "40", "200"]);
        Assert.That(lighting.levelzones[2].Light, Is.EqualTo(40));
    }

    [Test]
    public void A_Levels_Zones_Read_Back_From_A_Save()
    {
        // Arrange
        var lighting = Lighting();
        lighting.SetShading(null);
        lighting.FadeZoneLight(4, 50, 20);
        lighting.SetZoneEffect(4, ZoneEffect.Pulse);
        lighting.FadeZoneLight(9, 220, 0);
        using var stream = new MemoryStream();

        // Act
        lighting.WriteZoneLights(new BinaryWriter(stream));
        stream.Position = 0;
        var read = lighting.ReadZoneLights(new BinaryReader(stream));

        // Assert
        Assert.That(read.Keys, Is.EquivalentTo(new[] { 4, 9 }));
        Assert.That((read[4].Light, read[4].Effect, read[4].FadeLeft), Is.EqualTo((50, ZoneEffect.Pulse, 20)));
        Assert.That(read[9].Light, Is.EqualTo(220));
    }

    [TestCase("#FF8800", true)]
    [TestCase("FF8800", true)]
    [TestCase("#FF8800FF", false)]      // an alpha isn't a light's
    [TestCase("orange", false)]
    public void A_Color_Is_RRGGBB(string color, bool rgb)
    {
        Assert.That(Rendering.Lighting.IsRgb(color), Is.EqualTo(rgb));
    }

    [TestCase("flicker", true)]
    [TestCase("STROBE", true)]
    [TestCase("wobble", false)]
    [TestCase("7", false)]
    public void Zone_Effects_Are_Named(string name, bool known)
    {
        Assert.That(Rendering.Lighting.TryParseZoneEffect(name, out _), Is.EqualTo(known));
    }

    [Test]
    public void A_Bright_Frame_Or_Actor_Isnt_Shaded()
    {
        // Arrange
        var lamp = new Monster { Name = "Lamp" };
        var flare = new Monster { Name = "Flare", Flags = ["BRIGHT"] };

        // Act / Assert
        Assert.That(Rendering.Lighting.IsBright(lamp), Is.False);
        Assert.That(Rendering.Lighting.IsBright(flare), Is.True);
    }
}
