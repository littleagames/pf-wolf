using PFWolf.Assets.Sounds;
using PFWolf.Editor.Audio;

namespace PFWolf.Tests;

/// <summary>
/// The editor's sound player through OpenAL, as it plays off Windows (docs/dev/mac-linux-plan.md),
/// run here too. Needs an OpenAL library and an audio device: ignored without them, and with
/// Creative's old OpenAL32.dll in System32, whose Generic Software device plays at the wrong
/// speed. (Copy OpenAL Soft's OpenAL32.dll beside the tests to run them on Windows.)
/// </summary>
[NonParallelizable]
public class SoundPlayerOpenALTests
{
    private static void IgnoreUnlessUsable()
    {
        if (!SoundPlayer.IsAvailable)
            Assert.Ignore("OpenAL couldn't be started here");
        if (OpenTK.Audio.OpenAL.AL.Get(OpenTK.Audio.OpenAL.ALGetString.Vendor) is { } vendor && vendor.Contains("Creative", StringComparison.OrdinalIgnoreCase))
            Assert.Ignore("Creative's OpenAL, not OpenAL Soft: its Generic Software device plays at the wrong speed");
    }

    // A quiet tone: stereo, a set number of frames, then the end
    private sealed class ToneSource(int frames) : MusicSource
    {
        private int _left = frames;

        public override int SampleRate => 22050;

        public override int Read(short[] stereo, bool loop)
        {
            var count = Math.Min(_left, stereo.Length / 2);
            for (var frame = 0; frame < count; frame++)
                stereo[frame * 2] = stereo[frame * 2 + 1] = (short)(frame % 50 < 25 ? 200 : -200);
            _left -= count;
            return count;
        }
    }

    [Test]
    public void A_Sound_Plays_To_The_End_Through_OpenAL()
    {
        var wasOpenAL = SoundPlayer.UseOpenAL;
        SoundPlayer.UseOpenAL = true;
        try
        {
            IgnoreUnlessUsable();

            // Arrange: 0.35 s, so three chunks and a short one
            using var player = new SoundPlayer();
            using var done = new ManualResetEventSlim();
            player.Finished += (_, _) => done.Set();

            // Act
            player.Play(new ToneSource(7717), loop: false);
            var finished = done.Wait(TimeSpan.FromSeconds(5));

            // Assert
            Assert.That(finished, Is.True, "Finished wasn't raised");
            Assert.That(player.Position.TotalSeconds, Is.EqualTo(7717 / 22050.0).Within(0.001));
        }
        finally
        {
            SoundPlayer.UseOpenAL = wasOpenAL;
        }
    }

    [Test]
    public void Stopping_Part_Way_Keeps_How_Far_It_Got()
    {
        var wasOpenAL = SoundPlayer.UseOpenAL;
        SoundPlayer.UseOpenAL = true;
        try
        {
            IgnoreUnlessUsable();

            // Arrange: two seconds
            using var player = new SoundPlayer();
            var finished = false;
            player.Finished += (_, _) => finished = true;

            // Act
            player.Play(new ToneSource(44100), loop: false);
            Thread.Sleep(500);
            player.Stop();

            // Assert
            Assert.That(finished, Is.False);
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(player.Position.TotalSeconds, Is.InRange(0.2, 1.5));
        }
        finally
        {
            SoundPlayer.UseOpenAL = wasOpenAL;
        }
    }
}
