using PFWolf.Assets;

namespace PFWolf.Tests;

public class HudMessageKeyedTests
{
    private HudMessageManager _messages = null!;

    [SetUp]
    public void Make() =>
        _messages = new HudMessageManager(new Lazy<AssetManager>(() => new AssetManager()), new Lazy<ConsoleManager>(new ConsoleManager()))
        {
            EnabledSetting = false,     // keyed messages show with messages off too
        };

    [Test]
    public void A_Keyed_Message_Is_Kept_Up_To_Date_In_Its_Place()
    {
        // Arrange: a clock, then a power-up's timer under it
        _messages.ShowKeyed("clock", "3:00");
        _messages.ShowKeyed("god", "0:30");

        // Act: the clock ticks
        _messages.ShowKeyed("clock", "2:59");

        // Assert: still first, with the new time
        Assert.That(_messages.Messages.Select(m => m.Text), Is.EqualTo(new[] { "2:59", "0:30" }));
    }

    [Test]
    public void No_Text_Takes_It_Down()
    {
        _messages.ShowKeyed("god", "0:01");

        _messages.ShowKeyed("god", null);

        Assert.That(_messages.Messages, Is.Empty);
    }

    [Test]
    public void Its_Time_Starts_Again_With_Each_Update()
    {
        _messages.ShowKeyed("god", "0:30");
        _messages.Tick(50);
        int before = _messages.Messages[0].TicsLeft;

        _messages.ShowKeyed("god", "0:29");

        Assert.That(_messages.Messages[0].TicsLeft, Is.GreaterThan(before));
    }
}
