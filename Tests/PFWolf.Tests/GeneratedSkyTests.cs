namespace PFWolf.Tests;

public class GeneratedSkyTests
{
    [Test]
    public void The_C_Librarys_Rand_Is_Windows_Own()
    {
        // srand(1): rand() gives 41, then 18467 (MSVC's well-known sequence)
        var rand = new SkyAndFlats.CRand(1);

        Assert.That(rand.Next(), Is.EqualTo(41));
        Assert.That(rand.Next(), Is.EqualTo(18467));
    }

    [Test]
    public void A_Seed_Always_Makes_The_Same_Clouds()
    {
        byte[] colors = [31, 30, 29, 28, 0xff, 0xff, 0xff];

        var first = SkyAndFlats.MakeClouds(626, colors);
        var again = SkyAndFlats.MakeClouds(626, colors);
        var other = SkyAndFlats.MakeClouds(627, colors);

        Assert.That(again, Is.EqualTo(first));
        Assert.That(other, Is.Not.EqualTo(first));
    }

    [Test]
    public void The_Clouds_Use_Only_Their_Colors_And_Have_Gaps()
    {
        byte[] colors = [31, 30, 29, 28, 0xff, 0xff, 0xff];

        var clouds = SkyAndFlats.MakeClouds(626, colors);

        Assert.That(clouds, Has.Length.EqualTo(65536));
        Assert.That(clouds.Distinct(), Is.SubsetOf(colors));
        Assert.That(clouds, Has.Some.EqualTo((byte)0xff), "see-through gaps");
        Assert.That(clouds, Has.Some.Not.EqualTo((byte)0xff), "clouds");
    }

    [TestCase(300, 1)]
    [TestCase(640, 1)]
    [TestCase(641, 2)]
    [TestCase(1280, 2)]
    [TestCase(1650, 3)]
    [TestCase(3840, 3)]
    public void Stars_Grow_With_The_View(int viewWidth, int size) =>
        Assert.That(SkyAndFlats.StarSize(viewWidth), Is.EqualTo(size));

    [TestCase(304, 10)]
    [TestCase(608, 20)]
    [TestCase(1650, 54)]
    [TestCase(200, 10)]
    public void The_Moon_Keeps_Its_Share_Of_The_View(int viewWidth, int pixels) =>
        Assert.That(SkyAndFlats.MoonPixels(viewWidth), Is.EqualTo(pixels));

    [Test]
    public void Stars_Are_Spread_Around_And_Above()
    {
        var stars = SkyAndFlats.MakeStars(500, new Random(5));

        Assert.That(stars, Has.Length.EqualTo(500));
        Assert.That(stars.All(s => s.X is >= -16383 and <= 16384 && s.Z is >= -16383 and <= 16384), Is.True);
        Assert.That(stars.All(s => s.Y is >= 1024 and < 9216), Is.True);
    }
}
