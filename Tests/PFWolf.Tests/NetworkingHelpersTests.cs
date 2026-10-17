using System.IO.Compression;
using System.Net;
using PFWolf.Configuration;
using PFWolf.Networking;

namespace PFWolf.Tests;

public class NetAddressesTests
{
    private static readonly int Default = NetProtocol.DefaultPort;

    [TestCase("192.168.1.20", "192.168.1.20", 0)]
    [TestCase("192.168.1.20:2000", "192.168.1.20", 2000)]
    [TestCase(" games.example.com : 2001 ", "games.example.com", 2001)]
    [TestCase("udp://10.0.0.5:3000/", "10.0.0.5", 3000)]
    [TestCase("LAN: others join at 10.0.0.5 (UPnP ok)", "10.0.0.5", 0)]
    [TestCase("Internet: 81.2.3.4:4000", "81.2.3.4", 4000)]
    [TestCase("[::1]:5000", "::1", 5000)]
    [TestCase("[fe80::1]", "fe80::1", 0)]
    [TestCase("host:99999", "host:99999", 0)]      // not a port: taken as all host
    public void Typed_Addresses_Are_Forgiven_Their_Extras(string typed, string host, int port)
    {
        Assert.That(NetAddresses.ParseAddress(typed), Is.EqualTo(((string, int)?)(host, port == 0 ? Default : port)));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("http://")]
    public void Nothing_Typed_Is_No_Address(string typed)
    {
        Assert.That(NetAddresses.ParseAddress(typed), Is.Null);
    }

    [Test]
    public void The_Default_Port_Goes_Unsaid()
    {
        Assert.That(NetAddresses.WithPort("10.0.0.5", Default), Is.EqualTo("10.0.0.5"));
        Assert.That(NetAddresses.WithPort("10.0.0.5", 2000), Is.EqualTo("10.0.0.5:2000"));
        Assert.That(NetAddresses.WithPort(null, 2000), Is.EqualTo("port 2000"));
    }

    [TestCase("169.254.10.1", true)]
    [TestCase("192.168.1.1", false)]
    public void Link_Local_Addresses_Are_Spotted(string address, bool linkLocal)
    {
        Assert.That(NetAddresses.IsLinkLocal(IPAddress.Parse(address)), Is.EqualTo(linkLocal));
    }

    [TestCase("10.1.2.3", true)]
    [TestCase("172.16.0.1", true)]
    [TestCase("172.32.0.1", false)]
    [TestCase("192.168.1.53", true)]
    [TestCase("100.101.102.103", true)]     // carrier-grade NAT / Tailscale
    [TestCase("127.0.0.1", true)]
    [TestCase("71.94.110.234", false)]
    [TestCase("fd00::1", true)]
    [TestCase("2001:db8::1", false)]
    public void Private_Addresses_Are_Spotted(string address, bool isPrivate)
    {
        Assert.That(NetAddresses.IsPrivate(IPAddress.Parse(address)), Is.EqualTo(isPrivate));
    }

    // Wi-Fi listed first, Ethernet carrying the default route: both have a gateway, same subnet
    private static readonly (IPAddress, bool)[] TwoAdapters =
    [
        (IPAddress.Parse("fe80::1"), true),
        (IPAddress.Parse("192.168.1.10"), true),
        (IPAddress.Parse("169.254.3.4"), false),
        (IPAddress.Parse("192.168.1.53"), true),
    ];

    [Test]
    public void The_Local_Address_Is_The_Default_Routes_Adapter()
    {
        Assert.That(NetAddresses.PickLocalAddress(TwoAdapters, IPAddress.Parse("192.168.1.53")), Is.EqualTo(IPAddress.Parse("192.168.1.53")));
    }

    [Test]
    public void Without_A_Route_The_Local_Address_Is_One_With_A_Gateway()
    {
        Assert.That(NetAddresses.PickLocalAddress(TwoAdapters, null), Is.EqualTo(IPAddress.Parse("192.168.1.10")));
        Assert.That(NetAddresses.PickLocalAddress(
            [(IPAddress.Parse("10.0.0.5"), false), (IPAddress.Parse("192.168.1.53"), true)], null), Is.EqualTo(IPAddress.Parse("192.168.1.53")));
    }

    [Test]
    public void A_Routed_Address_No_Interface_Offers_Is_Ignored()
    {
        // e.g. a VPN tunnel's address, or loopback: tunnels are left out of the candidates
        Assert.That(NetAddresses.PickLocalAddress(TwoAdapters, IPAddress.Parse("10.8.0.2")), Is.EqualTo(IPAddress.Parse("192.168.1.10")));
        Assert.That(NetAddresses.PickLocalAddress(
            [(IPAddress.Parse("127.0.0.1"), false)], IPAddress.Parse("127.0.0.1")), Is.Null);
    }

    [Test]
    public void Streamer_Mode_Hides_Internet_Addresses_But_Not_Lan_Ones()
    {
        // Act
        var text = NetAddresses.Hide("LAN 192.168.1.53, net 71.94.110.234:10700 (UPnP ok). Was 8.8.8.8.", hide: true);

        // Assert
        Assert.That(text, Is.EqualTo("LAN 192.168.1.53, net (hidden):10700 (UPnP ok). Was (hidden)."));
    }

    [Test]
    public void Streamer_Mode_Hides_A_Typed_Host_Name()
    {
        Assert.That(NetAddresses.Hide("Couldn't find the address \"frag.example.net\".", hide: true, typedHost: "frag.example.net"),
            Is.EqualTo("Couldn't find the address \"(hidden)\"."));
        Assert.That(NetAddresses.Hide("Connecting to 10.0.0.5", hide: true, typedHost: "10.0.0.5"), Is.EqualTo("Connecting to 10.0.0.5"));
    }

    [Test]
    public void Without_Streamer_Mode_Nothing_Is_Hidden()
    {
        Assert.That(NetAddresses.Hide("net 71.94.110.234", hide: false, typedHost: "71.94.110.234"), Is.EqualTo("net 71.94.110.234"));
    }

    [Test]
    public void An_Open_Port_Says_Where_To_Join()
    {
        // Act
        var text = NetAddresses.DescribePortCheck(new PortCheck.Result(PortCheck.Outcome.Open, IPAddress.Parse("81.2.3.4")), null, 2000, "10.0.0.5");

        // Assert
        Assert.That(text, Does.Contain("open").And.Contain("81.2.3.4:2000"));
    }

    [Test]
    public void A_Closed_Port_Says_Where_To_Forward_It()
    {
        // Act
        var text = NetAddresses.DescribePortCheck(new PortCheck.Result(PortCheck.Outcome.Closed), null, 2000, "10.0.0.5");

        // Assert
        Assert.That(text, Does.Contain("closed").And.Contain("Forward UDP 2000 to 10.0.0.5"));
    }

    [Test]
    public void A_Port_The_Router_Opened_That_Stays_Closed_Blames_The_Firewall()
    {
        // Act
        var text = NetAddresses.DescribePortCheck(new PortCheck.Result(PortCheck.Outcome.Closed),
            new PortMapper.Result(PortMapper.Outcome.Opened, 2000, "UPnP"), 2000, null);

        // Assert
        Assert.That(text, Does.Contain("Firewall").And.Not.Contain("Forward"));
    }

    [Test]
    public void A_Router_That_Didnt_Open_It_Says_Forward_By_Hand()
    {
        Assert.That(NetAddresses.DescribePortMapping(new PortMapper.Result(PortMapper.Outcome.NoRouter, 2000), null),
            Does.Contain("forward UDP 2000 to this computer by hand"));
    }
}

public class ContentHashTests
{
    private string _folder = "";

    [SetUp]
    public void MakeFolder() => _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pfwolf-hash-" + Guid.NewGuid().ToString("N"))).FullName;

    [TearDown]
    public void RemoveFolder() => Directory.Delete(_folder, recursive: true);

    private string Zip(string name, params (string Path, string Text)[] files)
    {
        var path = Path.Combine(_folder, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entryPath, text) in files)
        {
            using var writer = new StreamWriter(zip.CreateEntry(entryPath).Open());
            writer.Write(text);
        }
        return path;
    }

    [Test]
    public void A_Zip_Is_Hashed_By_What_Is_In_It_Not_Its_Order_Or_Times()
    {
        // Arrange: the same files, zipped in another order (and so at other offsets and times)
        var first = Zip("a.pk3", ("maps/one.txt", "1"), ("two.txt", "2"));
        var second = Zip("b.pk3", ("two.txt", "2"), ("maps/one.txt", "1"));
        File.SetLastWriteTimeUtc(second, DateTime.UtcNow.AddDays(-3));

        // Act / Assert
        Assert.That(ContentHash.Of(second, []), Is.EqualTo(ContentHash.Of(first, [])));
    }

    [Test]
    public void Different_Contents_Hash_Differently()
    {
        // Arrange
        var first = Zip("a.pk3", ("one.txt", "1"));
        var second = Zip("b.pk3", ("one.txt", "2"));

        // Act / Assert
        Assert.That(ContentHash.Of(second, []), Is.Not.EqualTo(ContentHash.Of(first, [])));
    }

    [Test]
    public void Mods_Count_In_Their_Order()
    {
        // Arrange
        var basePk3 = Zip("pfwolf.pk3", ("base.txt", "b"));
        var modA = Zip("a.pk3", ("a.txt", "a"));
        var modB = Zip("b.pk3", ("b.txt", "b"));

        // Act / Assert
        Assert.That(ContentHash.Of(basePk3, [modA, modB]), Is.Not.EqualTo(ContentHash.Of(basePk3, [modB, modA])));
        Assert.That(ContentHash.Of(basePk3, [modA]), Is.Not.EqualTo(ContentHash.Of(basePk3, [])));
    }

    [Test]
    public void A_Folder_Mod_Is_Hashed_By_Its_Files()
    {
        // Arrange
        var basePk3 = Zip("pfwolf.pk3", ("base.txt", "b"));
        var mod = Directory.CreateDirectory(Path.Combine(_folder, "mod")).FullName;
        File.WriteAllText(Path.Combine(mod, "a.txt"), "a");
        var before = ContentHash.Of(basePk3, [mod]);

        // Act
        File.WriteAllText(Path.Combine(mod, "a.txt"), "changed");

        // Assert
        Assert.That(ContentHash.Of(basePk3, [mod]), Is.Not.EqualTo(before));
    }

    [Test]
    public void A_Missing_File_Still_Hashes()
    {
        Assert.That(ContentHash.Of(Path.Combine(_folder, "missing.pk3"), []), Has.Length.EqualTo(32));
    }
}

public class NetStartRequestTests
{
    [Test]
    public void Nothing_Asked_Is_Nothing_Pending()
    {
        // Arrange
        var request = new NetStartRequest();
        request.SetParams(new GameParams());

        // Act / Assert
        Assert.That(request.HasPending, Is.False);
        Assert.That(request.HostPort, Is.EqualTo(NetProtocol.DefaultPort));
    }

    [Test]
    public void Host_Is_Taken_Once()
    {
        // Arrange
        var request = new NetStartRequest();
        request.SetParams(new GameParams { Host = true, Port = 2000 });

        // Act / Assert
        Assert.That(request.HasPending, Is.True);
        Assert.That(request.Take(), Is.EqualTo((true, (string?)null)));
        Assert.That(request.HasPending, Is.False);
        Assert.That(request.HostPort, Is.EqualTo(2000));
    }

    [Test]
    public void Join_Is_Trimmed()
    {
        // Arrange
        var request = new NetStartRequest();
        request.SetParams(new GameParams { Join = " 10.0.0.5:2000 " });

        // Act / Assert
        Assert.That(request.Take(), Is.EqualTo((false, (string?)"10.0.0.5:2000")));
    }

    [TestCase(0)]
    [TestCase(70000)]
    public void A_Port_Out_Of_Range_Is_The_Default(int port)
    {
        // Arrange
        var request = new NetStartRequest();
        request.SetParams(new GameParams { Port = port });

        // Act / Assert
        Assert.That(request.HostPort, Is.EqualTo(NetProtocol.DefaultPort));
    }
}
