using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class MissilesTests
{
    private const int Tile = 1 << MapConstants.TILESHIFT;

    private const string Yaml = """
        Inventory:
          properties:
            "inventory.amount": 1
            "inventory.maxamount": 1
        Player:
          properties:
            "player.maxhealth": 100
        """;

    private MapManager _map = null!;
    private Doors _doors = null!;
    private GameSession _session = null!;
    private Missiles _missiles = null!;
    private List<(string Sound, Entities.Actors.Actor At)> _sounds = null!;
    private bool _died;

    [SetUp]
    public void MakeLevel()
    {
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session)
        {
            tilemap = new byte[MapManager.MAPSIZE, MapManager.MAPSIZE],
            actorat = new Actor?[MapManager.MAPSIZE, MapManager.MAPSIZE],
        };
        _doors = new Doors(null!, null!, _map, null!, null!, _session, null!);
        _sounds = [];
        _died = false;

        // Acting as a second player: no damage flash, which only the local player has
        var inventory = new InventoryManager(ActorDefs.Metadata(Yaml));
        _session.players.Add(new Entities.PlayerState());
        _session.ActingChanged = p => inventory.SetHolder(p.Items);
        _session.SetActing(_session.players[1]);
        _session.playerstate.playerclass = "Player";
        _session.playerstate.health = 100;
        _session.playerstate.Pawn = new PlayerPawn { State = _session.playerstate, X = 10 * Tile + Tile / 2, Y = 10 * Tile + Tile / 2 };

        var bar = new StatusBarDrawing(null!, null, null!, null!, inventory, null!, null!, null!, _session,
            new StatusBarHooks(() => false, map => map, (text, _, _) => [text]));
        var classes = new PlayerClassRules(null!, inventory, _session);
        var weapons = new PlayerWeapons(inventory, null!, bar, _session, new PlayerWeaponsHooks(() => false, _ => { }));
        var stats = new PlayerStats(null!, null!, null!, inventory, bar, classes, weapons, _session,
            new PlayerStatsHooks(() => 0, () => false, () => false, () => 1, () => { }, () => { }, _ => { }));
        _missiles = new Missiles(_map, null!, _doors, weapons, null!, stats, _session,
            new MissileHooks(_ => true, (sound, at) => _sounds.Add((sound, at)), (_, damage, _) => _playerHurts.Add(damage), () => { }, () => _died));
        _playerHurts = [];
    }

    private List<int> _playerHurts = null!;

    private static Monster Thing(params (string Key, object Value)[] properties)
    {
        var thing = new Monster { Name = "Thing" };
        foreach (var (key, value) in properties)
            thing.Properties[key] = value;
        return thing;
    }

    [Test]
    public void A_Missile_Without_A_Speed_Flies_At_The_Default()
    {
        Assert.That(_missiles.ReadMissileSpeed(Thing()), Is.EqualTo(0x1c00));
    }

    [Test]
    public void A_Missiles_Speed_Is_Its_Own_Plus_Up_To_Its_Random_Part()
    {
        // Arrange
        var rocket = Thing(("speed", "8192"), ("speed.random", "256"));

        // Act
        var speeds = Enumerable.Range(0, 50).Select(_ => _missiles.ReadMissileSpeed(rocket)).ToList();

        // Assert: random 0-255 of 256
        Assert.That(speeds, Has.All.InRange(8192, 8192 + 255));
        Assert.That(speeds.Distinct().Count(), Is.GreaterThan(1));
    }

    [Test]
    public void Open_Floor_And_Open_Doors_Let_A_Blast_Through_And_Walls_And_Shut_Doors_Dont()
    {
        // Arrange
        _map.actorat[1, 1] = new Wall(1);
        _map.tilemap[2, 1] = 5;                                  // a wall with nothing on actorat (a moving pushwall's)
        _map.actorat[3, 1] = new Door(0);
        _map.tilemap[3, 1] = (byte)(GameConstants.BIT_DOOR | 0);
        _doors.doorobjlist[0] = new doorobj_t { action = dooractiontypes.dr_closed };
        _map.actorat[4, 1] = new Door(1);
        _map.tilemap[4, 1] = (byte)(GameConstants.BIT_DOOR | 1);
        _doors.doorobjlist[1] = new doorobj_t { action = dooractiontypes.dr_open };

        // Act / Assert
        Assert.That(_missiles.IsSolidForBlast(0, 1), Is.False);
        Assert.That(_missiles.IsSolidForBlast(1, 1), Is.True);
        Assert.That(_missiles.IsSolidForBlast(2, 1), Is.True);
        Assert.That(_missiles.IsSolidForBlast(3, 1), Is.True);
        Assert.That(_missiles.IsSolidForBlast(4, 1), Is.False);
    }

    [Test]
    public void A_Barrier_Blocks_Its_Tile_And_Frees_It_Again()
    {
        // Arrange
        var barrier = new Monster { Name = "Barrier", TileX = 5, TileY = 6 };

        // Act / Assert
        _missiles.A_BlockTile(barrier);
        Assert.That(_map.actorat[5, 6], Is.TypeOf<BlockingActor>());
        _missiles.A_UnblockTile(barrier);
        Assert.That(_map.actorat[5, 6], Is.Null);
    }

    [Test]
    public void Freeing_A_Tile_Leaves_A_Wall_There_Alone()
    {
        // Arrange
        var barrier = new Monster { Name = "Barrier", TileX = 5, TileY = 6 };
        var wall = new Wall(1);
        _map.actorat[5, 6] = wall;

        // Act
        _missiles.A_BlockTile(barrier);
        _missiles.A_UnblockTile(barrier);

        // Assert
        Assert.That(_map.actorat[5, 6], Is.SameAs(wall));
    }

    [Test]
    public void A_Missile_With_No_Death_State_Just_Goes()
    {
        var missile = Thing();
        _missiles.Detonate(missile);
        Assert.That(missile.IsRemoved, Is.True);
    }

    [Test]
    public void Out_Of_Deathmatch_Missiles_Fly_Through_Players()
    {
        Assert.That(_missiles.RivalHitBy(Thing()), Is.Null);
    }

    // A thing dx tiles east of a blast at the middle of tile (10, 10)
    private static (Monster Blast, Monster Thing) BlastAndThing(double dx)
    {
        var blast = new Monster { Name = "Rocket", X = 10 * Tile + Tile / 2, Y = 10 * Tile + Tile / 2 };
        var thing = new Monster { Name = "Thing", X = blast.X + (int)(dx * Tile), Y = blast.Y };
        return (blast, thing);
    }

    [TestCase(0.0, 600)]
    [TestCase(1.0, 600)]        // all of it within a tile
    [TestCase(2.0, 150)]        // a quarter at two
    [TestCase(2.5, 96)]
    [TestCase(3.0, 0)]          // nothing at the radius
    public void A_Falloff_Blast_Weakens_With_The_Square_Of_The_Distance(double tiles, int damage)
    {
        var (blast, thing) = BlastAndThing(tiles);
        Assert.That(Missiles.BlastFalloff(600, 3, blast, thing), Is.EqualTo(damage));
    }

    [Test]
    public void A_Falloff_Blast_Reaches_Straight_Out_Not_Round_Corners_Of_Its_Square()
    {
        // Arrange: 2.5 tiles each way, inside the 3-tile square but 3.5 tiles off
        var (blast, thing) = BlastAndThing(2.5);
        thing.Y += (int)(2.5 * Tile);

        // Act / Assert
        Assert.That(Missiles.BlastFalloff(600, 3, blast, thing), Is.Zero);
    }

    [TestCase(10, 150)]         // on the blast: capped at playermax
    [TestCase(12, 150)]         // two tiles off: a quarter, 150
    [TestCase(13, 0)]           // out of reach
    public void A_Players_Share_Of_A_Falloff_Blast_Is_Capped(int tileX, int hurt)
    {
        // Arrange
        var pawn = _session.playerstate.Pawn!;
        (pawn.X, pawn.TileX, pawn.TileY) = (tileX * Tile + Tile / 2, (byte)tileX, 10);
        var blast = new Monster { Name = "Rocket", X = 10 * Tile + Tile / 2, Y = 10 * Tile + Tile / 2, TileX = 10, TileY = 10 };

        // Act
        _missiles.A_Explode(blast, ["600", "600", "3", "hurtplayer", "falloff", "playermax:150"]);

        // Assert
        Assert.That(_playerHurts, Is.EqualTo(hurt > 0 ? new[] { hurt } : Array.Empty<int>()));
    }

    [Test]
    public void Without_Falloff_A_Blast_Hurts_The_Same_All_Through()
    {
        // Arrange: two tiles off
        var pawn = _session.playerstate.Pawn!;
        (pawn.X, pawn.TileX, pawn.TileY) = (12 * Tile + Tile / 2, 12, 10);
        var blast = new Monster { Name = "Rocket", X = 10 * Tile + Tile / 2, Y = 10 * Tile + Tile / 2, TileX = 10, TileY = 10 };

        // Act
        _missiles.A_Explode(blast, ["100", "100", "2", "hurtplayer"]);

        // Assert
        Assert.That(_playerHurts, Is.EqualTo(new[] { 100 }));
    }

    [TestCase(30, false, true, 150)]     // a wall across x: x turned back
    [TestCase(30, true, false, 330)]     // a wall across y: y turned back
    [TestCase(30, false, false, 210)]    // a corner: straight back
    [TestCase(300, false, true, 240)]
    public void A_Bounce_Turns_Off_The_Wall_It_Met(int angle, bool xClear, bool yClear, int turned)
    {
        Assert.That(Missiles.BounceAngle(angle, xClear, yClear), Is.EqualTo(turned));
    }

    [Test]
    public void A_Bouncing_Missile_Goes_Back_Slows_To_Half_And_Sounds()
    {
        // Arrange: moved 0.25 tile east (as A_Missile does) into what the test's move check calls clear both ways
        var grenade = Thing(("bouncesound", "grenade/bounce"));
        (grenade.X, grenade.Y, grenade.Angle, grenade.Speed) = (5 * Tile + Tile / 4, 5 * Tile, 0, 12288);

        // Act
        _missiles.Bounce(grenade, Tile / 4, 0);

        // Assert
        Assert.That(grenade.X, Is.EqualTo(5 * Tile));
        Assert.That(grenade.Angle, Is.EqualTo(180));
        Assert.That(grenade.Speed, Is.EqualTo(6144));
        Assert.That(_sounds, Is.EqualTo(new[] { ("grenade/bounce", (Entities.Actors.Actor)grenade) }));
    }

    [Test]
    public void A_Thrown_Missile_Rises_Slows_And_Falls_To_Its_Floor()
    {
        // Act / Assert: 2 texels a tic up, slowing by 0.5 a tic
        var (z, speed) = Missiles.Fall(32, 2, 0.5, -32, 1);
        Assert.That((z, speed), Is.EqualTo((33.5, 1.5)));
        for (int tic = 0; tic < 30; tic++)
            (z, speed) = Missiles.Fall(z, speed, 0.5, -32, 1);
        Assert.That((z, speed), Is.EqualTo((-32.0, 0.0)));       // on its floor, and stays
    }

    [Test]
    public void Without_Gravity_A_Missile_Keeps_Its_Height()
    {
        Assert.That(Missiles.Fall(10, 0, 0, 0, 5), Is.EqualTo((10.0, 0.0)));
    }

    [TestCase(0, 0)]        // as its art puts it
    [TestCase(32, -64)]     // half a story up, at 64 pixels to half a story: 64 pixels higher
    [TestCase(-16, 32)]
    public void A_Raised_Sprite_Is_Drawn_That_Much_Higher(double z, int moved)
    {
        // a sprite whose origin is its bottom row (64 texels down), 64 pixels to 32 texels
        Assert.That(Rendering.SpriteScaler.RaisedTop(200, 64, z, 64) - Rendering.SpriteScaler.RaisedTop(200, 64, 0, 64), Is.EqualTo(moved));
    }

    [TestCase(0, 25)]       // not cooked: a quarter
    [TestCase(10, 25)]
    [TestCase(60, 60)]
    [TestCase(100, 100)]
    public void A_Cooked_Throw_Goes_As_Hard_As_It_Was_Cooked_A_Quarter_At_Least(int power, int percent)
    {
        Assert.That(Missiles.ThrowSpeed(10000, power), Is.EqualTo(100 * percent));
    }

    [Test]
    public void A_Shock_Hurts_The_Player_Close_By_With_Its_Sound()
    {
        // Arrange: a tile away, a sure chance
        var fence = new Monster { Name = "Fence", X = _session.player.X + Tile, Y = _session.player.Y };

        // Act
        _missiles.A_ShockNearby(fence, ["5", "1.5", "256", "zap"]);

        // Assert
        Assert.That(_session.playerstate.health, Is.EqualTo(95));
        Assert.That(_sounds, Is.EqualTo(new[] { ("zap", (Entities.Actors.Actor)fence) }));
    }

    [Test]
    public void A_Shock_Doesnt_Reach_Further_Than_Its_Distance_Or_A_Dead_Player()
    {
        // Arrange
        var fence = new Monster { Name = "Fence", X = _session.player.X + 2 * Tile, Y = _session.player.Y };

        // Act / Assert
        _missiles.A_ShockNearby(fence, ["5", "1.5", "256"]);
        Assert.That(_session.playerstate.health, Is.EqualTo(100));

        fence.X = _session.player.X;
        _died = true;
        _missiles.A_ShockNearby(fence, ["5", "1.5", "256"]);
        Assert.That(_session.playerstate.health, Is.EqualTo(100));
    }
}
