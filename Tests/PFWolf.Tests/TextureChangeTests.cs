using PFWolf.Assets;
using PFWolf.Managers;

namespace PFWolf.Tests;

/// <summary>
/// A_ChangeTexture's per-tile swaps and the tiles' animation starts (MapManager), and how a
/// play-once animation is timed from them while a looping one keeps to the shared clock
/// </summary>
public class TextureChangeTests
{
    private static MapManager NewMapManager() => new(new Lazy<AssetManager>(() => new AssetManager()), null!, new GameSession());

    private static TextureAsset Texture(byte pixel) => new() { RawData = Enumerable.Repeat(pixel, 64 * 64).ToArray() };

    [Test]
    public void A_Change_Applies_To_Its_Own_Tile_Only_Whatever_The_Case()
    {
        // Arrange
        var map = NewMapManager();

        // Act
        map.ChangeTexture(3, 4, "SWITCH1", "SWITCHANIM", tic: 100);

        // Assert
        Assert.That(map.ChangedTexture(3, 4, "switch1"), Is.EqualTo("SWITCHANIM"));
        Assert.That(map.ChangedTexture(3, 4, "OTHER"), Is.EqualTo("OTHER"));
        Assert.That(map.ChangedTexture(4, 3, "SWITCH1"), Is.EqualTo("SWITCH1"));
        Assert.That(map.AnimStart[(4 << MapManager.MapShift) + 3], Is.EqualTo(100));
    }

    [Test]
    public void Changing_What_Was_Changed_Changes_The_Original_Too()
    {
        // Arrange
        var map = NewMapManager();
        map.ChangeTexture(1, 1, "A", "B", tic: 1);

        // Act: what shows as B now shows as C, and changing it back to A undoes it
        map.ChangeTexture(1, 1, "B", "C", tic: 2);
        var afterSecond = map.ChangedTexture(1, 1, "A");
        map.ChangeTexture(1, 1, "C", "A", tic: 3);

        // Assert
        Assert.That(afterSecond, Is.EqualTo("C"));
        Assert.That(map.ChangedTexture(1, 1, "A"), Is.EqualTo("A"));
        Assert.That(map.ChangedTexture(1, 1, "B"), Is.EqualTo("A"));
    }

    [Test]
    public void Changes_And_Starts_Read_Back_As_Written()
    {
        // Arrange
        var map = NewMapManager();
        map.ChangeTexture(2, 5, "FROM", "TO", tic: 70);
        map.RestartAnimations(9, 9, tic: 140);
        using var stream = new MemoryStream();
        using (var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            map.WriteTextureChanges(bw);
        stream.Position = 0;

        // Act
        var (starts, changes) = MapManager.ReadTextureChanges(new BinaryReader(stream));

        // Assert
        Assert.That(starts[(5 << MapManager.MapShift) + 2], Is.EqualTo(70));
        Assert.That(starts[(9 << MapManager.MapShift) + 9], Is.EqualTo(140));
        Assert.That(starts.Count(s => s != 0), Is.EqualTo(2));
        Assert.That(changes.Keys, Is.EqualTo(new[] { (5 << MapManager.MapShift) + 2 }));
        Assert.That(changes[(5 << MapManager.MapShift) + 2]["from"], Is.EqualTo("TO"));
        Assert.That(stream.Position, Is.EqualTo(stream.Length));
    }

    [Test]
    public void A_Play_Once_Animation_Runs_From_Its_Start_And_A_Looping_One_From_The_Shared_Clock()
    {
        // Arrange: two 10-tic frames each
        var assets = new AssetManager();
        assets.UseAssets("alpha",
        [
            ("ONE", Texture(1)), ("TWO", Texture(2)),
            ("animdefs", new AnimationDefinitionsAsset(new()
            {
                ["LOOP"] = new AnimationDefinition { Tics = 10, Frames = ["ONE", "TWO"] },
                ["ONCE"] = new AnimationDefinition { Tics = 10, Frames = ["ONE", "TWO", "stop"] },
            })),
        ]);

        // Act / Assert: 15 tics into the level, a tile changed at tic 10
        Assert.That(assets.Find<TextureAsset>("LOOP", 15, 10)!.RawData[0], Is.EqualTo(2));
        Assert.That(assets.Find<TextureAsset>("ONCE", 15, 10)!.RawData[0], Is.EqualTo(1));
        Assert.That(assets.Find<TextureAsset>("ONCE", 500, 10)!.RawData[0], Is.EqualTo(2));
        Assert.That(assets.Find<TextureAsset>("ONE", 15, 10)!.RawData[0], Is.EqualTo(1));
    }
}
