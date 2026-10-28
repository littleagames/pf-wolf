using System.Reflection;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Scripting;

namespace PFWolf.Tests;

/// <summary>Walls taken away as the level's played (Gameplay/DestructibleWalls.cs, MapManager.RemoveWall)</summary>
public class DestructibleWallsTests
{
    private const int AreaTile = 108;
    private const int SecretTrigger = 98;
    private const int WallId = 12;
    private const int Cracked = 40, MoreCracked = 41, Glass = 42, Bunker = 43, Crumbly = 44;
    private const int CrumbleTics = 20;
    private bool _keepStanding;
    private readonly List<string> _breaks = [];
    private const ushort Tag = 4;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private DestructibleWalls _walls = null!;
    private readonly List<string> _sounds = [], _objectives = [];

    private static MapObjectTranslationAsset MapData() => new()
    {
        Triggers = { [SecretTrigger] = new MapTriggerTranslation { Action = "A_PushWall", Secret = true } },
        Walls =
        {
            [WallId] = new MapTextureTranslation { North = "PLAIN" },
            // 40 takes 50 rockets' worth before it cracks further into 41, which the next rocket breaks
            [Cracked] = new MapTextureTranslation
            {
                North = "CRACKED",
                Destructible = new MapDestructibleTranslation { DamageTypes = ["Rocket"], Health = 50, To = MoreCracked, Sound = "world/crack" },
            },
            [MoreCracked] = new MapTextureTranslation
            {
                North = "CRACKED2",
                Destructible = new MapDestructibleTranslation
                {
                    DamageTypes = ["rocket"], Sound = "world/crumble", Secret = true, Objective = "Sabotage", Actions = ["A_RemoveWall"],
                },
            },
            [Glass] = new MapTextureTranslation { North = "GLASS", Destructible = new MapDestructibleTranslation { DamageTypes = ["Bullet", "Melee"], Floor = 2 } },
            [Bunker] = new MapTextureTranslation { North = "BUNKER", Destructible = new MapDestructibleTranslation { DamageTypes = ["Blast"], PlayersOnly = true } },
            [Crumbly] = new MapTextureTranslation
            {
                North = "CRUMBLY", East = "CRUMBLY2",
                Destructible = new MapDestructibleTranslation { DamageTypes = ["Rocket"], Animation = "CRUMBLE", Sound = "world/crumble" },
            },
        },
    };

    [SetUp]
    public void MakeLevel()
    {
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
        typeof(MapManager).GetField("_levelMapData", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_map, MapData());
        _session.Areas.Reset(45);
        var doors = new Doors(null!, null!, _map, null!, null!, _session, null!);
        _sounds.Clear();
        _objectives.Clear();
        _breaks.Clear();
        _keepStanding = false;
        _walls = new DestructibleWalls(_map, doors, _session, new DestructibleWallHooks((sound, x, y) => _sounds.Add($"{sound} {x},{y}"), _objectives.Add,
            name => name.Equals("CRUMBLE", StringComparison.OrdinalIgnoreCase) ? CrumbleTics : null,
            (x, y, wall, type, by) => { _breaks.Add($"{x},{y} {wall} {type ?? "none"}"); return !_keepStanding; }));
    }

    // Area 5 west of 10,10 and area 7 east of it, the wall between them
    private void WallBetweenAreas()
    {
        _map.SetMapSpot(9, 10, 0, AreaTile + 5);
        _map.SetMapSpot(11, 10, 0, AreaTile + 7);
        Wall(10, 10);
    }

    private void Wall(int x, int y, int id = WallId)
    {
        _map.SetMapSpot(x, y, 0, (ushort)id);
        _map.TileMap[x, y] = (ushort)id;
        _map.ActorAt[x, y] = new Wall(id);
    }

    // A wall of that id between areas 5 and 7, at 10,10
    private void DestructibleWall(int id)
    {
        WallBetweenAreas();
        Wall(10, 10, id);
    }

    private static Entities.Actors.Actor Missile(string type, Entities.Actors.Actor? shooter = null, int x = 0, int y = 0)
    {
        var missile = new Entities.Actors.Actor { Name = "Missile", X = x, Y = y, Shooter = shooter };
        missile.Properties["damagetype"] = type;
        return missile;
    }

    [Test]
    public void A_Hit_Of_A_Type_It_Doesnt_List_Does_Nothing_And_Rolls_Nothing()
    {
        // Arrange
        DestructibleWall(Cracked);
        bool rolled = false;

        // Act
        bool broke = _walls.Hit(10, 10, "Bullet", new Entities.Actors.PlayerPawn(), () => { rolled = true; return 999; });

        // Assert
        Assert.That(broke, Is.False);
        Assert.That(rolled, Is.False);
        Assert.That(_map.WallDamage, Is.Empty);
        Assert.That(_map.TileMap[10, 10], Is.EqualTo(Cracked));
    }

    [Test]
    public void Hits_Add_Up_To_Its_Health_Then_It_Turns_Into_Its_Next_Stage()
    {
        // Arrange
        DestructibleWall(Cracked);
        var player = new Entities.Actors.PlayerPawn();

        // Act
        bool first = _walls.Hit(10, 10, "Rocket", player, () => 30);
        int taken = _map.WallDamage[_map.TileIndex(10, 10)];
        bool second = _walls.Hit(10, 10, "ROCKET", player, () => 30);

        // Assert
        Assert.That((first, taken, second), Is.EqualTo((false, 30, true)));
        Assert.That(_map.TileMap[10, 10], Is.EqualTo(MoreCracked));
        Assert.That(_map.ActorAt[10, 10], Is.TypeOf<Wall>());
        Assert.That(_map.WallDamage, Is.Empty, "the next stage starts afresh");
        Assert.That(_sounds, Is.EqualTo(new[] { "world/crack 10,10" }));
    }

    [Test]
    public void The_Last_Stage_Goes_With_Its_Secret_Objective_And_Actions()
    {
        // Arrange: its action (A_RemoveWall) takes the other wall with its tag, at 20,20
        _walls.Register();
        DestructibleWall(MoreCracked);
        Wall(20, 20);
        _map.SetTag(10, 10, Tag);
        _map.SetTag(20, 20, Tag);
        var player = new Entities.Actors.PlayerPawn();

        // Act
        bool broke = _walls.Hit(10, 10, "Rocket", Missile("Rocket", shooter: player), () => 1);

        // Assert
        Assert.That(broke, Is.True);
        Assert.That(_map.TileMap[10, 10], Is.Zero);
        Assert.That(_session.Areas.AreaConnect[5, 7], Is.EqualTo(1));
        Assert.That(_session.GameState.SecretCount, Is.EqualTo(1));
        Assert.That(_objectives, Is.EqualTo(new[] { "Sabotage" }));
        Assert.That(_sounds, Is.EqualTo(new[] { "world/crumble 10,10" }));
        Assert.That(_map.TileMap[20, 20], Is.Zero, "its action ran on its tag");
    }

    [Test]
    public void A_Wall_Can_Leave_A_Floor_Flat_Of_Its_Own()
    {
        // Arrange: the tile's ceiling index 3 is kept
        DestructibleWall(Glass);
        _map.SetMapSpot(10, 10, MapManager.FlatPlane, 3 << 8);

        // Act
        bool broke = _walls.Hit(10, 10, "Bullet", new Entities.Actors.PlayerPawn(), () => 5);

        // Assert
        Assert.That(broke, Is.True);
        Assert.That(_map.MapSpot(10, 10, MapManager.FlatPlane), Is.EqualTo(2 | 3 << 8));
    }

    [Test]
    public void A_Players_Only_Wall_Takes_No_Hits_From_Enemies()
    {
        // Arrange
        DestructibleWall(Bunker);
        var enemy = new Entities.Actors.Monster { Name = "Hans" };

        // Act / Assert
        Assert.That(_walls.Hit(10, 10, "Blast", Missile("Blast", shooter: enemy), () => 10), Is.False);
        Assert.That(_walls.Hit(10, 10, "Blast", enemy, () => 10), Is.False);
        Assert.That(_walls.Hit(10, 10, "Blast", Missile("Blast", shooter: new Entities.Actors.PlayerPawn()), () => 10), Is.True);
    }

    [Test]
    public void A_Missile_Hits_The_Wall_Its_Box_Reaches_Into()
    {
        // Arrange: a rocket just west of 10,10, its box over the wall's west face
        DestructibleWall(MoreCracked);
        var rocket = Missile("Rocket", x: (10 << MapConstants.TileShift) - GameConstants.ProjSize / 2, y: (10 << MapConstants.TileShift) + 0x8000);

        // Act
        bool broke = _walls.HitByMissile(rocket, () => 1);

        // Assert
        Assert.That(broke, Is.True);
        Assert.That(_map.TileMap[10, 10], Is.Zero);
    }

    [Test]
    public void A_Blast_Without_A_Damage_Type_Of_Its_Own_Deals_Blast()
    {
        // Arrange
        DestructibleWall(Bunker);
        var grenade = new Entities.Actors.Actor { Name = "Grenade", Shooter = new Entities.Actors.PlayerPawn() };

        // Act / Assert
        Assert.That(_walls.HitByBlast(grenade, 10, 10, () => 1), Is.True);
    }

    [Test]
    public void Scripts_Break_Walls_Read_Their_Health_And_Can_Keep_Them_Standing()
    {
        // Arrange: a hook that keeps rocket-broken walls standing
        var console = new ConsoleManager();
        var scripts = new GameScripts(null!, _map, null!, null!, console, null, null!, _session, (_, _) => { })
        {
            BreakWall = (x, y) => x == 10,
            WallHealth = (x, y) => x == 10 ? 25 : null,
        };
        scripts.LuaCommand(["x = 1"]);
        scripts.Scripts!.Load("game.onwallbreak(function(x, y, wall, type, by) print('break', x, y, wall, type) return type ~= 'Rocket' end)", "test.lua");

        // Act
        scripts.Scripts.Execute("print(game.breakwall(10, 3), game.breakwall(11, 3), game.wallhealth(10, 3), game.wallhealth(11, 3))");
        bool rocket = scripts.RunWallBreakHooks(4, 5, 40, "Rocket", null);
        bool action = scripts.RunWallBreakHooks(4, 5, 40, null, null);

        // Assert
        Assert.That(console.Scrollback, Has.Some.EqualTo("true\tfalse\t25\tnil"));
        Assert.That((rocket, action), Is.EqualTo((false, true)));
        Assert.That(console.Scrollback, Has.Some.EqualTo("break\t4\t5\t40\tRocket"));
        Assert.That(console.Scrollback, Has.Some.EqualTo("break\t4\t5\t40\tnil"));
    }

    [Test]
    public void A_Wall_With_An_Animation_Crumbles_Solid_Until_It_Has_Played()
    {
        // Arrange
        DestructibleWall(Crumbly);
        _session.GameState.TimeCount = 100;

        // Act: broken, then a tic short of the animation's end
        bool broke = _walls.Hit(10, 10, "Rocket", new Entities.Actors.PlayerPawn(), () => 1);
        _session.GameState.TimeCount = 100 + CrumbleTics - 1;
        _walls.Tick();

        // Assert: crumbling, every face showing the animation, its sound gone off, no more hits
        Assert.That(broke, Is.True);
        Assert.That(_walls.IsCrumbling(10, 10), Is.True);
        Assert.That(_map.TileMap[10, 10], Is.EqualTo(Crumbly));
        Assert.That((_map.ChangedTexture(10, 10, "CRUMBLY"), _map.ChangedTexture(10, 10, "CRUMBLY2")), Is.EqualTo(("CRUMBLE", "CRUMBLE")));
        Assert.That(_map.AnimStart[_map.TileIndex(10, 10)], Is.EqualTo(100));
        Assert.That(_sounds, Is.EqualTo(new[] { "world/crumble 10,10" }));
        Assert.That(_walls.Hurts(10, 10, "Rocket", null), Is.False);

        // Act: its last tic
        _session.GameState.TimeCount = 100 + CrumbleTics;
        _walls.Tick();

        // Assert: gone, its faces' change taken back
        Assert.That(_walls.IsCrumbling(10, 10), Is.False);
        Assert.That(_map.TileMap[10, 10], Is.Zero);
        Assert.That(_map.ChangedTexture(10, 10, "CRUMBLY"), Is.EqualTo("CRUMBLY"));
    }

    [Test]
    public void A_Wall_Taken_Away_While_It_Crumbles_Stops_Crumbling()
    {
        // Arrange
        DestructibleWall(Crumbly);
        _walls.Break(10, 10);

        // Act
        _walls.Remove(10, 10);

        // Assert
        Assert.That(_map.CrumblingWalls, Is.Empty);
        Assert.That(_map.TileMap[10, 10], Is.Zero);
    }

    [Test]
    public void A_Script_Can_Keep_A_Wall_Standing_And_The_Damage_Stays()
    {
        // Arrange
        DestructibleWall(Cracked);
        _keepStanding = true;

        // Act
        bool broke = _walls.Hit(10, 10, "Rocket", new Entities.Actors.PlayerPawn(), () => 70);

        // Assert
        Assert.That(broke, Is.False);
        Assert.That(_map.TileMap[10, 10], Is.EqualTo(Cracked));
        Assert.That(_map.WallDamage[_map.TileIndex(10, 10)], Is.EqualTo(70));
        Assert.That(_breaks, Is.EqualTo(new[] { $"10,10 {Cracked} Rocket" }));
        Assert.That(_sounds, Is.Empty);
    }

    [Test]
    public void Health_Left_Counts_Down_And_Is_Nothing_For_A_Plain_Wall()
    {
        // Arrange
        DestructibleWall(Cracked);
        Wall(20, 20);
        Wall(30, 30, Glass);

        // Act
        _walls.Hit(10, 10, "Rocket", null, () => 20);

        // Assert
        Assert.That((_walls.HealthLeft(10, 10), _walls.HealthLeft(20, 20), _walls.HealthLeft(30, 30)), Is.EqualTo(((int?)30, (int?)null, (int?)1)));
    }

    [Test]
    public void A_DamageWall_Without_A_Type_Hurts_Whatever_Types_A_Wall_Lists()
    {
        // Arrange: tagged 40 (rockets) and 42 (bullets), and a switch at 20,10
        _walls.Register();
        DestructibleWall(Cracked);
        Wall(30, 30, Glass);
        _map.SetTag(10, 10, Tag);
        _map.SetTag(30, 30, Tag);
        var activation = new Entities.TriggerActivation(20, 10, ControlDir.North, new Entities.Actors.PlayerPawn(), Tag);

        // Act
        Entities.MapTriggerRegistry.Invoke("A_DamageWall(10, \"Bullet\")", activation);
        int afterBullets = _map.WallDamage.GetValueOrDefault(_map.TileIndex(10, 10));
        Entities.MapTriggerRegistry.Invoke("A_DamageWall(10)", activation);

        // Assert: the bullets only broke the glass; the untyped damage reached the rocket wall
        Assert.That(afterBullets, Is.Zero);
        Assert.That(_map.TileMap[30, 30], Is.Zero);
        Assert.That(_map.WallDamage[_map.TileIndex(10, 10)], Is.EqualTo(10));
    }

    [Test]
    public void A_BreakWall_Crumbles_A_Plain_Wall_With_The_Animation_Given()
    {
        // Arrange
        _walls.Register();
        WallBetweenAreas();
        _map.SetTag(10, 10, Tag);
        var activation = new Entities.TriggerActivation(20, 10, ControlDir.North, new Entities.Actors.PlayerPawn(), Tag);

        // Act
        Entities.MapTriggerRegistry.Invoke("A_BreakWall(\"CRUMBLE\", \"world/rumble\")", activation);

        // Assert
        Assert.That(_walls.IsCrumbling(10, 10), Is.True);
        Assert.That(_map.ChangedTexture(10, 10, "PLAIN"), Is.EqualTo("CRUMBLE"));
        Assert.That(_sounds, Is.EqualTo(new[] { "world/rumble 10,10" }));
        Assert.That(_breaks, Is.EqualTo(new[] { $"10,10 {WallId} none" }));
    }

    [Test]
    public void An_Animation_That_Isnt_One_Breaks_The_Wall_At_Once()
    {
        // Arrange
        WallBetweenAreas();

        // Act
        bool broke = _walls.Break(10, 10, animation: "NOSUCH");

        // Assert
        Assert.That(broke, Is.True);
        Assert.That(_map.TileMap[10, 10], Is.Zero);
        Assert.That(_map.CrumblingWalls, Is.Empty);
    }

    [Test]
    public void The_Crumbling_Walls_Are_Saved_With_The_Level()
    {
        // Arrange
        DestructibleWall(Crumbly);
        _session.GameState.TimeCount = 50;
        _walls.Break(10, 10);
        using var stream = new MemoryStream();

        // Act
        _map.WriteLevelState(new BinaryWriter(stream));
        stream.Position = 0;
        var level = MapManager.ReadLevelState(new BinaryReader(stream));

        // Assert
        Assert.That(level.CrumblingWalls, Is.EqualTo(new[] { new CrumblingWall(10, 10, 50 + CrumbleTics, "CRUMBLE") }));
    }

    [Test]
    public void The_Damage_Walls_Have_Taken_Is_Saved_With_The_Level()
    {
        // Arrange
        DestructibleWall(Cracked);
        _walls.Hit(10, 10, "Rocket", new Entities.Actors.PlayerPawn(), () => 30);
        using var stream = new MemoryStream();

        // Act
        _map.WriteLevelState(new BinaryWriter(stream));
        stream.Position = 0;
        var level = MapManager.ReadLevelState(new BinaryReader(stream));

        // Assert
        Assert.That(level.WallDamage, Is.EqualTo(new Dictionary<int, int> { [_map.TileIndex(10, 10)] = 30 }));
    }

    [Test]
    public void A_Removed_Wall_Leaves_Open_Floor_In_An_Area_Beside_It()
    {
        // Arrange
        WallBetweenAreas();

        // Act
        bool removed = _walls.Remove(10, 10);

        // Assert
        Assert.That(removed, Is.True);
        Assert.That(_map.TileMap[10, 10], Is.Zero);
        Assert.That(_map.ActorAt[10, 10], Is.Null);
        Assert.That(_map.MapSpot(10, 10, 0), Is.EqualTo(AreaTile + 5));
    }

    [Test]
    public void The_Areas_It_Stood_Between_Are_Joined_For_Good()
    {
        // Arrange
        WallBetweenAreas();

        // Act
        _walls.Remove(10, 10);

        // Assert
        Assert.That(_session.Areas.AreaConnect[5, 7], Is.EqualTo(1));
        Assert.That(_session.Areas.AreaConnect[7, 5], Is.EqualTo(1));
    }

    [Test]
    public void A_Wall_In_One_Area_Joins_Nothing()
    {
        // Arrange: area 5 on both sides
        WallBetweenAreas();
        _map.SetMapSpot(11, 10, 0, AreaTile + 5);

        // Act
        _walls.Remove(10, 10);

        // Assert
        Assert.That(_session.Areas.AreaConnect[5, 5], Is.Zero);
    }

    [TestCase(false, 3, 0)]     // every story goes
    [TestCase(true, 3, 3)]      // the upper ones stay up as an arch
    [TestCase(true, 1, 0)]      // a one-story wall leaves nothing to stand
    public void A_Tall_Wall_Leaves_An_Arch_Only_When_Asked(bool arch, int stories, int left)
    {
        // Arrange
        WallBetweenAreas();
        _map.SetWallStories(10, 10, stories);

        // Act
        _walls.Remove(10, 10, arch);

        // Assert
        Assert.That(_map.StoryMap[10, 10], Is.EqualTo(left));
        Assert.That(_map.MapSpot(10, 10, MapManager.HeightPlane), Is.EqualTo(left));
    }

    [Test]
    public void An_Arch_From_A_Wall_At_The_Levels_Height_Keeps_That_Height()
    {
        // Arrange: no height of its own, on a level of 2-story walls
        WallBetweenAreas();
        _map.LevelWallStories = 2;

        // Act
        _walls.Remove(10, 10, arch: true);

        // Assert
        Assert.That(_map.StoryMap[10, 10], Is.EqualTo(2));
    }

    [Test]
    public void Its_Shape_Patch_And_Door_Side_Mark_Go_With_It_But_Its_Tag_Stays()
    {
        // Arrange: a diagonal beside a door, with a patch and a tag
        WallBetweenAreas();
        _map.TileMap[10, 10] |= GameConstants.BitWall;
        _map.WallShapes[10, 10] = WallShape.SolidNE;
        _map.SetMapSpot(10, 10, 1, 401);
        _map.SetPatch(10, 10, 3);
        _map.SetTag(10, 10, Tag);

        // Act
        _walls.Remove(10, 10);

        // Assert
        Assert.That(_map.TileMap[10, 10], Is.Zero);
        Assert.That(_map.WallShapes[10, 10], Is.EqualTo(WallShape.Square));
        Assert.That(_map.MapSpot(10, 10, 1), Is.Zero);
        Assert.That(_map.GetPatch(10, 10), Is.Zero);
        Assert.That(_map.GetTag(10, 10), Is.EqualTo(Tag));
    }

    [Test]
    public void A_Secret_Trigger_On_It_Counts_As_Found()
    {
        // Arrange
        WallBetweenAreas();
        _map.SetMapSpot(10, 10, 1, SecretTrigger);

        // Act
        _walls.Remove(10, 10);

        // Assert
        Assert.That(_session.GameState.SecretCount, Is.EqualTo(1));
        Assert.That(_map.MapSpot(10, 10, 1), Is.Zero);
    }

    [Test]
    public void Something_Inside_It_Without_A_Death_State_Goes()
    {
        // Arrange: a trap shooter in the wall
        WallBetweenAreas();
        var shooter = new Entities.Actors.Monster { Name = "Shooter", TileX = 10, TileY = 10 };
        _map.GetActors().AddLast(shooter);

        // Act
        _walls.Remove(10, 10);

        // Assert
        Assert.That(shooter.IsRemoved, Is.True);
    }

    [TestCase(0, 10, "edge")]
    [TestCase(10, 10, "door")]
    [TestCase(20, 20, "isn't a wall")]
    [TestCase(30, 30, "moving pushwall")]
    public void Doors_Open_Floor_Moving_Pushwalls_And_The_Edge_Stay(int x, int y, string why)
    {
        // Arrange
        Wall(0, 10);
        _map.TileMap[10, 10] = (ushort)GameConstants.BitDoor;
        _map.TileMap[30, 30] = (ushort)GameConstants.BitWall;
        var before = _map.TileMap[x, y];

        // Act
        bool removed = _walls.Remove(x, y);

        // Assert
        Assert.That(removed, Is.False);
        Assert.That(_walls.WhyNot(x, y), Does.Contain(why));
        Assert.That(_map.TileMap[x, y], Is.EqualTo(before));
    }

    [Test]
    public void A_RemoveWall_Takes_The_Tagged_Walls_But_Not_The_Switch()
    {
        // Arrange: a switch at 20,10 and walls at 10,10 and 30,10, all tagged
        _walls.Register();
        WallBetweenAreas();
        Wall(20, 10);
        Wall(30, 10);
        foreach (var x in new[] { 10, 20, 30 })
            _map.SetTag(x, 10, Tag);
        var activation = new Entities.TriggerActivation(20, 10, ControlDir.North, new Entities.Actors.PlayerPawn(), Tag);

        // Act
        bool any = Entities.MapTriggerRegistry.Invoke("A_RemoveWall", [], activation);

        // Assert
        Assert.That(any, Is.True);
        Assert.That((_map.TileMap[10, 10], _map.TileMap[20, 10], _map.TileMap[30, 10]), Is.EqualTo((0, WallId, 0)));
    }

    [Test]
    public void Game_Removewall_Asks_For_The_Tile_And_Whether_To_Leave_An_Arch()
    {
        // Arrange
        var asked = new List<(int, int, bool)>();
        var console = new ConsoleManager();
        var scripts = new GameScripts(null!, _map, null!, null!, console, null, null!, _session, (_, _) => { })
        {
            RemoveWall = (x, y, arch) => { asked.Add((x, y, arch)); return true; },
        };
        scripts.LuaCommand(["x = 1"]);

        // Act
        scripts.Scripts!.Execute("print(game.removewall(10, 10), game.removewall(11, 12, 'arch'))");
        var error = scripts.Scripts!.Execute("game.removewall(-1, 3)");

        // Assert
        Assert.That(asked, Is.EqualTo(new[] { (10, 10, false), (11, 12, true) }));
        Assert.That(console.Scrollback, Has.Some.EqualTo("true\ttrue"));
        Assert.That(error, Does.Contain("isn't a tile on the map"));
    }
}
