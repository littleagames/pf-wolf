using PFWolf.Assets.Sounds;
using PFWolf.Editor.Audio.Opl;
using PFWolf.Editor.Data;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Tests;

/// <summary>
/// Every game's IMF songs read into notes and written again play the same, and its AdLib and PC
/// speaker sounds written again are the same bytes. Needs a built game folder with the data files.
/// </summary>
[Category("GameData")]
public class ImfSongGameTests
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

    private static List<(string Name, T Asset)> All<T>(GameContent content) where T : PFWolf.Assets.Asset
        => content.Assets.AssetNames.Where(name => !name.Contains('/') && content.Assets.Exists<T>(name))
            .Select(name => (name, content.Find<T>(name)!)).ToList();

    [TestCase("wolf3d")]
    [TestCase("spear")]
    [TestCase("wolf3d-shareware")]
    [TestCase("blake")]
    [TestCase("planetstrike")]
    [TestCase("blake-shareware")]
    public void Songs_Read_And_Written_Again_Play_The_Same(string game)
    {
        var songs = All<Wolf3dImfAudio>(Load(game));
        Assert.That(songs, Is.Not.Empty);
        foreach (var (name, audio) in songs)
        {
            var song = ImfSongReader.Read(audio);
            var written = ImfSongWriter.Write(song);
            ImfPlayback.AssertSame(ImfPlayback.Of(audio.Commands), ImfPlayback.Of(written), $"{game} {name}");
            Assert.That(song.Notes, Is.Not.Empty, $"{game} {name} has notes");
        }
        TestContext.Out.WriteLine($"{game}: {songs.Count} songs,{songs.Sum(song => ImfSongReader.Read(song.Asset).Writes.Count)} writes kept as writes, "
            + $"{songs.Sum(song => ImfSongReader.Read(song.Asset).Notes.Count)} notes, {songs.Sum(song => ImfSongReader.Read(song.Asset).PitchChanges.Count)} pitch changes");
    }

    [Test]
    public void Exported_Files_Load_As_The_Same_Song_And_Sounds()
    {
        var content = Load("wolf3d");
        var song = SoundFiles.Exportable(content, SoundKind.Music, "CORNER");
        Assert.That(song?.Extension, Is.EqualTo(".imf"));
        Assert.That(Wolf3dImfAudio.FromFile(song!.Value.Data).Commands, Is.EqualTo(content.Find<Wolf3dImfAudio>("CORNER")!.Commands));

        var (adLibName, adLib) = All<AdLibSound>(content)[0];
        var adLibFile = SoundFiles.Exportable(content, SoundKind.AdLib, adLibName)!.Value;
        Assert.That(adLibFile.Extension, Is.EqualTo(".adl"));
        Assert.That(AdLibSound.FromFile(adLibFile.Data).ToPcm16(), Is.EqualTo(adLib.ToPcm16()));

        var (pcName, pc) = All<PcSound>(content)[0];
        var pcFile = SoundFiles.Exportable(content, SoundKind.PcSpeaker, pcName)!.Value;
        Assert.That(PcSound.FromFile(pcFile.Data).ToMono8(), Is.EqualTo(pc.ToMono8()));

        Assert.That(SoundFiles.Exportable(content, SoundKind.Digitized, "DSDOGBARK"), Is.Null);
    }

    [TestCase("wolf3d")]
    [TestCase("blake")]
    public void AdLib_And_Pc_Sounds_Written_Again_Are_The_Same(string game)
    {
        var content = Load(game);
        foreach (var (name, sound) in All<AdLibSound>(content))
        {
            var written = AdLibSound.Create(sound.Priority, sound.Instrument, sound.Block, sound.Tones);
            // Voice, mode and the unused bytes are written 0; anything past the tones isn't the sound's
            var expected = sound.RawData[..written.RawData.Length];
            expected.AsSpan(17, 5).Clear();
            Assert.That(written.RawData, Is.EqualTo(expected), $"{game} AdLib {name}");
        }
        foreach (var (name, sound) in All<PcSound>(content))
        {
            var written = PcSound.Create(sound.Priority, sound.Values);
            Assert.That(written.RawData, Is.EqualTo(sound.RawData[..written.RawData.Length]), $"{game} PC {name}");
        }
    }

    [TestCase("wolf3d")]
    [TestCase("blake")]
    public void Pc_Sounds_Open_In_The_Editor_As_They_Are_And_AdLib_Ones_Make_Pc_Ones(string game)
    {
        var content = Load(game);
        var adLibs = All<AdLibSound>(content).ToDictionary(sound => sound.Name, sound => sound.Asset, StringComparer.OrdinalIgnoreCase);
        var sequences = SoundCatalog.Sequences(content);
        int lowest = 255, highest = 0, made = 0, ticks = 0, near = 0;
        var differences = new List<double>();
        foreach (var (name, sound) in All<PcSound>(content))
        {
            var draft = PcDraft.From(sound);
            Assert.That(draft.ToFile(), Is.EqualTo(sound.RawData[..draft.ToFile().Length]), $"{game} PC {name}");
            foreach (var value in draft.Tones.Where(value => value != 0))
                (lowest, highest) = (Math.Min(lowest, value), Math.Max(highest, value));

            // Its AdLib version, as the sound-seq entries pair them (PCHITWAL and ALHITWAL)
            var partner = SoundCatalog.PartnerOf(sequences, SoundKind.PcSpeaker, name);
            if (!adLibs.TryGetValue(partner, out var adLib))
                continue;
            Assert.That(SoundCatalog.PartnerOf(sequences, SoundKind.AdLib, partner), Is.EqualTo(name).IgnoreCase);
            var fromAdLib = PcDraft.FromAdLib(AdLibDraft.From(adLib), draft.Priority);
            Assert.That(fromAdLib.Tones, Has.Length.EqualTo(adLib.Tones.Length));
            made++;
            // How near the game's own PC versions the made ones come: ticks within a semitone
            for (var tick = 0; tick < Math.Min(draft.Tones.Length, fromAdLib.Tones.Length); tick++)
            {
                if (draft.Tones[tick] == 0 || fromAdLib.Tones[tick] == 0)
                    continue;
                ticks++;
                var difference = OplPitch.MidiNote(draft.Frequency(draft.Tones[tick])) - OplPitch.MidiNote(fromAdLib.Frequency(fromAdLib.Tones[tick]));
                differences.Add(difference);
                if (Math.Abs(difference) <= 1)
                    near++;
            }
        }
        differences.Sort();
        var median = differences[differences.Count / 2];
        TestContext.Out.WriteLine($"{game}: PC values {lowest}-{highest}; {made} made from AdLib, {near} of {ticks} sounding ticks within a semitone of the game's;"
            + $" the game's are a median {median:0.#} semitones higher");
        Assert.That(made, Is.GreaterThan(0));
        // The game's PC versions weren't made from the AdLib ones; they mostly sit about an octave
        // up, which is why "Make it from" goes an octave higher unless told otherwise
        Assert.That(median, Is.InRange(6, 18));
    }
}
