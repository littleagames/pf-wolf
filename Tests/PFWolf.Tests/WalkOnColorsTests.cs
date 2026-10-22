using System.Reflection;
using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Tests;

/// <summary>Blake Stone's on-the-fly colors: stepping onto a ceiling-floor-colors code changes the level's colors (MapManager.StepOntoColorsCode)</summary>
public class WalkOnColorsTests
{
    private MapManager _map = null!;

    [SetUp]
    public void MakeLevel()
    {
        _map = new MapManager(new Lazy<AssetManager>(() => new AssetManager()), null!, new GameSession(), null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            MapWidth = MapConstants.ClassicMapSize, MapHeight = MapConstants.ClassicMapSize,
        };
        typeof(MapManager).GetField("mapsegs", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        typeof(MapManager).GetField("_levelMapData", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, new MapObjectTranslationAsset { MapInfo = { [0xFE] = MapInfoCodes.CeilingFloorColors, [0xF4] = MapInfoCodes.None } });
    }

    [Test]
    public void Stepping_Onto_A_Colors_Code_Gives_The_Level_Its_Colors()
    {
        // Arrange
        _map.SetMapSpot(10, 10, 1, 0xFE00);
        _map.SetMapSpot(11, 10, 1, 0x2F19);

        // Act
        _map.StepOntoColorsCode(10, 10);

        // Assert
        Assert.That((_map.MapCeilingColor, _map.MapFloorColor), Is.EqualTo(((byte?)0x2F, (byte?)0x19)));
    }

    [TestCase(0xFE01)]      // not exactly the code
    [TestCase(0xF400)]      // another kind
    [TestCase(0)]
    public void Other_Values_Leave_The_Colors_Alone(int value)
    {
        // Arrange
        _map.SetMapSpot(10, 10, 1, (ushort)value);
        _map.SetMapSpot(11, 10, 1, 0x2F19);

        // Act
        _map.StepOntoColorsCode(10, 10);

        // Assert
        Assert.That(_map.MapCeilingColor, Is.Null);
    }
}
