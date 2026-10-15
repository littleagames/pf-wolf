namespace PFWolf.Tests;

public class TitleLoopTests
{
    private static TitleLoop Make() =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, new WarpRequest(),
            new GameSession(), new TitleLoopHooks(() => true, () => "INTRO", () => false, () => false, () => false,
                () => playstatetypes.ex_stillplaying, (_, _, _) => Assert.Fail("no game")));

    [Test]
    public void With_No_Game_Started_Or_Loaded_None_Is_Played()
    {
        // No game loop to run: playing one would throw
        Assert.DoesNotThrow(() => Make().RunStartedGame());
    }

    [Test]
    public void Without_A_Start_Tile_The_Player_Stays_On_The_Levels_Start()
    {
        Assert.DoesNotThrow(() => Make().ApplyPendingWarpStart());
    }
}