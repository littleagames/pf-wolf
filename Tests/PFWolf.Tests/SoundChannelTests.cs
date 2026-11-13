using PFWolf.Assets.Sounds;
using PFWolf.Loaders;
using PFWolf.Managers;

namespace PFWolf.Tests;

/// <summary>
/// Which voice a sound plays on: a free shared one (else the oldest), or its channel's own, as
/// Wolf4SDL's mixer groups and reserved weapon channels
/// </summary>
public class SoundChannelTests
{
    [Test]
    public void A_Sound_Takes_The_Next_Free_Voice_From_Where_The_Last_Was_Taken()
    {
        // Arrange: voices 2 and 3 sounding
        long[] started = [5, 6, 7, 8];

        // Act
        var index = AudioManager.PickSharedSource(4, next: 2, started, i => i is 2 or 3);

        // Assert: round past them to 0
        Assert.That(index, Is.EqualTo(0));
    }

    [Test]
    public void A_Voice_That_Is_Sounding_Is_Not_Taken_While_Another_Is_Free()
    {
        // Arrange: every voice but 3 sounding (a held chaingun's shots once round them all)
        long[] started = [1, 2, 3, 4];

        // Act
        var index = AudioManager.PickSharedSource(4, next: 0, started, i => i != 3);

        // Assert
        Assert.That(index, Is.EqualTo(3));
    }

    [Test]
    public void With_Every_Voice_Sounding_The_One_Started_Longest_Ago_Is_Taken()
    {
        // Arrange
        long[] started = [9, 4, 12, 7];

        // Act
        var index = AudioManager.PickSharedSource(4, next: 2, started, _ => true);

        // Assert
        Assert.That(index, Is.EqualTo(1));
    }

    [Test]
    public void The_Bosses_Chaingun_Plays_On_The_Boss_Channel()
    {
        // Arrange
        var loader = new PfWolfPk3Loader([new DirectoryAssetSource(TestPaths.Pk3SourceFolder())], "wolf3d", "wolf3d");

        // Act
        var sounds = loader.Load<SoundSequenceAsset>("sound-seq").SoundInfo;

        // Assert: every sound of DSBOSSFR (Wolf4SDL's BOSSFIRESND, on its channel 1)
        var bossFire = sounds.Where(s => s.Value.Digitized == "DSBOSSFR").ToList();
        Assert.That(bossFire, Is.Not.Empty);
        Assert.That(bossFire.Where(s => s.Value.Channel != "boss").Select(s => s.Key), Is.Empty);
    }
}
