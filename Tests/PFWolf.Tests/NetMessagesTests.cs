using LiteNetLib.Utils;
using PFWolf.Entities;
using PFWolf.Networking;
using GameMode = PFWolf.Program.GameMode;

namespace PFWolf.Tests;

public class NetMessagesTests
{
    private static readonly NetIdentity Identity = new("0.3.2", "wolf3d", "wolf3d-gt", "switch-demo 1.0", "abc123");

    private static T RoundTrip<T>(Action<NetDataWriter> write, Func<NetDataReader, T> read)
    {
        var writer = new NetDataWriter();
        write(writer);
        var reader = new NetDataReader(writer);
        var result = read(reader);
        Assert.That(reader.AvailableBytes, Is.Zero, "everything written should be read");
        return result;
    }

    private static TicCmd Cmd(uint buttons, int x, int y, int strafe, double pitch = 0, bool center = false) =>
        new() { Buttons = buttons, ControlX = x, ControlY = y, ControlStrafe = strafe, Pitch = pitch, CenterView = center };

    [Test]
    public void NetIdentity_Round_Trips()
    {
        Assert.That(RoundTrip(Identity.Write, NetIdentity.Read), Is.EqualTo(Identity));
    }

    [Test]
    public void NetIdentity_Matches_Itself_And_Ignores_Case_Where_It_Should()
    {
        // Arrange
        var other = Identity with { GamePack = "WOLF3D", GameRelease = "Wolf3D-GT", Mods = "SWITCH-DEMO 1.0" };

        // Act / Assert
        Assert.That(Identity.Mismatch(Identity), Is.Null);
        Assert.That(Identity.Mismatch(other), Is.Null);
    }

    [Test]
    public void NetIdentity_Names_Each_Mismatch()
    {
        Assert.That(Identity.Mismatch(Identity with { GamePack = "spear" }), Does.Contain("spear"));
        Assert.That(Identity.Mismatch(Identity with { GameRelease = "wolf3d-apogee" }), Does.Contain("wolf3d-apogee"));
        Assert.That(Identity.Mismatch(Identity with { Engine = "0.3.3" }), Does.Contain("0.3.3"));
        Assert.That(Identity.Mismatch(Identity with { Mods = "" }), Does.Contain("none"));
        Assert.That(Identity.Mismatch(Identity with { ContentHash = "def456" }), Does.Contain("pfwolf.pk3"));
    }

    [Test]
    public void NetIdentity_Engine_And_Hash_Are_Case_Sensitive()
    {
        Assert.That(Identity.Mismatch(Identity with { ContentHash = "ABC123" }), Is.Not.Null);
    }

    [Test]
    public void HostInfo_Round_Trips()
    {
        // Arrange
        var info = new HostInfo(12345, "Host", "wolf3d", 2, 4, true, GameMode.Deathmatch, "E1M1");

        // Act / Assert
        Assert.That(RoundTrip(info.Write, HostInfo.Read), Is.EqualTo(info));
    }

    [Test]
    public void Hello_Round_Trips()
    {
        // Arrange
        var hello = new Hello(NetProtocol.Version, Identity, "Player", "Soldier");

        // Act
        var read = Hello.Read(new NetDataReader(hello.Write()));

        // Assert
        Assert.That(read, Is.EqualTo(hello));
    }

    [Test]
    public void Hello_From_Another_Version_Keeps_Only_The_Version()
    {
        // Arrange
        var hello = new Hello(NetProtocol.Version + 1, Identity, "Player", "Soldier");

        // Act
        var read = Hello.Read(new NetDataReader(hello.Write()));

        // Assert
        Assert.That(read, Is.Not.Null);
        Assert.That(read!.Version, Is.EqualTo(NetProtocol.Version + 1));
        Assert.That(read.Name, Is.Empty);
    }

    [Test]
    public void Hello_Read_Returns_Null_For_Other_Data()
    {
        // Arrange
        var other = new NetDataWriter();
        other.Put("SOMETHING ELSE");
        other.Put(NetProtocol.Version);

        // Act / Assert
        Assert.That(Hello.Read(new NetDataReader(other)), Is.Null);
        Assert.That(Hello.Read(new NetDataReader(new NetDataWriter())), Is.Null);
    }

    [Test]
    public void Hello_Read_Returns_Null_When_Cut_Short()
    {
        // Arrange
        var full = new Hello(NetProtocol.Version, Identity, "Player", "Soldier").Write();
        var cut = new NetDataWriter();
        cut.Put(full.Data, 0, full.Length - 4);

        // Act / Assert
        Assert.That(Hello.Read(new NetDataReader(cut)), Is.Null);
    }

    [Test]
    public void LobbySettings_Round_Trip()
    {
        // Arrange
        var settings = new LobbySettings(GameMode.Deathmatch, 2, 3, Monsters: true, FragLimit: 50, TimeLimit: 15, ItemRespawn: false, Map: "DM03");

        // Act / Assert
        Assert.That(RoundTrip(settings.Write, LobbySettings.Read), Is.EqualTo(settings));
    }

    [Test]
    public void LobbyPlayer_List_Round_Trips_In_Order()
    {
        // Arrange
        List<LobbyPlayer> players = [new(0, "Host", "Soldier", true), new(1, "Guest", "Medic", false), new(3, "Late", "", true)];

        // Act
        var read = RoundTrip(w => LobbyPlayer.WriteList(w, players), LobbyPlayer.ReadList);

        // Assert
        Assert.That(read, Is.EqualTo(players));
    }

    [Test]
    public void TicCmd_Round_Trips()
    {
        // Arrange
        var cmd = Cmd(0b1011, -100, 250, 37, pitch: 1.5, center: true);

        // Act
        var read = RoundTrip(w => TicCmdCodec.Write(w, cmd), TicCmdCodec.Read);

        // Assert
        Assert.That(read, Is.EqualTo(cmd));
    }

    [Test]
    public void TicCmd_Clamps_Controls_To_Shorts()
    {
        // Act
        var read = TicCmdCodec.RoundTrip(Cmd(0, 100_000, -100_000, int.MaxValue));

        // Assert
        Assert.That((read.ControlX, read.ControlY, read.ControlStrafe), Is.EqualTo((32767, -32768, 32767)));
    }

    [Test]
    public void TicCmd_RoundTrip_Is_Stable()
    {
        // Arrange: the pitch goes over as a float, so the first trip may change it; later ones mustn't
        var once = TicCmdCodec.RoundTrip(Cmd(1, 1, 1, 1, pitch: 0.1));

        // Act
        var twice = TicCmdCodec.RoundTrip(once);

        // Assert
        Assert.That(twice, Is.EqualTo(once));
    }

    [Test]
    public void TicBundle_Round_Trips_With_A_Join()
    {
        // Arrange
        var bundle = new TicBundle(3, 1234, 0b0100, new NetJoin(2, "Newcomer", "Soldier"),
            [Cmd(1, 2, 3, 4), Cmd(0, 0, 0, 0), Cmd(uint.MaxValue, -5, -6, -7)]);

        // Act
        var read = RoundTrip(bundle.Write, TicBundle.Read);

        // Assert
        Assert.That((read.Level, read.Step, read.Left, read.Join), Is.EqualTo((bundle.Level, bundle.Step, bundle.Left, bundle.Join)));
        Assert.That(read.Cmds, Is.EqualTo(bundle.Cmds));
    }

    [Test]
    public void TicBundle_Round_Trips_Without_A_Join()
    {
        // Arrange
        var bundle = new TicBundle(1, 0, 0, null, [Cmd(1, 0, 0, 0)]);

        // Act
        var read = RoundTrip(bundle.Write, TicBundle.Read);

        // Assert
        Assert.That(read.Join, Is.Null);
        Assert.That(read.Cmds, Is.EqualTo(bundle.Cmds));
    }

    [Test]
    public void TicBundle_HasLeft_Reads_Each_Players_Bit()
    {
        // Arrange
        var bundle = new TicBundle(1, 0, 0b1010, null, []);

        // Act / Assert
        Assert.That(Enumerable.Range(0, 4).Select(bundle.HasLeft), Is.EqualTo(new[] { false, true, false, true }));
    }

    [Test]
    public void StartGameInfo_Round_Trips()
    {
        // Arrange
        var start = new StartGameInfo(new LobbySettings(GameMode.Coop, 0, 2), 77,
            [new(0, "Host", "Soldier", true), new(1, "Guest", "Soldier", true)]);

        // Act
        var read = RoundTrip(start.Write, StartGameInfo.Read);

        // Assert
        Assert.That((read.Settings, read.Seed), Is.EqualTo((start.Settings, start.Seed)));
        Assert.That(read.Players, Is.EqualTo(start.Players));
    }

    [Test]
    public void NetRefusal_Round_Trips_A_Reason()
    {
        Assert.That(NetRefusal.Read(new NetDataReader(NetRefusal.Write("The game is full."))), Is.EqualTo("The game is full."));
    }

    [Test]
    public void NetRefusal_Read_Returns_Null_For_No_Or_Other_Data()
    {
        // Arrange
        var other = new NetDataWriter();
        other.Put("NOT PFWOLF");
        other.Put("reason");

        // Act / Assert
        Assert.That(NetRefusal.Read(null), Is.Null);
        Assert.That(NetRefusal.Read(new NetDataReader(new NetDataWriter())), Is.Null);
        Assert.That(NetRefusal.Read(new NetDataReader(other)), Is.Null);
    }

    [Test]
    public void Message_Starts_With_Its_Type()
    {
        // Act
        var writer = NetProtocol.Message(NetMessage.Chat);

        // Assert
        Assert.That(writer.Length, Is.EqualTo(1));
        Assert.That(writer.Data[0], Is.EqualTo((byte)NetMessage.Chat));
    }
}
