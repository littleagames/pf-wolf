using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Tests;

public class WeatherTests
{
    private const long Tile = MapConstants.TileGlobal;

    private static (Weather Weather, RenderState Render) Make()
    {
        var render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, new byte[256]);
        var weather = new Weather(null!, null!, null!, null!, render, new GameSession(), half => half, (_, _) => false);
        return (weather, render);
    }

    [Test]
    public void None_Or_Nothing_Is_No_Weather()
    {
        // Arrange
        var (weather, _) = Make();

        // Act / Assert
        weather.SetWeather(null);
        Assert.That(weather.LevelWeather, Is.Null);
        weather.SetWeather(WeatherInfo.None);
        Assert.That((weather.LevelWeather, weather.LevelWeatherName), Is.EqualTo(((WeatherInfo?)null, (string?)null)));
    }

    [Test]
    public void A_Point_Straight_Ahead_At_Eye_Height_Is_In_The_Middle_Of_The_View()
    {
        // Arrange: looking east from (10, 10), the view 320x160
        var (weather, render) = Make();
        (render.ViewX, render.ViewY, render.ViewCos, render.ViewSin) = ((int)(10 * Tile), (int)(10 * Tile), 0x10000, 0);
        (render.CenterX, render.CenterY, render.Scale, render.HeightNumerator, render.ViewEyeZ) = (160, 80, 256, 0x10000, 32);

        // Act
        bool seen = weather.ProjectPoint(14 * Tile, 10 * Tile, 32, out int sx, out int sy, out long nx, out _);

        // Assert
        Assert.That(seen, Is.True);
        Assert.That((sx, sy, nx), Is.EqualTo((160, 80, 4 * Tile)));
    }

    [Test]
    public void One_Higher_Up_Is_Higher_On_Screen_And_One_To_The_Right_Is_Right()
    {
        // Arrange
        var (weather, render) = Make();
        (render.ViewX, render.ViewY, render.ViewCos, render.ViewSin) = ((int)(10 * Tile), (int)(10 * Tile), 0x10000, 0);
        (render.CenterX, render.CenterY, render.Scale, render.HeightNumerator, render.ViewEyeZ) = (160, 80, 256, 0x10000, 32);

        // Act: south is to the right, looking east (y grows south)
        weather.ProjectPoint(14 * Tile, 11 * Tile, 64, out int sx, out int sy, out _, out _);

        // Assert
        Assert.That(sx, Is.GreaterThan(160));
        Assert.That(sy, Is.LessThan(80));
    }

    [Test]
    public void A_Point_Behind_Or_Too_Close_Isnt_Drawn()
    {
        var (weather, render) = Make();
        (render.ViewX, render.ViewY, render.ViewCos, render.ViewSin) = ((int)(10 * Tile), (int)(10 * Tile), 0x10000, 0);
        Assert.That(weather.ProjectPoint(8 * Tile, 10 * Tile, 32, out _, out _, out _, out _), Is.False);
        Assert.That(weather.ProjectPoint(10 * Tile + 0x100, 10 * Tile, 32, out _, out _, out _, out _), Is.False);
    }

    [Test]
    public void A_Particle_Lands_Inside_Its_Box_The_Same_Place_Every_Frame_Of_A_Fall()
    {
        // Arrange
        var (weather, _) = Make();
        int box = (int)(4 * Tile);

        // Act
        var spots = Enumerable.Range(0, 200).Select(fall => weather.WeatherSpot(7, fall, box)).ToList();

        // Assert
        Assert.That(spots, Has.All.Matches<(int X, int Y)>(s => s.X >= 0 && s.X < box && s.Y >= 0 && s.Y < box));
        Assert.That(weather.WeatherSpot(7, 3, box), Is.EqualTo(spots[3]));
        Assert.That(spots.Distinct().Count(), Is.GreaterThan(150));     // a new place each fall
    }

    [Test]
    public void Gusts_Make_The_Wind_Rise_And_Fall_About_Its_Strength()
    {
        // Arrange
        var info = new WeatherInfo { Gust = 0.5, GustTics = 100 };

        // Act / Assert
        Assert.That(Weather.WindStrength(info, 0), Is.EqualTo(1).Within(1e-9));
        Assert.That(Weather.WindStrength(info, 25), Is.EqualTo(1.5).Within(1e-9));
        Assert.That(Weather.WindStrength(info, 75), Is.EqualTo(0.5).Within(1e-9));
        Assert.That(Weather.WindStrength(new WeatherInfo(), 25), Is.EqualTo(1));
    }

    [TestCase(5L, 4L, 1L)]
    [TestCase(-1L, 4L, 3L)]
    [TestCase(-8L, 4L, 0L)]
    public void Wrapping_Round_The_Box_Never_Goes_Below_Zero(long value, long modulus, long wrapped)
    {
        Assert.That(Weather.PositiveMod(value, modulus), Is.EqualTo(wrapped));
    }
}
