using PFWolf.Assets.Sounds;
using PFWolf.Managers;
using static PFWolf.Managers.AudioManager;

namespace PFWolf.Tests;

/// <summary>Which of a sound's variants plays: the best one the game has, on the devices switched on</summary>
public class SoundVariantTests
{
    // Wolf3D's ammo pickup: no digitized sound in the game, an AdLib and PC speaker one
    private static readonly SoundProfile AmmoPickup = new() { Digitized = "DSAMMOUP", AdLib = "ALAMMOUP", PC = "PCAMMOUP" };

    // The game's own sounds (no pk3 sound files)
    private static Func<SoundDevice, string, bool> GameHas(params string[] assets) =>
        (device, asset) => device != SoundDevice.File && assets.Contains(asset, StringComparer.OrdinalIgnoreCase);

    [Test]
    public void A_Digitized_Sound_The_Game_Hasnt_Got_Falls_Back_To_AdLib()
    {
        // Act
        var variant = AudioManager.BestVariant(AmmoPickup, digitized: true, adLib: true, pc: true, GameHas("ALAMMOUP", "PCAMMOUP"));

        // Assert
        Assert.That(variant, Is.EqualTo((SoundDevice.AdLib, "ALAMMOUP")));
    }

    [Test]
    public void The_Best_Variant_On_The_Devices_Switched_On_Plays()
    {
        // Arrange
        var exists = GameHas("DSAMMOUP", "ALAMMOUP", "PCAMMOUP");

        // Act
        var all = AudioManager.BestVariant(AmmoPickup, true, true, true, exists);
        var noDigitized = AudioManager.BestVariant(AmmoPickup, false, true, true, exists);
        var speakerOnly = AudioManager.BestVariant(AmmoPickup, false, false, true, exists);
        var none = AudioManager.BestVariant(AmmoPickup, false, false, false, exists);

        // Assert
        Assert.That(all, Is.EqualTo((SoundDevice.Digitized, "DSAMMOUP")));
        Assert.That(noDigitized, Is.EqualTo((SoundDevice.AdLib, "ALAMMOUP")));
        Assert.That(speakerOnly, Is.EqualTo((SoundDevice.PC, "PCAMMOUP")));
        Assert.That(none, Is.Null);
    }

    [Test]
    public void A_Pk3s_Sound_File_Wins_Over_The_Games_Digitized_Sound()
    {
        // Arrange: sounds/DSAMMOUP.ogg in a mod
        Func<SoundDevice, string, bool> exists = (device, asset) => device == SoundDevice.File && asset == "DSAMMOUP" || asset == "ALAMMOUP";

        // Act
        var variant = AudioManager.BestVariant(AmmoPickup, true, true, true, exists);

        // Assert
        Assert.That(variant, Is.EqualTo((SoundDevice.File, "DSAMMOUP")));
    }

    [Test]
    public void A_Sound_With_None_Of_Its_Variants_Has_Nothing_To_Play()
    {
        // Act: Spear's angel in Wolf3D
        var variant = AudioManager.BestVariant(new SoundProfile { Digitized = "DSAGLATK", AdLib = "ALAGLATK" }, true, true, true, GameHas());

        // Assert
        Assert.That(variant, Is.Null);
    }
}
