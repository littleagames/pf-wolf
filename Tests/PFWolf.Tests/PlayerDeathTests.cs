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
            tilemap = new byte[MapManager.MAPSIZE, MapManager.MAPSIZE],
            actorat = new Actor?[MapManager.MAPSIZE, MapManager.MAPSIZE],
        };
        var session = new GameSession { tics = 5 };
        var death = new PlayerDeath(null!, null!, null!, null!, null!, new InventoryManager(ActorDefs.Metadata(Yaml)), null!, map,
            null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, session, null!, null!);
        var player = new Entities.Actors.Monster { Name = "Player" };

        // Act / Assert: made, and moved on a frame by this frame's tics
        death.T_DeathCam(player);
        var banner = death.deathCamSprite;
        Assert.That(banner?.CurrentState?.FrameLetter, Is.EqualTo("B"));

        // the same one, each frame after
        death.T_DeathCam(player);
        Assert.That(death.deathCamSprite, Is.SameAs(banner));
    }
}
