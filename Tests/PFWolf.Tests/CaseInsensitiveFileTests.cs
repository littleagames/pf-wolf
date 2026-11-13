using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>
/// Data files found whatever the case of their names (docs/dev/mac-linux-plan.md):
/// gamepack-info names vswap.wl6, a Steam or GOG copy is VSWAP.WL6, and Linux's file system
/// tells them apart
/// </summary>
public class CaseInsensitiveFileTests
{
    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-case-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workFolder);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_workFolder, recursive: true);
        }
        catch (IOException)
        {
            // Only a temp folder
        }
    }

    [Test]
    public void A_Name_Matches_A_File_In_Another_Case()
    {
        Assert.That(CaseInsensitiveFile.Match("vswap.wl6", ["AUDIOT.WL6", "VSWAP.WL6"]), Is.EqualTo("VSWAP.WL6"));
        Assert.That(CaseInsensitiveFile.Match("vswap.wl6", ["vswap.wl6x", "AUDIOT.WL6"]), Is.Null);
        Assert.That(CaseInsensitiveFile.Match("vswap.wl6", ["vswap.wl6", "VSWAP.WL6"]), Is.EqualTo("VSWAP.WL6"), "ordinal order when there are both");
    }

    [Test]
    public void A_Portable_File_Name_Leaves_Out_What_Windows_Refuses()
    {
        Assert.That(PortableFileName.Clean("doors/open: \"big\"?"), Is.EqualTo("doors_open_ _big__"));
        Assert.That(PortableFileName.Clean("E1M1 Start-2"), Is.EqualTo("E1M1 Start-2"));
    }

    [Test]
    public void A_Missing_File_Comes_Back_As_Given()
    {
        var path = Path.Combine(_workFolder, "vswap.wl6");

        Assert.That(CaseInsensitiveFile.Find(path), Is.EqualTo(path));
        Assert.That(CaseInsensitiveFile.Find(Path.Combine(_workFolder, "no-folder", "vswap.wl6")), Is.EqualTo(Path.Combine(_workFolder, "no-folder", "vswap.wl6")));
    }

    [Test]
    public void A_Lower_Case_Data_File_Name_Finds_An_Upper_Case_File()
    {
        // Arrange: gamepack-info's lower-case name, the file in upper case
        var mod = Directory.CreateDirectory(Path.Combine(_workFolder, "mod")).FullName;
        File.WriteAllText(Path.Combine(mod, "VSWAP.WL6"), "x");
        var info = new GamePackInfoAsset(new()
        {
            ["wolf3d"] = new GamePack { FilePack = new FilePack() },
            ["my-mod"] = new GamePack { BasePack = "wolf3d", FilePack = new FilePack(), ModFolder = mod },
        });

        // Act
        var found = info.ResolveDataFile("my-mod", "vswap.wl6");

        // Assert: the file itself (on Windows under the name asked for, elsewhere under its own)
        Assert.That(File.Exists(found), Is.True, found);
        Assert.That(Path.GetDirectoryName(found), Is.EqualTo(mod));
        Assert.That(Path.GetFileName(found), Is.EqualTo("VSWAP.WL6").IgnoreCase);
    }
}
