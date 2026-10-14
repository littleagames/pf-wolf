namespace PFWolf.Tests;

public class SaveFileTests
{
    private string _folder = "";

    [SetUp]
    public void SetUp() => _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pfwolf-savefile-" + Guid.NewGuid().ToString("N"))).FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    private static SaveInfo Info(string name, DateTime savedAt) => new()
    {
        Name = name,
        SavedAt = savedAt,
        GamePack = "wolf3d",
        MapOn = "MAP01",
        MapName = "Floor 1",
        Mods = [new SavedMod("Moody Lights", "1.0", "moody-lights")],
    };

    private static readonly DateTime When = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Body = [1, 2, 3, 4, 5, 250, 251];

    [Test]
    public void A_Save_Reads_Back_Its_Header_Body_And_Picture()
    {
        // Arrange
        var path = Path.Combine(_folder, "save_1.dat");
        var thumbnail = new SaveThumbnail(3, 2, [10, 11, 12, 13, 14, 15]);

        // Act
        SaveFile.Write(path, Info("First", When), thumbnail, Body);
        var (body, checksumOk) = SaveFile.ReadBody(path, out var info);

        // Assert
        Assert.That(body, Is.EqualTo(Body));
        Assert.That(checksumOk, Is.True);
        Assert.That(info!.Name, Is.EqualTo("First"));
        Assert.That(info.Mods, Is.EqualTo(Info("", When).Mods));
        var read = SaveFile.ReadThumbnail(SaveFile.ReadInfo(path)!);
        Assert.That((read!.Width, read.Height, read.Pixels), Is.EqualTo((3, 2, thumbnail.Pixels)));
    }

    [Test]
    public void A_Save_Without_A_Picture_Has_None()
    {
        // Arrange
        var path = Path.Combine(_folder, "save_1.dat");

        // Act
        SaveFile.Write(path, Info("First", When), null, Body);

        // Assert
        Assert.That(SaveFile.ReadThumbnail(SaveFile.ReadInfo(path)!), Is.Null);
        Assert.That(SaveFile.ReadBody(path, out _).Body, Is.EqualTo(Body));
    }

    [Test]
    public void A_Changed_Body_Fails_Its_Checksum()
    {
        // Arrange: the body's last byte, just before the checksum
        var path = Path.Combine(_folder, "save_1.dat");
        SaveFile.Write(path, Info("First", When), null, Body);
        var bytes = File.ReadAllBytes(path);
        bytes[^(sizeof(int) + 1)] ^= 0xFF;
        File.WriteAllBytes(path, bytes);

        // Act
        var (_, checksumOk) = SaveFile.ReadBody(path, out _);

        // Assert
        Assert.That(checksumOk, Is.False);
    }

    [Test]
    public void A_Truncated_Save_Throws_But_Still_Gives_Its_Header()
    {
        // Arrange: cut off in the middle of the thumbnail
        var path = Path.Combine(_folder, "save_1.dat");
        SaveFile.Write(path, Info("First", When), new SaveThumbnail(10, 10, new byte[100]), Body);
        var bytes = File.ReadAllBytes(path);
        File.WriteAllBytes(path, bytes[..(bytes.Length - Body.Length - 60)]);

        // Act
        SaveInfo? info = null;
        Assert.Throws<EndOfStreamException>(() => SaveFile.ReadBody(path, out info));

        // Assert: so the load can say whether other mods are to blame
        Assert.That(info?.Name, Is.EqualTo("First"));
    }

    [Test]
    public void A_Failed_Write_Leaves_No_Temporary_File()
    {
        // Arrange: a folder where the save would go can't be replaced by it
        var path = Path.Combine(_folder, "save_1.dat");
        Directory.CreateDirectory(path);

        // Act / Assert
        var failure = Assert.Catch(() => SaveFile.Write(path, Info("First", When), null, Body));
        Assert.That(failure, Is.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>());
        Assert.That(File.Exists(path + ".tmp"), Is.False);
    }

    [Test]
    public void Saving_Over_A_Save_Replaces_It()
    {
        // Arrange
        var path = Path.Combine(_folder, "save_1.dat");
        SaveFile.Write(path, Info("First", When), null, Body);

        // Act
        SaveFile.Write(path, Info("Second", When), null, [9, 9]);

        // Assert
        Assert.That(SaveFile.ReadInfo(path)!.Name, Is.EqualTo("Second"));
        Assert.That(Directory.GetFiles(_folder), Has.Length.EqualTo(1));
    }

    [Test]
    public void List_Gives_Readable_Saves_Newest_First()
    {
        // Arrange
        SaveFile.Write(Path.Combine(_folder, "a.dat"), Info("Older", When), null, Body);
        SaveFile.Write(Path.Combine(_folder, "b.dat"), Info("Newer", When.AddHours(1)), null, Body);
        File.WriteAllText(Path.Combine(_folder, "junk.dat"), "not a save");
        File.WriteAllText(Path.Combine(_folder, "notes.txt"), "not a .dat");

        // Act
        var saves = SaveFile.List(_folder);

        // Assert
        Assert.That(saves.Select(s => s.Name), Is.EqualTo(new[] { "Newer", "Older" }));
        Assert.That(saves[0].Path, Is.EqualTo(Path.Combine(_folder, "b.dat")));
    }

    [Test]
    public void List_Of_A_Missing_Folder_Is_Empty()
    {
        Assert.That(SaveFile.List(Path.Combine(_folder, "missing")), Is.Empty);
    }

    [Test]
    public void NewPath_Is_Named_For_When_And_Never_Taken()
    {
        // Arrange
        var now = new DateTime(2026, 10, 14, 9, 5, 3);
        var first = SaveFile.NewPath(_folder, now);
        File.WriteAllText(first, "");

        // Act
        var second = SaveFile.NewPath(_folder, now);

        // Assert
        Assert.That(Path.GetFileName(first), Is.EqualTo("save_20261014_090503.dat"));
        Assert.That(Path.GetFileName(second), Is.EqualTo("save_20261014_090503_2.dat"));
    }
}
