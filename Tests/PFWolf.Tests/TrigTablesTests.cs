namespace PFWolf.Tests;

public class TrigTablesTests
{
    [OneTimeSetUp]
    public void Tables() => TrigTables.BuildTables();

    [Test]
    public void The_Cosine_Table_Is_The_Sine_Table_A_Quarter_Turn_On()
    {
        Assert.That(TrigTables.costable, Has.Length.EqualTo(GameConstants.Angles));
        for (int angle = 0; angle < GameConstants.Angles; angle++)
            Assert.That(TrigTables.costable[angle], Is.EqualTo(TrigTables.sintable[angle + GameConstants.Angles / 4]), $"angle {angle}");
    }

    [TestCase(0, 65536)]
    [TestCase(90, 0)]
    [TestCase(180, -65536)]
    [TestCase(270, 0)]
    public void The_Cosine_Of_The_Right_Angles(int angle, int cosine)
    {
        Assert.That(TrigTables.costable[angle], Is.EqualTo(cosine).Within(1));
    }

    [Test]
    public void Reading_It_Is_The_Same_Table_Each_Time()
    {
        // (it was once a slice of sintable, which made a copy on every read)
        Assert.That(TrigTables.costable, Is.SameAs(TrigTables.costable));
    }
}
