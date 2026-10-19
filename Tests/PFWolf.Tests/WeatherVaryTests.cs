using PFWolf.Assets;

namespace PFWolf.Tests;

public class WeatherVaryTests
{
    private static Weather MakeWeather() =>
        new(null!, null!, null!, null!, new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.NoShade),
            new GameSession(), half => half, (_, _) => false);

    private static readonly WeatherInfo Varying = new() { Kind = "snow", Vary = true, VaryChance = 0, VaryRate = 0.1 };

    [TestCase(0.1, 0.0)]
    [TestCase(0.99, 1.0)]
    public void A_Level_Starts_With_None_Or_All(double roll, double amount)
    {
        var weather = MakeWeather();

        weather.StartAmount(Varying, roll);

        Assert.That(weather.Amount, Is.EqualTo(amount));
    }

    [Test]
    public void Some_Is_A_Little()
    {
        var weather = MakeWeather();

        weather.StartAmount(Varying, 0.5);

        Assert.That(weather.Amount, Is.InRange(0.05, 0.25));
    }

    [Test]
    public void Weather_That_Doesnt_Vary_Is_All_There()
    {
        var weather = MakeWeather();

        weather.StartAmount(Varying with { Vary = false }, 0.1);

        Assert.That(weather.Amount, Is.EqualTo(1));
    }

    [Test]
    public void Thickening_Goes_On_Until_Its_All_There()
    {
        // Arrange: none falling, starting to thicken at a tenth a second
        var weather = MakeWeather();
        weather.StartAmount(Varying, 0.1);
        weather.AmountChange = 1;
        weather.UpdateAmount(Varying, 0);

        // Act: five seconds, then fifteen more
        weather.UpdateAmount(Varying, 5 * 70);
        double after5 = weather.Amount;
        weather.UpdateAmount(Varying, 20 * 70);

        // Assert
        Assert.That(after5, Is.EqualTo(0.5).Within(1e-9));
        Assert.That(weather.Amount, Is.EqualTo(1));
        Assert.That(weather.AmountChange, Is.EqualTo(0));
    }

    [Test]
    public void Every_Particle_Has_Its_Place_In_The_Spread()
    {
        var places = Enumerable.Range(0, 1000).Select(Weather.SpreadPlace).ToList();

        Assert.That(places.All(p => p is >= 0 and < 1), Is.True);
        // about half of them fall at half the amount
        Assert.That(places.Count(p => p < 0.5), Is.InRange(480, 520));
    }
}
