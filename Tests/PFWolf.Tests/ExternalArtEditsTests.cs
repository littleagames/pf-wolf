using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;

namespace PFWolf.Tests;

/// <summary>Pictures opened in another program: written, watched, a save reported once, stopped</summary>
public class ExternalArtEditsTests
{
    private string _folder = "";

    [SetUp]
    public void MakeFolder() => _folder = Path.Combine(Path.GetTempPath(), "pfwolf-external-tests-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void RemoveFolder()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Test]
    public void A_Save_In_The_Other_Program_Is_Reported_Once_Things_Go_Quiet()
    {
        // Arrange
        using var edits = new ExternalArtEdits(_folder, action => action());
        var saved = new List<ExternalArt>();
        edits.Saved += (_, art) => { lock (saved) saved.Add(art); };
        var art = edits.Start(ArtKind.Sprite, "garda1", () => [1, 2, 3]);

        // Act: saved three times in a row, as some programs do
        Thread.Sleep(50);
        for (int i = 0; i < 3; i++)
        {
            File.WriteAllBytes(art.FilePath, [4, 5, (byte)i]);
            File.SetLastWriteTimeUtc(art.FilePath, DateTime.UtcNow.AddSeconds(i + 1));
            Thread.Sleep(50);
        }
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && saved.Count == 0)
            Thread.Sleep(50);
        Thread.Sleep(ExternalArtEdits.QuietTime * 2);

        // Assert
        Assert.That(art.FilePath, Is.EqualTo(Path.Combine(_folder, "sprite-GARDA1.png")));
        Assert.That(saved, Is.EqualTo(new[] { art }));
        Assert.That(ExternalArtEdits.Read(art), Is.EqualTo(new byte[] { 4, 5, 2 }));
    }

    [Test]
    public void Opening_It_Again_Keeps_What_Was_Drawn_And_Stopping_Removes_The_File()
    {
        // Arrange
        using var edits = new ExternalArtEdits(_folder, action => action());
        var art = edits.Start(ArtKind.Texture, "WALL", () => [1]);
        File.WriteAllBytes(art.FilePath, [2]);

        // Act
        var again = edits.Start(ArtKind.Texture, "WALL", () => [3]);
        var drawn = File.ReadAllBytes(art.FilePath);
        edits.Stop(again);

        // Assert
        Assert.That(again, Is.EqualTo(art));
        Assert.That(drawn, Is.EqualTo(new byte[] { 2 }));
        Assert.That(File.Exists(art.FilePath), Is.False);
        Assert.That(edits.Watched, Is.Empty);
    }
}
