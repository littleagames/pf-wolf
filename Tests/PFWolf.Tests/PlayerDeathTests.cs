namespace PFWolf.Tests;

public class PlayerDeathTests
{
    private const string Yaml = """
        DeathCam:
          states:
            Spawn:
              - sprite: DCAM
                frames: [A, B]
                tics-per-frame: 5
        """;

    [Test]
    public void The_Death_Cams_Banner_Is_Made_Once_And_Animates_With_The_Tics()
    {
        // Arrange
        var map = new MapManager(new Lazy<AssetManager>(() => new AssetManager()), null!, new GameSession())
        {
            tilemap = new ushort[MapManager.MAPSIZE, MapManager.MAPSIZE],
            actorat = new Actor?[MapManager.MAPSIZE, MapManager.MAPSIZE],
        };
        var session = new GameSession { tics = 5 };
        var death = new PlayerDeath(null!, null!, null!, null!, null!, new InventoryManager(ActorDefs.Metadata(Yaml)), null!, map,
            null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, session, null!, new Configuration.GameSettings(), null!);
        var player = new Entities.Actors.Monster { Name = "Player" };

        // Act / Assert: made, and moved on a frame by this frame's tics
        death.T_DeathCam(player);
        var banner = death.deathCamSprite;
        Assert.That(banner?.CurrentState?.FrameLetter, Is.EqualTo("B"));

        // the same one, each frame after
        death.T_DeathCam(player);
        Assert.That(death.deathCamSprite, Is.SameAs(banner));
    }

    [Test]
    public void Death_Keeps_Puts_Items_Back_As_They_Came_Into_The_Level()
    {
        // Arrange: came in with 2 medkits and no grenades; now (the starting items given) 0 and 3
        var inventory = new InventoryManager(ActorDefs.Metadata("""
            Inventory:
              properties:
                "inventory.maxamount": 9
            Medkit:
              parent: Inventory
            Grenade:
              parent: Inventory
            Knife:
              parent: Inventory
            """));
        var items = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        inventory.SetHolder(items);
        inventory.Give("Grenade", 3);
        inventory.Give("Knife", 1);
        var death = new PlayerDeath(null!, null!, null!, null!, null!, inventory, null!, null!,
            null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, new GameSession(), null!, new Configuration.GameSettings(), null!);

        // Act
        death.KeepItems(["Medkit", "Grenade"], new Dictionary<string, int> { ["Medkit"] = 2 });

        // Assert: those two as they were, the rest left alone
        Assert.That((inventory.GetCount("Medkit"), inventory.GetCount("Grenade"), inventory.GetCount("Knife")), Is.EqualTo((2, 0, 1)));
    }

    [Test]
    public void Lives_Count_Unless_Game_Info_Says_Not()
    {
        Assert.That(new Assets.GameInfoAsset().Lives, Is.True);
        var noLives = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<Assets.GameInfoAsset>("lives: false\ndeath-keeps: [Medkit]\n");
        Assert.That((noLives.Lives, noLives.DeathKeeps.Single()), Is.EqualTo((false, "Medkit")));
    }
}
