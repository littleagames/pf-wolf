using PFWolf.Assets;

namespace PFWolf.Tests;

/// <summary>When the status bar's radar shows (Ui/Radar.cs Shows): maps-only, Midnight Assault's corner map</summary>
public class RadarShowsTests
{
    private static Radar Radar(bool mapSaysRadar, bool fullScreen = false) =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, new GameSession { InGame = true },
            new Configuration.GameSettings { ViewSize = fullScreen ? 21 : 19 }, () => mapSaysRadar);

    [Test]
    public void A_Radar_For_Every_Map_Shows_Everywhere()
    {
        Assert.That(Radar(mapSaysRadar: false).Shows(new StatusBarElement()), Is.True);
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void A_Maps_Only_Radar_Shows_Where_The_Map_Turns_It_On(bool mapSaysRadar, bool shows)
    {
        Assert.That(Radar(mapSaysRadar).Shows(new StatusBarElement { MapsOnly = true }), Is.EqualTo(shows));
    }

    [Test]
    public void No_Radar_With_The_View_Full_Screen()
    {
        Assert.That(Radar(mapSaysRadar: true, fullScreen: true).Shows(new StatusBarElement { MapsOnly = true }), Is.False);
    }

    [Test]
    public void A_Map_Reads_Its_Radar_Switch()
    {
        var map = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<MapInfo>("radar: true\n");
        Assert.That(map.Radar, Is.True);
    }
}
