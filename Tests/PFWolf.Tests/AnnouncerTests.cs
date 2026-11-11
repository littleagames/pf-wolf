using PFWolf.Assets;

namespace PFWolf.Tests;

public class AnnouncerTests
{
    private sealed class Rig
    {
        public readonly GameSession Session = new();
        public readonly List<string> Sounds = [];
        public readonly List<string> Messages = [];
        public AnnouncerInfo Settings = new();
        public readonly Announcer Announcer;

        public Rig()
        {
            // This machine's player is the first; a second, Client, plays too
            Session.PlayerState.Name = "Host";
            Session.Players.Add(new Entities.PlayerState { Number = 1, Name = "Client" });
            Session.SetGameMode(GameMode.Deathmatch);
            Announcer = new Announcer(Session, new AnnouncerHooks(
                () => Settings,
                Sounds.Add,
                (text, style, placeholders) => Messages.Add($"{style}: " + string.Concat(text.Select((c, i) =>
                    i > 0 && text[i - 1] == '%' ? placeholders[c] : c == '%' ? "" : c.ToString()))),
                p => p.Name!));
        }

        public Entities.PlayerState Local => Session.Players[0];
        public Entities.PlayerState Other => Session.Players[1];

        public void Streak(Entities.PlayerState p, int length)
        {
            p.Streak = length;
            Announcer.Streaked(p);
        }
    }

    [Test]
    public void A_Streak_Reaching_A_Number_Gives_Its_Sound_And_Message_To_The_Player_On_It()
    {
        // Arrange
        var rig = new Rig
        {
            Settings = new AnnouncerInfo { Streaks = new() { [3] = new AnnouncerCue { Sound = "announcer/spree", Message = "%p: %n in a row!" } } },
        };

        // Act: theirs at 2 and 3, then the other player's at 3
        rig.Streak(rig.Local, 2);
        rig.Streak(rig.Local, 3);
        rig.Streak(rig.Other, 3);

        // Assert
        Assert.That(rig.Sounds, Is.EqualTo(new[] { "announcer/spree" }));
        Assert.That(rig.Messages, Is.EqualTo(new[] { "Center: Host: 3 in a row!" }));
    }

    [TestCase("others", false, true)]
    [TestCase("all", true, true)]
    [TestCase("SELF", true, false)]
    public void A_Streaks_Cue_Can_Be_For_Everyone_Else_Or_Everyone(string to, bool ownHeard, bool othersHeard)
    {
        // Arrange
        var rig = new Rig { Settings = new AnnouncerInfo { Streaks = new() { [5] = new AnnouncerCue { Sound = "rampage", To = to } } } };

        // Act / Assert
        rig.Streak(rig.Local, 5);
        Assert.That(rig.Sounds, Has.Count.EqualTo(ownHeard ? 1 : 0));
        rig.Sounds.Clear();
        rig.Streak(rig.Other, 5);
        Assert.That(rig.Sounds, Has.Count.EqualTo(othersHeard ? 1 : 0));
    }

    [TestCase("Won", "win")]
    [TestCase("Lost", "lose")]
    [TestCase("Draw", "draw")]
    public void As_The_Match_Ends_This_Machines_Player_Hears_How_It_Went_For_Them(string outcomeName, string sound)
    {
        // Arrange
        var outcome = Enum.Parse<MatchOutcome>(outcomeName);
        var rig = new Rig
        {
            Settings = new AnnouncerInfo
            {
                MatchWon = new AnnouncerCue { Sound = "win", Message = "You win!", Style = "Big" },
                MatchLost = new AnnouncerCue { Sound = "lose" },
                MatchDraw = new AnnouncerCue { Sound = "draw" },
            },
        };

        // Act: asked about this machine's player only
        rig.Announcer.MatchOver(p => ReferenceEquals(p, rig.Local) ? outcome : throw new AssertionException("asked about another player"));

        // Assert
        Assert.That(rig.Sounds, Is.EqualTo(new[] { sound }));
        Assert.That(rig.Messages, Is.EqualTo(outcome == MatchOutcome.Won ? new[] { "Big: You win!" } : Array.Empty<string>()));
    }

    [Test]
    public void Game_Info_Gives_The_Cues_By_Streak_Length_And_Match_End()
    {
        // Act
        var announcer = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<GameInfoAsset>("""
            announcer:
              streaks:
                3: { sound: announcer/spree, message: "Killing spree!" }
                5: { sound: announcer/rampage, to: all }
              match-won: { sound: announcer/win, style: Big }
              match-draw: {}
            """).Announcer;

        // Assert
        Assert.That(announcer.Streaks.Keys, Is.EquivalentTo(new[] { 3, 5 }));
        Assert.That(announcer.Streaks[3], Is.EqualTo(new AnnouncerCue { Sound = "announcer/spree", Message = "Killing spree!" }));
        Assert.That(announcer.Streaks[5].To, Is.EqualTo("all"));
        Assert.That(announcer.MatchWon, Is.EqualTo(new AnnouncerCue { Sound = "announcer/win", Style = "Big" }));
        Assert.That((announcer.MatchLost, announcer.MatchDraw), Is.EqualTo(((AnnouncerCue?)null, new AnnouncerCue())));
    }

    [Test]
    public void Empty_Slots_Give_Nothing()
    {
        // Arrange: game-info's slots, before there are sounds for them
        var rig = new Rig
        {
            Settings = new AnnouncerInfo { Streaks = new() { [3] = new AnnouncerCue() }, MatchWon = new AnnouncerCue { Sound = " " } },
        };

        // Act
        rig.Streak(rig.Local, 3);
        rig.Announcer.MatchOver(_ => MatchOutcome.Won);

        // Assert
        Assert.That((rig.Sounds, rig.Messages), Is.EqualTo((new List<string>(), new List<string>())));
    }
}
