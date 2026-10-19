using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>Levels that aren't 64x64: loading, the per-tile lists, saves and the map loaders</summary>
public class LargeMapTests
{
    private const int Width = 100, Height = 70;
    private const ushort Wall = 1, Floor = 107;

    // A Width x Height level: walls round the edge, open floor inside, a wall in the far corner's
    // last open tile but one, a tag and a height on the far side, and a patch on the far wall
    private static MapAsset BigLevel()
    {
        var planes = new ushort[MapConstants.LevelPlanes][];
        for (int p = 0; p < planes.Length; p++)
            planes[p] = new ushort[Width * Height];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                planes[0][y * Width + x] = x == 0 || y == 0 || x == Width - 1 || y == Height - 1 ? Wall : Floor;

        planes[0][(Height - 3) * Width + (Width - 3)] = Wall | (5 << MapConstants.PatchShift);
        planes[MapConstants.TagPlane][(Height - 2) * Width + (Width - 2)] = 9;
        planes[MapConstants.HeightPlane][(Height - 3) * Width + (Width - 3)] = 3;
        return new MapAsset { Name = "BIG", Width = Width, Height = Height, MapData = planes };
    }

    private static MapManager LoadedMap(MapAsset level)
    {
        var assets = new AssetManager();
        var mapdefs = new MapObjectTranslationAsset();
        mapdefs.Walls[Wall] = new MapTextureTranslation { North = "WALL" };
        mapdefs.Floors.AreaStart = Floor;
        mapdefs.Floors.AreaCount = 1;
        assets.UseAssets("test", [(level.Name, level), ("test/mapdefs", mapdefs)]);

        var map = new MapManager(new Lazy<AssetManager>(() => assets), null!, new GameSession(), null);
        map.LoadMap(level.Name, difficulty: 0);
        return map;
    }

    [Test]
    public void A_Level_Loads_At_Its_Own_Size()
    {
        // Act
        var map = LoadedMap(BigLevel());

        // Assert
        Assert.That((map.MapWidth, map.MapHeight), Is.EqualTo((Width, Height)));
        Assert.That(map.MapArea, Is.EqualTo(Width * Height));
        Assert.That(map.TileMap.GetLength(0), Is.EqualTo(Width));
        Assert.That(map.TileMap.GetLength(1), Is.EqualTo(Height));
        Assert.That(map.ActorAt.GetLength(0), Is.EqualTo(Width));
        Assert.That(map.Seen.GetLength(1), Is.EqualTo(Height));
        Assert.That(map.FloorFlat, Has.Length.EqualTo(Width * Height));
        Assert.That(map.AnimStart, Has.Length.EqualTo(Width * Height));
    }

    [Test]
    public void The_Far_Side_Of_A_Large_Level_Reads_Its_Own_Tiles()
    {
        // Act
        var map = LoadedMap(BigLevel());

        // Assert: the wall, its patch and height, and the tag, all past 64 each way
        Assert.That(map.TileMap[Width - 3, Height - 3], Is.EqualTo(Wall));
        Assert.That(map.GetPatch(Width - 3, Height - 3), Is.EqualTo(5));
        Assert.That(map.WallStories(Width - 3, Height - 3), Is.EqualTo(3));
        Assert.That(map.GetTag(Width - 2, Height - 2), Is.EqualTo(9));
        Assert.That(map.TileMap[Width - 2, Height - 2], Is.Zero);
        Assert.That(map.TaggedTiles(9), Is.EqualTo(new[] { (Width - 2, Height - 2) }));
    }

    [Test]
    public void Tiles_Are_On_The_Level_Up_To_Its_Width_And_Height()
    {
        // Act
        var map = LoadedMap(BigLevel());

        // Assert
        Assert.That(map.InMap(Width - 1, Height - 1), Is.True);
        Assert.That(map.InMap(Width, 0), Is.False);
        Assert.That(map.InMap(0, Height), Is.False);
        Assert.That(map.InMap(-1, 0), Is.False);
        Assert.That(map.TileIndex(3, 2), Is.EqualTo(2 * Width + 3));
        Assert.That(map.TileIndexOrNone(Width, 2), Is.EqualTo(-1));
    }

    [Test]
    public void A_Saved_Large_Level_Reads_Back_At_Its_Size()
    {
        // Arrange
        var map = LoadedMap(BigLevel());
        map.Seen[Width - 2, Height - 2] = SeenFlags.Floor;
        map.SetPatch(Width - 3, Height - 3, 7);
        using var stream = new MemoryStream();
        using (var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            map.WriteLevelState(bw);
            bw.Write(map.GetSeenBytes());
        }
        stream.Position = 0;

        // Act
        using var br = new BinaryReader(stream);
        var level = MapManager.ReadLevelState(br);
        var seen = br.ReadBytes(level.TileMap.Length);

        // Assert
        Assert.That(level.TileMap.GetLength(0), Is.EqualTo(Width));
        Assert.That(level.TileMap.GetLength(1), Is.EqualTo(Height));
        Assert.That(level.TileMap[Width - 3, Height - 3], Is.EqualTo(Wall));
        Assert.That(level.Patches[Width - 3, Height - 3], Is.EqualTo(7));
        Assert.That(level.ActorAt[0, Height - 1], Is.InstanceOf<Wall>());
        map.SetSeenBytes(seen);
        Assert.That(map.Seen[Width - 2, Height - 2], Is.EqualTo(SeenFlags.Floor));
    }

    [Test]
    public void A_Saved_Level_Of_Another_Size_Is_Refused()
    {
        // Arrange: saved from a 100x70 level, restored onto a 64x64 one of the same name
        var map = LoadedMap(BigLevel());
        using var stream = new MemoryStream();
        using (var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            map.WriteLevelState(bw);
        stream.Position = 0;
        var level = MapManager.ReadLevelState(new BinaryReader(stream));
        var small = LoadedMap(EditorMaps.Blank("BIG"));

        // Act
        var why = small.CheckLevelState(level, "BIG");

        // Assert
        Assert.That(why, Does.Contain("has changed"));
    }

    [Test]
    public void The_Scrollers_Size_Themselves_To_The_Level()
    {
        // Arrange
        var map = LoadedMap(BigLevel());

        // Act
        map.Scrollers.InitLevel(null);

        // Assert
        Assert.That(map.Scrollers.WallColumn, Has.Length.EqualTo(Width * Height));
        Assert.That(map.Scrollers.Column(Width - 3, Height - 3, 5 << GameConstants.TextureShift), Is.EqualTo(5 << GameConstants.TextureShift));
    }

    [TestCase(2, 64)]
    [TestCase(64, 257)]
    [TestCase(0, 0)]
    public void Sizes_Past_The_Limits_Are_Refused(int width, int height)
    {
        Assert.That(MapConstants.CheckMapSize(width, height), Is.Not.Null);
    }

    [TestCase(3, 3)]
    [TestCase(64, 64)]
    [TestCase(256, 17)]
    public void Sizes_Within_The_Limits_Are_Allowed(int width, int height)
    {
        Assert.That(MapConstants.CheckMapSize(width, height), Is.Null);
    }

    [Test]
    public void An_EcWolf_Map_Of_Any_Size_Loads_And_Saves()
    {
        // Arrange
        var level = BigLevel();

        // Act
        var loaded = EcWolfMapLoader.Load(EcWolfMapLoader.Save(level, "BIG"));

        // Assert
        Assert.That((loaded.Width, loaded.Height), Is.EqualTo(((ushort)Width, (ushort)Height)));
        Assert.That(loaded.MapData.Length, Is.EqualTo(MapConstants.LevelPlanes));
        for (int p = 0; p < MapConstants.LevelPlanes; p++)
            Assert.That(loaded.MapData[p], Is.EqualTo(level.MapData[p]), $"plane {p}");
    }

    [Test]
    public void An_EcWolf_Map_Too_Big_Is_Refused()
    {
        // Arrange
        var planes = new[] { new ushort[300 * 4] };

        // Act / Assert
        Assert.That(() => EcWolfMapLoader.ToMapAsset(new WdcMap("HUGE", 300, 4, planes)),
            Throws.InstanceOf<InvalidDataException>().With.Message.Contains("256"));
    }
}
