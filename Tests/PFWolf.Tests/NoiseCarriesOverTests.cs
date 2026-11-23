using PFWolf.Assets;

namespace PFWolf.Tests;

/// <summary>
/// game-info noise-carries-over (Blake Stone): a noise is still heard the tic after it's made, so
/// an enemy thinking before the one that shot hears it too. Off, as the original games, only that
/// tic's noise counts.
/// </summary>
public class NoiseCarriesOverTests
{
    [TestCase(false, false, false, ExpectedResult = false)]
    [TestCase(true, false, false, ExpectedResult = true)]      // this tic's noise, either way
    [TestCase(true, false, true, ExpectedResult = true)]
    [TestCase(false, true, false, ExpectedResult = false)]     // last tic's: the original's way, gone
    [TestCase(false, true, true, ExpectedResult = true)]       // last tic's, carried over
    public bool A_Noise_Is_Heard(bool thisTic, bool lastTic, bool carriesOver) =>
        new GameSession { MadeNoise = thisTic, NoiseLastTic = lastTic, NoiseCarriesOver = carriesOver }.NoiseHeard;

    [Test]
    public void Game_Info_Turns_It_On_And_Its_Off_Without()
    {
        Assert.That(new GameInfoAsset().NoiseCarriesOver, Is.False);
        Assert.That(PFWolf.Loaders.YamlDataEntryLoader.Deserialize<GameInfoAsset>("noise-carries-over: true\n").NoiseCarriesOver, Is.True);
    }
}
