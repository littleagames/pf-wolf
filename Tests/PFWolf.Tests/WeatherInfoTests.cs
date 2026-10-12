using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Tests;

public class WeatherInfoTests
{
    [Test]
    public void Game_Info_Gives_Weather_Kinds_And_Each_Level_Its_Weather()
    {
        // Arrange
        const string yaml =
            "default-map:\n" +
            "  weather: drizzle\n" +
            "weather-types:\n" +
            "  drizzle:\n" +
            "    kind: rain\n" +
            "    count: 200\n" +
            "    fall-speed: 4.5\n" +
            "    wind-x: -1\n" +
            "    wind-y: 0.5\n" +
            "    length: 8\n" +
            "    color: \"#808890\"\n" +
            "    distance: 6\n" +
            "  flurry: { kind: snow, size: 2, drift: 3 }\n" +
            "maps:\n" +
            "  MAP02:\n" +
            "    weather: none\n";

        // Act
        var info = YamlDataEntryLoader.Deserialize<GameInfoAsset>(yaml);

        // Assert
        Assert.That(info.DefaultMap.Weather, Is.EqualTo("drizzle"));
        Assert.That(info.Maps["MAP02"].Weather, Is.EqualTo("none"));

        var drizzle = info.WeatherTypes["drizzle"];
        Assert.That(drizzle.Kind, Is.EqualTo("rain"));
        Assert.That(drizzle.Count, Is.EqualTo(200));
        Assert.That(drizzle.FallSpeed, Is.EqualTo(4.5));
        Assert.That(drizzle.WindX, Is.EqualTo(-1));
        Assert.That(drizzle.WindY, Is.EqualTo(0.5));
        Assert.That(drizzle.Length, Is.EqualTo(8));
        Assert.That(drizzle.Color, Is.EqualTo("#808890"));
        Assert.That(drizzle.Distance, Is.EqualTo(6));

        // What's left out keeps its default
        var flurry = info.WeatherTypes["flurry"];
        Assert.That(flurry.Kind, Is.EqualTo("snow"));
        Assert.That(flurry.Size, Is.EqualTo(2));
        Assert.That(flurry.Drift, Is.EqualTo(3));
        Assert.That(flurry.Count, Is.EqualTo(new WeatherInfo().Count));
    }

    [Test]
    public void A_Level_With_No_Weather_Has_None()
    {
        // Act
        var info = YamlDataEntryLoader.Deserialize<GameInfoAsset>("maps:\n  MAP01:\n    music: GETTHEM\n");

        // Assert
        Assert.That(info.DefaultMap.Weather, Is.Null);
        Assert.That(info.Maps["MAP01"].Weather, Is.Null);
        Assert.That(info.WeatherTypes, Is.Empty);
    }
}
