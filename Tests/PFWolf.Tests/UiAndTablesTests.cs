using PFWolf.Assets;

namespace PFWolf.Tests;

public class RandomTableTests
{
    [Test]
    public void From_The_Start_It_Gives_Ids_Numbers()
    {
        // Arrange
        var random = new RandomTable();
        random.InitRndT(randomize: false);

        // Act / Assert: id's table, from its second entry
        Assert.That(Enumerable.Range(0, 5).Select(_ => random.RndT()), Is.EqualTo(new[] { 8, 109, 220, 222, 241 }));
    }

    [Test]
    public void It_Wraps_Round_After_256()
    {
        // Arrange
        var random = new RandomTable();
        random.InitRndT(randomize: false);
        var first = Enumerable.Range(0, 256).Select(_ => random.RndT()).ToList();

        // Act / Assert
        Assert.That(Enumerable.Range(0, 256).Select(_ => random.RndT()), Is.EqualTo(first));
        Assert.That(random.RndIndex, Is.EqualTo(0));
    }

    [Test]
    public void Two_Tables_At_The_Same_Place_Agree()
    {
        // Arrange: as a joining player is given the host's place
        var host = new RandomTable { RndIndex = 77 };
        var joiner = new RandomTable { RndIndex = host.RndIndex };

        // Act / Assert
        for (int i = 0; i < 300; i++)
            Assert.That(joiner.RndT(), Is.EqualTo(host.RndT()));
    }

    [Test]
    public void Randomizing_Stays_In_The_Table()
    {
        // Arrange
        var random = new RandomTable();

        // Act
        random.InitRndT(randomize: true);

        // Assert
        Assert.That(random.RndIndex, Is.InRange(0, 255));
    }
}

public class HighScoreTests
{
    [Test]
    public void Wolf3ds_Table_Is_Kept_When_The_Game_Pack_Leaves_It()
    {
        // Arrange
        var scores = HighScore.WolfDefaults();

        // Act / Assert
        Assert.That(HighScore.TableFor(new HighScoresInfo(), scores), Is.SameAs(scores));
    }

    [Test]
    public void A_Longer_Table_Keeps_Wolf3ds_And_Adds_Plain_Ones()
    {
        // Arrange
        var scores = HighScore.WolfDefaults();

        // Act
        var table = HighScore.TableFor(new HighScoresInfo { Count = 9 }, scores);

        // Assert
        Assert.That(table, Has.Length.EqualTo(9));
        Assert.That(table[0].Name, Is.EqualTo("id software-'92"));
        Assert.That((table[8].Name, table[8].Score, table[8].Completed), Is.EqualTo(("", 10000, (ushort)1)));
    }

    [Test]
    public void The_Game_Packs_Defaults_Replace_Wolf3ds()
    {
        // Act
        var table = HighScore.TableFor(new HighScoresInfo
        {
            Defaults = [new HighScoreDefault { Name = "Blake", Score = 5000, Level = 3 }],
            Count = 2,
        }, HighScore.WolfDefaults());

        // Assert
        Assert.That((table[0].Name, table[0].Score, table[0].Completed), Is.EqualTo(("Blake", 5000, (ushort)3)));
        Assert.That(table[1].Name, Is.Empty);
    }

    [TestCase(0, 1)]
    [TestCase(500, 100)]
    public void The_Count_Is_Kept_Sensible(int count, int expected)
    {
        Assert.That(HighScore.TableFor(new HighScoresInfo { Count = count }, HighScore.WolfDefaults()), Has.Length.EqualTo(expected));
    }

    [Test]
    public void A_Score_Reads_Back_What_Was_Written()
    {
        // Arrange
        var score = new HighScore { Name = "Jérôme", Score = 123456, Completed = 9, Episode = 2 };
        using var stream = new MemoryStream();

        // Act
        score.Write(new BinaryWriter(stream));
        stream.Position = 0;
        var read = new HighScore();
        read.Read(new BinaryReader(stream));

        // Assert: Latin-1, so an accented letter is still one byte
        Assert.That(stream.Length, Is.EqualTo(HighScore.MaxHighName + 1 + 4 + 2 + 2));
        Assert.That((read.Name, read.Score, read.Completed, read.Episode), Is.EqualTo(("Jérôme", 123456, (ushort)9, (ushort)2)));
    }

    [Test]
    public void A_Long_Name_Is_Cut_To_Fit()
    {
        // Arrange
        var score = new HighScore { Name = new string('x', 100) };
        using var stream = new MemoryStream();

        // Act
        score.Write(new BinaryWriter(stream));
        stream.Position = 0;
        var read = new HighScore();
        read.Read(new BinaryReader(stream));

        // Assert
        Assert.That(read.Name, Has.Length.EqualTo(HighScore.MaxHighName));
    }
}

public class LineInputTests
{
    [TestCase('A', 1, 'B')]
    [TestCase('B', -1, 'A')]
    [TestCase(' ', 1, 'A')]
    [TestCase('9', 1, ' ')]         // round from the last to the first
    [TestCase(' ', -1, '9')]
    [TestCase('a', 1, 'A')]         // one it doesn't have counts as a space
    public void RotateChar_Steps_Through_The_Letters(char ch, int dir, char expected)
    {
        Assert.That(LineInput.RotateChar(ch, dir), Is.EqualTo(expected));
    }
}
