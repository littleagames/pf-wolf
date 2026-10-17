using PFWolf.Assets.Sounds;
using PFWolf.Editor.Data;

namespace PFWolf.Tests;

/// <summary>The sound browser's catalog and how it plays sounds</summary>
public class SoundCatalogTests
{
    private static Dictionary<string, (SoundProfile Profile, string From)> Sequences(params (string Name, SoundProfile Profile)[] entries)
        => entries.ToDictionary(entry => entry.Name, entry => (entry.Profile, "sounds/sound-seq.yaml"), StringComparer.OrdinalIgnoreCase);

    [Test]
    public void Resolve_Follows_Aliases_To_What_Plays()
    {
        // Arrange
        var sequences = Sequences(
            ("switches/exitbutn", new SoundProfile { Alias = "switches/normbutn" }),
            ("switches/normbutn", new SoundProfile { Digitized = "DSSWITCH", AdLib = "ALSWITCH" }));

        // Act
        var profile = SoundCatalog.Resolve(sequences, "switches/exitbutn", pickRandom: null);

        // Assert
        Assert.That(profile?.Digitized, Is.EqualTo("DSSWITCH"));
    }

    [Test]
    public void Resolve_Picks_A_Random_Sound_Only_When_Asked_To()
    {
        // Arrange
        var sequences = Sequences(
            ("guard/death", new SoundProfile { Random = ["guard/death1", "guard/death2"] }),
            ("guard/death1", new SoundProfile { Digitized = "DSDEATH1" }),
            ("guard/death2", new SoundProfile { Digitized = "DSDEATH2" }));

        // Act / Assert
        Assert.That(SoundCatalog.Resolve(sequences, "guard/death", pickRandom: null), Is.Null);
        Assert.That(SoundCatalog.Resolve(sequences, "guard/death", count => count - 1)?.Digitized, Is.EqualTo("DSDEATH2"));
    }

    [Test]
    public void Resolve_Gives_Up_On_An_Alias_Loop()
    {
        // Arrange
        var sequences = Sequences(
            ("a", new SoundProfile { Alias = "b" }),
            ("b", new SoundProfile { Alias = "a" }));

        // Act / Assert
        Assert.That(SoundCatalog.Resolve(sequences, "a", pickRandom: null), Is.Null);
    }

    [Test]
    public void A_Sound_Plays_From_Where_It_Starts_On_Both_Sides()
    {
        // Arrange
        using var source = new SampleMusicSource([10, 20, 30, 40], 8000, start: 2);
        var stereo = new short[4 * 2];

        // Act
        var frames = source.Read(stereo, loop: false);

        // Assert
        Assert.That(frames, Is.EqualTo(2));
        Assert.That(stereo.Take(4), Is.EqualTo(new short[] { 30, 30, 40, 40 }));
    }

    [Test]
    public void A_Looping_Sound_Comes_Round_Again()
    {
        // Arrange
        using var source = new SampleMusicSource([1, 2, 3], 8000);
        var stereo = new short[5 * 2];

        // Act
        var frames = source.Read(stereo, loop: true);

        // Assert
        Assert.That(frames, Is.EqualTo(5));
        Assert.That(Enumerable.Range(0, 5).Select(frame => stereo[frame * 2]), Is.EqualTo(new short[] { 1, 2, 3, 1, 2 }));
    }

    [Test]
    public void An_Exported_Wav_Reads_Back_The_Same()
    {
        // Arrange
        short[] samples = [0, 1000, -1000, short.MaxValue, short.MinValue];

        // Act
        var wav = SoundCatalog.ToWav(samples, 22050);
        var (decoded, sampleRate) = AudioFileDecoder.DecodeMono16(wav);

        // Assert
        Assert.That(sampleRate, Is.EqualTo(22050));
        Assert.That(decoded, Is.EqualTo(samples).Within(1));
    }
}

/// <summary>The sound browser's catalog over the real games. Needs a built game folder (pfwolf.pk3 and the data files).</summary>
[Category("GameData")]
public class SoundCatalogGameTests
{
    private static GameContent Load(string game)
    {
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        var folder = new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");

        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            return GameContent.Load(folder!, game, []);
        }
        catch (PFWolf.Exceptions.DataFilesException)
        {
            Assert.Ignore($"{game}'s data files aren't in {folder}");
            throw;
        }
        finally
        {
            Directory.SetCurrentDirectory(workingFolder);
        }
    }

    [TestCase("wolf3d")]
    [TestCase("spear")]
    public void Every_Sound_And_Song_Is_Listed_And_Plays(string game)
    {
        // Arrange
        var content = Load(game);

        // Act
        var entries = SoundCatalog.Build(content);

        // Assert: the door sound, made of the game's three sounds, each used by it
        var door = entries.Single(entry => entry.Kind == SoundKind.Sound && entry.Name == "doors/open");
        var variants = SoundCatalog.Variants(content, door.Name);
        Assert.That(variants.Select(variant => variant.Kind), Is.EqualTo(new[] { SoundKind.Digitized, SoundKind.AdLib, SoundKind.PcSpeaker }));
        foreach (var variant in variants)
        {
            var asset = entries.Single(entry => entry.Kind == variant.Kind && entry.Name == variant.AssetName);
            Assert.That(asset.Uses.Select(use => use.SoundName), Does.Contain("doors/open"), variant.Label);
            Assert.That(SoundCatalog.Samples(content, variant.Kind, variant.AssetName)?.Samples, Is.Not.Empty, variant.Label);
        }

        // ... and every level's song is there, and makes a sound
        var songs = entries.Where(entry => entry.Kind == SoundKind.Music).ToDictionary(entry => entry.Name);
        Assert.That(songs, Is.Not.Empty);
        foreach (var music in content.GameInfo!.Maps.Values.Select(map => map.Music).Where(music => !string.IsNullOrEmpty(music)).Distinct())
        {
            Assert.That(songs, Does.ContainKey(music.ToUpperInvariant()), music);
            Assert.That(songs[music.ToUpperInvariant()].Uses, Is.Not.Empty, music);
        }

        var first = songs.Values.First(song => song.Uses.Count > 0);
        Assert.That(SoundCatalog.MusicLength(content, first.Name), Is.GreaterThan(TimeSpan.FromSeconds(5)), first.Name);
        using var source = SoundCatalog.Open(content, SoundKind.Music, first.Name)!;
        var stereo = new short[source.SampleRate * 2 * 2];
        Assert.That(source.Read(stereo, loop: true), Is.EqualTo(stereo.Length / 2));
        Assert.That(stereo.Any(sample => sample != 0), Is.True, "two seconds of silence");

        TestContext.Out.WriteLine(string.Join(", ", entries.GroupBy(entry => entry.Kind).Select(kind => $"{kind.Key}: {kind.Count()}")));
    }
}
