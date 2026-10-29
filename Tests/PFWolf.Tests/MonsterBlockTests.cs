using PFWolf.Constants;

namespace PFWolf.Tests;

/// <summary>Tiles enemies can't walk onto (MapManager.BlocksMonster: a class's blocktile)</summary>
public class MonsterBlockTests
{
    private static MapManager Map()
    {
        var map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession(), null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        map.SetMonsterBlock(3, 4, MapManager.MonsterBlock.Monsters);
        map.SetMonsterBlock(5, 4, MapManager.MonsterBlock.Bosses);
        map.SetMonsterBlock(7, 4, MapManager.MonsterBlock.Players);
        map.SetMonsterBlock(9, 4, MapManager.MonsterBlock.All);
        return map;
    }

    [TestCase(3, 4, false, true)]       // keeps every monster off
    [TestCase(3, 4, true, true)]
    [TestCase(5, 4, false, false)]      // only bosses
    [TestCase(5, 4, true, true)]
    [TestCase(7, 4, false, false)]      // players only
    [TestCase(9, 4, false, true)]       // all: monsters too
    [TestCase(4, 4, true, false)]       // an unmarked tile
    [TestCase(-1, 4, true, false)]      // off the map
    public void A_Marked_Tile_Keeps_Monsters_Or_Bosses_Off(int x, int y, bool boss, bool blocked)
    {
        Assert.That(Map().BlocksMonster(x, y, boss), Is.EqualTo(blocked));
    }

    [TestCase(7, 4, true)]              // players
    [TestCase(9, 4, true)]              // all
    [TestCase(3, 4, false)]             // monsters only
    [TestCase(4, 4, false)]
    public void A_Players_Or_All_Marker_Keeps_Players_On_Foot_Off(int x, int y, bool blocked)
    {
        Assert.That(Map().BlocksPlayer(x, y), Is.EqualTo(blocked));
    }

    [TestCase("monsters", "Monsters")]
    [TestCase("Enemies", "Monsters")]
    [TestCase("bosses", "Bosses")]
    [TestCase("players", "Players")]
    [TestCase("all", "All")]
    [TestCase("walls", "None")]
    [TestCase(null, "None")]
    public void Blocktile_Names_What_It_Keeps_Off(string? value, string block)
    {
        Assert.That(MapManager.BlockKind(value).ToString(), Is.EqualTo(block));
    }
}
