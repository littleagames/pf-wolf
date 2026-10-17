using PFWolf.Assets;
using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class FloorDamageTests
{
    private MapManager _map = null!;
    private GameSession _session = null!;
    private FloorDamage _floors = null!;
    private PlayerPawn _pawn = null!;
    private Dictionary<(int, int), string> _textures = null!;
    private List<(object Who, int Points)> _hurt = null!;
    private MapFlatsTranslation _flats = null!;

    [SetUp]
    public void MakeLevel()
    {
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session)
        {
            TileMap = new ushort[MapManager.MapSize, MapManager.MapSize],
            ActorAt = new Actor?[MapManager.MapSize, MapManager.MapSize],
        };
        _session.PlayerState.Health = 100;
        _pawn = new PlayerPawn { State = _session.PlayerState };
        _pawn.SetPosition(10, 10);
        _session.PlayerState.Pawn = _pawn;

        _textures = new() { [(10, 10)] = "WATER_ELECTRIC", [(12, 10)] = "WATER_ELECTRIC", [(14, 10)] = "WATER_DARK" };
        _flats = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<MapFlatsTranslation>("damage:\n  water_electric: { amount: 25, tics: 20 }\n");
        _hurt = [];
        _floors = new FloorDamage(_map, _session, new FloorDamageHooks(
            (pawn, points) => _hurt.Add((pawn, points)),
            (monster, points) => _hurt.Add((monster, points)),
            () => _flats,
            (x, y) => _textures.GetValueOrDefault((x, y))));
    }

    private Monster Enemy(int x, int y)
    {
        var enemy = new Monster { Name = "Guard", Hitpoints = 25 };
        enemy.SetPosition(x, y);
        enemy.RuntimeFlags |= ObjFlags.Shootable;
        _map.GetActors().AddLast(enemy);
        return enemy;
    }

    private void RunTo(int time)
    {
        _session.GameState.TimeCount = time;
        _floors.Tick();
    }

    [Test]
    public void Standing_On_It_Hurts_Each_Time_Its_Interval_Comes_Round()
    {
        // Act: 1 to 41 tics, a few at a time
        foreach (var time in new[] { 1, 5, 19, 20, 23, 39, 41 })
            RunTo(time);

        // Assert: at 20 and 40
        Assert.That(_hurt, Is.EqualTo(new (object, int)[] { (_pawn, 25), (_pawn, 25) }));
    }

    [Test]
    public void Another_Floor_Or_None_Doesnt()
    {
        _textures.Remove((10, 10));
        RunTo(1); RunTo(40);
        _textures[(10, 10)] = "WATER_DARK";
        RunTo(80);
        Assert.That(_hurt, Is.Empty);
    }

    [Test]
    public void Enemies_On_It_Are_Hurt_Too_Unless_The_Floor_Says_Not()
    {
        // Arrange
        var onIt = Enemy(12, 10);
        Enemy(14, 10);
        _pawn.SetPosition(20, 20);

        // Act / Assert
        RunTo(1); RunTo(20);
        Assert.That(_hurt, Is.EqualTo(new (object, int)[] { (onIt, 25) }));

        _flats = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<MapFlatsTranslation>("damage:\n  WATER_ELECTRIC: { amount: 25, enemies: false }\n");
        RunTo(40);
        Assert.That(_hurt, Has.Count.EqualTo(1));
    }

    [Test]
    public void A_Dead_Player_Isnt_Hurt_Again()
    {
        _session.PlayerState.Health = 0;
        RunTo(1); RunTo(20);
        Assert.That(_hurt, Is.Empty);
    }

    [Test]
    public void A_New_Levels_Clock_Starts_Over()
    {
        RunTo(500);
        _hurt.Clear();
        RunTo(1);           // the next level
        RunTo(19);
        Assert.That(_hurt, Is.Empty);
        RunTo(20);
        Assert.That(_hurt, Has.Count.EqualTo(1));
    }
}
