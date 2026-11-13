using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>
/// Where the game's own files are in a macOS app (docs/dev/mac-linux-plan.md, phase 4):
/// Contents/Resources beside the program's Contents/MacOS. Tried with folders laid out as an app
/// is, so it runs on every system.
/// </summary>
public class GameInstallTests
{
    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workFolder);
        UserDataFolder.Override = Path.Combine(_workFolder, "user");
    }

    [TearDown]
    public void TearDown()
    {
        UserDataFolder.Override = null;
        try
        {
            Directory.Delete(_workFolder, recursive: true);
        }
        catch (IOException)
        {
            // Only a temp folder
        }
    }

    private string MakeApp(string name)
    {
        var app = Path.Combine(_workFolder, name);
        Directory.CreateDirectory(Path.Combine(app, "Contents", "MacOS"));
        Directory.CreateDirectory(Path.Combine(app, "Contents", "Resources"));
        return app;
    }

    [Test]
    public void An_Apps_Program_Folder_Has_Its_Files_In_Resources()
    {
        var app = MakeApp("PFWolf.app");

        Assert.That(GameInstall.AppBundleResources(Path.Combine(app, "Contents", "MacOS")),
            Is.EqualTo(Path.Combine(app, "Contents", "Resources")));
        Assert.That(GameInstall.AppBundleResources(Path.Combine(app, "Contents", "MacOS") + Path.DirectorySeparatorChar),
            Is.EqualTo(Path.Combine(app, "Contents", "Resources")), "with a trailing separator, as AppContext.BaseDirectory has");
        Assert.That(GameInstall.AppBundleResources(_workFolder), Is.Null, "an ordinary folder");
        Assert.That(GameInstall.AppBundleResources(Path.Combine(_workFolder, "Contents", "MacOS")), Is.Null, "not in an .app");
    }

    [Test]
    public void A_Picked_App_Means_Its_Resources()
    {
        var app = MakeApp("PFWolf.app");

        Assert.That(GameInstall.ContentFolder(app), Is.EqualTo(Path.Combine(app, "Contents", "Resources")));
        Assert.That(GameInstall.ContentFolder(_workFolder), Is.EqualTo(_workFolder));
    }

    [Test]
    public void Folders_Inside_An_App_Are_Known()
    {
        var app = MakeApp("PFWolf.app");

        Assert.That(GameInstall.IsInsideApp(Path.Combine(app, "Contents", "Resources")), Is.True);
        Assert.That(GameInstall.IsInsideApp(_workFolder), Is.False);
        Assert.That(ModSource.WritableModsFolderOf(Path.Combine(app, "Contents", "Resources")), Is.EqualTo(UserDataFolder.ModsFolder),
            "new mods never go inside an app");
    }

    [Test]
    public void The_Games_Program_Is_Found_For_A_Game_Folder()
    {
        var app = MakeApp("PFWolf.app");
        File.WriteAllBytes(Path.Combine(app, "Contents", "MacOS", "PFWolf"), []);

        if (OperatingSystem.IsWindows())
        {
            Assert.That(GameInstall.ExeIn(_workFolder), Is.EqualTo(Path.Combine(_workFolder, "PFWolf.exe")));
            return;
        }
        Assert.That(GameInstall.ExeIn(_workFolder), Is.EqualTo(Path.Combine(_workFolder, "PFWolf")));
        Assert.That(GameInstall.ExeIn(app), Is.EqualTo(Path.Combine(app, "Contents", "MacOS", "PFWolf")));
        Assert.That(GameInstall.ExeIn(Path.Combine(app, "Contents", "Resources")), Is.EqualTo(Path.Combine(app, "Contents", "MacOS", "PFWolf")));
    }

    [Test]
    public void Settings_Live_In_The_Systems_Own_Folder()
    {
        var expected = OperatingSystem.IsMacOS()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Assert.That(UserSettingsFolder.Path, Is.EqualTo(Path.Combine(expected, "PFWolf")));
    }
}
