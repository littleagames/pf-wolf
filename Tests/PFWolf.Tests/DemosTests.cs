namespace PFWolf.Tests;

public class DemosTests
{
    private DemoBuffer _buffer = null!;
    private bool? _recording;
    private Demos _demos = null!;

    [SetUp]
    public void Make()
    {
        _buffer = new DemoBuffer();
        _recording = null;
        _demos = new Demos(null!, null!, null!, null!, null!, null!, _buffer, null!, null!,
            new DemosHooks((_, _, _, _) => Assert.Fail("no game"), () => 3, () => { }, () => { }, _ => { }, () => { },
                _ => { }, on => _recording = on, () => false, _ => "", () => { }, () => "INTRO"));
    }

    [Test]
    public void A_Recording_Starts_With_Its_Map_And_Room_For_The_Length()
    {
        // Act: MAP05
        _demos.StartDemoRecord(4);

        // Assert: id's header is the map less one, two bytes of length and a pad byte; then
        // 3 bytes a frame, up to the buffer's end
        Assert.That(_buffer.demoData, Has.Length.EqualTo(Demos.MAXDEMOSIZE));
        Assert.That(_buffer.demoData[0], Is.EqualTo(4));
        Assert.That((_buffer.demoptr, _buffer.lastdemoptr), Is.EqualTo((4, Demos.MAXDEMOSIZE)));
        Assert.That(_recording, Is.True);
    }

    [Test]
    public void With_Nothing_Asked_For_The_Title_Loop_Plays_And_Records_Nothing()
    {
        // Act / Assert: no game is started (NewGame fails the test)
        Assert.That(_demos.RecordPendingDemo(), Is.False);
        _demos.PlayPendingDemo();
        Assert.That(_recording, Is.Null);
    }
}
