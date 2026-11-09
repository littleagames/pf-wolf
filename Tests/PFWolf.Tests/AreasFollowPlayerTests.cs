using System.Reflection;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

/// <summary>
/// game-info areas-follow-player: a player crossing into an area no open door joins to theirs
/// (another floor code with no door between, or a teleport's far end) joins the areas up again
/// from it, as Midnight Assault does, so enemies there notice them. Off, as Wolf3D, only doors do.
/// </summary>
public class AreasFollowPlayerTests
{
    private const int AreaTile = 108;
    private const int Tile = 1 << MapConstants.TileShift;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private bool _follow;
    private PlayerMovement _movement = null!;

    [SetUp]
    public void MakeLevel()
    {
        // Area 5 from x 0 to 10 on row 10, area 6 from x 11 on: no door between
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session, null)
        {
            MapWidth = MapConstants.ClassicMapSize, MapHeight = MapConstants.ClassicMapSize,
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        typeof(MapManager).GetField("mapsegs", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        typeof(MapManager).GetField("_floors", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, new FloorCodes(AreaTile, 45, 106, -1, 162));
        typeof(MapManager).GetField("_levelMapData", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, new MapObjectTranslationAsset());       // no triggers to step onto
        for (int x = 0; x < 20; x++)
            _map.SetMapSpot(x, 10, 0, (ushort)(AreaTile + (x <= 10 ? 5 : 6)));
        _session.Areas.Reset(45);

        _session.PlayerState.Pawn = new PlayerPawn { State = _session.PlayerState };
        _movement = new PlayerMovement(_map, new AudioManager(new Lazy<AssetManager>(() => null!), _session), new PushWalls(null!, null!, new GameSession()), _session,
            new PlayerMovementHooks(_ => 1, () => 20, _ => { }, (_, _, _, _, _, _, _) => null, (_, _) => { }, (_, _, _, _) => { },
                AreasFollowPlayer: () => _follow));

        // In area 5, the areas joined from there
        var pawn = _session.Player;
        pawn.SetPosition(10, 10);
        _movement.Thrust(0, 0);
        _session.Areas.ConnectAreas([5], 45);
    }

    [TestCase(true)]        // Midnight Assault's: area 6 is the player's now; 5, no door joining it, isn't
    [TestCase(false)]       // Wolf3D's: still area 5's, so area 6's enemies are blind to them
    public void Walking_Onto_Another_Floor_Code_With_No_Door_Between(bool follow)
    {
        // Arrange
        _follow = follow;
        var pawn = _session.Player;

        // Act: east across the edge into area 6
        pawn.X = 11 * Tile + 0x1000;
        _movement.Thrust(0, 0);

        // Assert
        Assert.That(pawn.AreaNumber, Is.EqualTo(6));
        var byplayer = _session.Areas.AreaByPlayer;
        Assert.That((byplayer[5], byplayer[6]), Is.EqualTo(follow ? ((byte)0, (byte)1) : ((byte)1, (byte)0)));
    }

    [Test]
    public void Game_Info_Turns_It_On_And_Its_Off_Without()
    {
        Assert.That(new GameInfoAsset().AreasFollowPlayer, Is.False);
        Assert.That(PFWolf.Loaders.YamlDataEntryLoader.Deserialize<GameInfoAsset>("areas-follow-player: true\n").AreasFollowPlayer, Is.True);
    }

    [Test]
    public void A_Teleport_Into_Another_Area_Joins_It_Up()
    {
        // Arrange
        _follow = true;
        var pawn = _session.Player;

        // Act: as A_TeleportTag moves them
        pawn.SetPosition(15, 10);
        _movement.Thrust(0, 0);

        // Assert
        Assert.That(_session.Areas.AreaByPlayer[6], Is.EqualTo(1));
    }

    [Test]
    public void An_Area_Already_Joined_By_An_Open_Door_Is_Left_As_It_Is()
    {
        // Arrange: a door between 5 and 6 standing open, so both are the player's
        _follow = true;
        _session.Areas.AreaConnect[5, 6] = _session.Areas.AreaConnect[6, 5] = 1;
        _session.Areas.ConnectAreas([5], 45);
        var pawn = _session.Player;

        // Act
        pawn.X = 11 * Tile + 0x1000;
        _movement.Thrust(0, 0);

        // Assert: both still
        Assert.That((_session.Areas.AreaByPlayer[5], _session.Areas.AreaByPlayer[6]), Is.EqualTo(((byte)1, (byte)1)));
    }
}
