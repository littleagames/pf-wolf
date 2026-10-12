using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>
/// A data file with more chunks than raw-data-map names (one WDC or ChaosEdit added walls, pics
/// or sounds to) loads with made-up names for the rest and a warning, instead of crashing. The
/// game's own WL6 files stand in for such a file, given only a few names.
/// </summary>
[Category("GameData")]
public class DataMapMismatchTests
{
    private static string Wl6(string file)
    {
        var candidates = new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA") }
            .Concat(new[] { "Debug", "Release" }.Select(c => Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64", c, "net10.0")))
            .Where(folder => !string.IsNullOrWhiteSpace(folder));
        var path = candidates.Select(folder => Path.Combine(folder!, file)).FirstOrDefault(File.Exists);
        if (path == null)
            Assert.Ignore($"{file} isn't in the built game folder; set PFWOLF_GAMEDATA to a folder that has it");
        return path!;
    }

    [Test]
    public void Vswap_Names_The_Walls_Sprites_And_Sounds_Past_The_Map()
    {
        // Arrange
        var loader = new Wolf3dVswapFileLoader(Wl6("VSWAP.WL6"));
        var warnings = new List<string>();

        // Act
        var assets = loader.GetAssets(["W0", "W1"], ["S0"], ["D0"], warnings.Add);

        // Assert
        Assert.That(assets.Keys, Does.Contain("w0").And.Contain("wall0002").And.Contain("s0").And.Contain("spr0001")
            .And.Contain("d0").And.Contain("digi0001"));
        Assert.That(warnings, Has.Count.EqualTo(3));
    }

    [Test]
    public void VgaGraph_Names_The_Pics_And_Externs_Past_The_Map()
    {
        // Arrange: three pics and one extern named, around the map's Tile8
        var loader = new Wolf3dVgaFileLoader(Wl6("VGAHEAD.WL6"), Wl6("VGAGRAPH.WL6"), Wl6("VGADICT.WL6"), numFonts: 2);
        var warnings = new List<string>();

        // Act
        var assets = loader.GetAssets(["STRUCTPIC", "SmallFont", "LargeFont", "P0", "P1", "P2", "Tile8", "E0"], warnings.Add);

        // Assert
        Assert.That(assets.Keys, Does.Contain("p2").And.Contain("pic0003").And.Contain("tile8")
            .And.Contain("e0").And.Contain("extern0001"));
        Assert.That(warnings, Has.Count.EqualTo(2));
    }

    [Test]
    public void AudioT_Names_The_Sounds_By_Kind_And_The_Songs_Past_The_Map()
    {
        // Arrange
        var loader = new Wolf3dAudioFileLoader(Wl6("AUDIOT.WL6"), Wl6("AUDIOHED.WL6"));
        var warnings = new List<string>();

        // Act
        var assets = loader.GetAssets(["PCA", "PCB", "ALA", "ALB"], ["M0"], warnings.Add);

        // Assert: the made-up names keep the kind, so each is still loaded as a PC / AdLib sound
        Assert.That(assets.Keys, Does.Contain("pca").And.Contain("pcsnd0002").And.Contain("ala").And.Contain("alsnd0002")
            .And.Contain("m0").And.Contain("music0001"));
        Assert.That(assets["alsnd0002"], Is.InstanceOf<PFWolf.Assets.Sounds.AdLibSound>());
        Assert.That(warnings, Has.Count.EqualTo(3));
    }
}
