using PFWolf.Assets.Sounds;
using PFWolf.Editor.Data;
using Command = PFWolf.Assets.Sounds.Wolf3dImfAudio.WolfensteinMusicCommand;

namespace PFWolf.Tests;

/// <summary>The sound browser's Add to mod: what a file is as a song or sound, and where it goes in a mod</summary>
public class SoundFilesTests
{
    private string _mod = "";

    [SetUp]
    public void SetUp()
    {
        _mod = Path.Combine(Path.GetTempPath(), "pfwolf-soundfiles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_mod);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_mod))
            Directory.Delete(_mod, recursive: true);
    }

    private static byte[] Imf() => Wolf3dImfAudio.ToFile([new Command(0x00, 0x00, 0), new Command(0xB1, 0x36, 70)], "test");
    private static byte[] AdLib() => AdLibSound.Create(10, default, 4, [0x80, 0x90]).RawData;
    private static byte[] Pc() => PcSound.Create(10, [40, 50]).RawData;

    [Test]
    public void Files_Are_What_Their_Extensions_Say()
    {
        Assert.That(SoundFiles.Check(true, "song.imf", Imf()), Is.EqualTo(SoundKind.Music));
        Assert.That(SoundFiles.Check(true, "song.WLF", Imf()), Is.EqualTo(SoundKind.Music));
        Assert.That(SoundFiles.Check(false, "boom.adl", AdLib()), Is.EqualTo(SoundKind.AdLib));
        Assert.That(SoundFiles.Check(false, "boom.pcs", Pc()), Is.EqualTo(SoundKind.PcSpeaker));
    }

    [Test]
    public void A_File_That_Cant_Be_One_Says_Why()
    {
        Assert.Throws<InvalidDataException>(() => SoundFiles.Check(true, "boom.adl", AdLib()), "a song isn't .adl");
        Assert.Throws<InvalidDataException>(() => SoundFiles.Check(false, "song.imf", Imf()), "a sound isn't .imf");
        Assert.Throws<InvalidDataException>(() => SoundFiles.Check(false, "boom.adl", [1, 2, 3]));
        Assert.Throws<InvalidDataException>(() => SoundFiles.Check(true, "song.ogg", [1, 2, 3, 4, 5, 6, 7, 8]));
    }

    [Test]
    public void A_Song_Takes_The_Place_Of_The_Mods_Other_Files_Of_Its_Name()
    {
        Directory.CreateDirectory(Path.Combine(_mod, "music"));
        Directory.CreateDirectory(Path.Combine(_mod, "sounds"));
        File.WriteAllBytes(Path.Combine(_mod, "music", "CORNER.ogg"), [1]);
        File.WriteAllBytes(Path.Combine(_mod, "music", "OTHER.ogg"), [1]);
        File.WriteAllBytes(Path.Combine(_mod, "sounds", "CORNER.wav"), [1]);

        var (entryPath, deletes) = SoundFiles.Target(_mod, SoundKind.Music, "corner", ".IMF");
        Assert.That(entryPath, Is.EqualTo("music/CORNER.imf"));
        Assert.That(deletes, Is.EqualTo(new[] { "music/CORNER.ogg" }));
    }

    [Test]
    public void An_AdLib_Sound_Keeps_The_Digitized_One_Of_Its_Name()
    {
        Directory.CreateDirectory(Path.Combine(_mod, "sounds"));
        File.WriteAllBytes(Path.Combine(_mod, "sounds", "BOOM.wav"), [1]);
        File.WriteAllBytes(Path.Combine(_mod, "sounds", "BOOM.adl"), [1]);

        Assert.That(SoundFiles.Target(_mod, SoundKind.AdLib, "BOOM", ".adl").Deletes, Is.Empty);
        var (entryPath, deletes) = SoundFiles.Target(_mod, SoundKind.Digitized, "BOOM", ".ogg");
        Assert.That(entryPath, Is.EqualTo("sounds/BOOM.ogg"));
        Assert.That(deletes, Is.EqualTo(new[] { "sounds/BOOM.wav" }));
    }
}
