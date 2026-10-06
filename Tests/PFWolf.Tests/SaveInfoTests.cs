namespace PFWolf.Tests;

public class SaveInfoTests
{
    // Program.SaveVersion: a save's header can only be read at the current format
    private const int CurrentVersion = 20;

    private string _folder = "";

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "pfwolf-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    private static SaveInfo SampleInfo() => new()
    {
        Name = "Before the boss",
        SavedAt = new DateTime(2026, 10, 6, 12, 30, 0, DateTimeKind.Utc),
        GamePack = "wolf3d",
        MapOn = "e1m9",
        MapName = "Episode 1 Boss",
        Difficulty = 3,
        SkillName = "I am Death incarnate!",
        LevelTime = 70 * 90,
        PlayTime = 70 * 3600,
        Score = 123456,
        Lives = 3,
        Health = 42,
        Kills = 10,
        KillTotal = 20,
        Secrets = 1,
        SecretTotal = 2,
        Treasure = 3,
        TreasureTotal = 4,
        Mods = [new SavedMod("Switch Demo", "1.0", "switch-demo")],
    };

    private string WriteSave(string fileName, Action<BinaryWriter> write)
    {
        var path = Path.Combine(_folder, fileName);
        using (var bw = new BinaryWriter(File.Create(path)))
            write(bw);
        return path;
    }

    private static void WriteHeader(BinaryWriter bw, SaveInfo info, string signature = "PFWS", int version = CurrentVersion)
    {
        using var header = new MemoryStream();
        using (var hw = new BinaryWriter(header, System.Text.Encoding.UTF8, leaveOpen: true))
            info.Write(hw);

        bw.Write(System.Text.Encoding.ASCII.GetBytes(signature));
        bw.Write(version);
        bw.Write((int)header.Length);
        bw.Write(header.ToArray());
    }

    [Test]
    public void ReadSaveInfo_Reads_The_Header_Back()
    {
        // Arrange
        var info = SampleInfo();
        var path = WriteSave("save_1.dat", bw => WriteHeader(bw, info));

        // Act
        var read = Program.ReadSaveInfo(path);

        // Assert
        Assert.That(read, Is.Not.Null);
        Assert.That(read!.Mods, Is.EqualTo(info.Mods));
        Assert.That(read with { Mods = info.Mods }, Is.EqualTo(info with { Path = path }));
        Assert.That(read.SavedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    [Test]
    public void ReadSaveInfo_Skips_Another_Signature()
    {
        // Arrange
        var path = WriteSave("other.dat", bw => WriteHeader(bw, SampleInfo(), signature: "WOLF"));

        // Act / Assert
        Assert.That(Program.ReadSaveInfo(path), Is.Null);
    }

    [TestCase(CurrentVersion - 1)]
    [TestCase(CurrentVersion + 1)]
    public void ReadSaveInfo_Skips_Another_Format_Version(int version)
    {
        // Arrange
        var path = WriteSave("old.dat", bw => WriteHeader(bw, SampleInfo(), version: version));

        // Act / Assert
        Assert.That(Program.ReadSaveInfo(path), Is.Null);
    }

    [Test]
    public void ReadSaveInfo_Skips_A_Header_Length_Past_The_End()
    {
        // Arrange
        var path = WriteSave("cut.dat", bw =>
        {
            bw.Write("PFWS"u8.ToArray());
            bw.Write(CurrentVersion);
            bw.Write(5000);
            bw.Write(new byte[10]);
        });

        // Act / Assert
        Assert.That(Program.ReadSaveInfo(path), Is.Null);
    }

    [Test]
    public void ReadSaveInfo_Skips_A_Truncated_Header()
    {
        // Arrange: the length is right but the header inside stops short
        var path = WriteSave("short.dat", bw =>
        {
            bw.Write("PFWS"u8.ToArray());
            bw.Write(CurrentVersion);
            bw.Write(3);
            bw.Write(new byte[] { 2, (byte)'h', (byte)'i' });
        });

        // Act / Assert
        Assert.That(Program.ReadSaveInfo(path), Is.Null);
    }

    [Test]
    public void ReadSaveInfo_Skips_An_Empty_File()
    {
        // Arrange
        var path = WriteSave("empty.dat", _ => { });

        // Act / Assert
        Assert.That(Program.ReadSaveInfo(path), Is.Null);
    }

    [Test]
    public void SaveInfo_Read_Rejects_A_Bad_Mod_Count()
    {
        // Arrange: a header whose mod count is negative
        using var stream = new MemoryStream();
        using (var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            (SampleInfo() with { Mods = [] }).Write(bw);
            bw.Seek(-sizeof(int), SeekOrigin.End);
            bw.Write(-1);
        }
        stream.Position = 0;

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => SaveInfo.Read(new BinaryReader(stream)));
    }

    [TestCase("quicksave.dat", SaveKind.Quick)]
    [TestCase("AUTOSAVE.DAT", SaveKind.Auto)]
    [TestCase("save_20261006_123000.dat", SaveKind.Normal)]
    public void Kind_Comes_From_The_File_Name(string fileName, object kind)
    {
        // Act
        var info = new SaveInfo { Path = Path.Combine("saves", fileName) };

        // Assert
        Assert.That(info.Kind, Is.EqualTo((SaveKind)kind));
    }
}
