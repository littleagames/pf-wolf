namespace PFWolf.Tests;

public class TrigTablesTests
{
    [OneTimeSetUp]
    public void Tables() => Program.BuildTables();

    [Test]
    public void The_Cosine_Table_Is_The_Sine_Table_A_Quarter_Turn_On()
    {
        Assert.That(Program.costable, Has.Length.EqualTo(Program.ANGLES));
        for (int angle = 0; angle < Program.ANGLES; angle++)
            Assert.That(Program.costable[angle], Is.EqualTo(Program.sintable[angle + Program.ANGLES / 4]), $"angle {angle}");
    }

    [TestCase(0, 65536)]
    [TestCase(90, 0)]
    [TestCase(180, -65536)]
    [TestCase(270, 0)]
    public void The_Cosine_Of_The_Right_Angles(int angle, int cosine)
    {
        Assert.That(Program.costable[angle], Is.EqualTo(cosine).Within(1));
    }

    [Test]
    public void Reading_It_Is_The_Same_Table_Each_Time()
    {
        // (it was once a slice of sintable, which made a copy on every read)
        Assert.That(Program.costable, Is.SameAs(Program.costable));
    }
}
