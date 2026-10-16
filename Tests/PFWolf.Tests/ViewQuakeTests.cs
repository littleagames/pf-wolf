using PFWolf.Rendering;

namespace PFWolf.Tests;

public class ViewQuakeTests
{
    [Test]
    public void A_Still_View_Isnt_Pushed()
    {
        Assert.That(new ViewQuake(new Random(1)).Update(5, 0), Is.Zero);
    }

    [Test]
    public void A_Quake_Pulses_The_Focal_Length_Then_Dies_Away()
    {
        // Arrange: power 20 is an intensity of 0.2, gone in 40 tics
        var quake = new ViewQuake(new Random(1));
        quake.Start(20);

        // Act: 3 tics in, the pulse is at 90 degrees
        long pushed = quake.Update(3, 0);

        // Assert
        Assert.That(pushed, Is.EqualTo((long)((0.2 - 3 * ViewQuake.DecayPerTic) * ViewQuake.UnitExcursion)));
        Assert.That(quake.Update(37, 0), Is.Zero);
        Assert.That(quake.Intensity, Is.Zero);
    }

    [Test]
    public void Quakes_Add_Up_To_Half_An_Intensity()
    {
        // Arrange
        var quake = new ViewQuake(new Random(1));

        // Act
        quake.Start(30);
        quake.Start(30);

        // Assert
        Assert.That(quake.Intensity, Is.EqualTo(ViewQuake.MaxIntensity));
    }

    [Test]
    public void No_Power_Is_No_Quake()
    {
        var quake = new ViewQuake(new Random(1));
        quake.Start(0);
        Assert.That(quake.Intensity, Is.Zero);
    }

    [Test]
    public void A_Rumbling_Level_Quakes_Every_So_Often_And_A_Still_One_Doesnt()
    {
        // Arrange: 141 tics is past the longest wait (20 + 119)
        var still = new ViewQuake(new Random(1));
        var rumbling = new ViewQuake(new Random(1));

        // Act: a tic at a time, noting whether the view ever moved
        bool stillMoved = false, rumbleMoved = false;
        for (int tic = 0; tic < 400; tic++)
        {
            stillMoved |= still.Update(1, 0) != 0;
            rumbleMoved |= rumbling.Update(1, 10) != 0;
        }

        // Assert
        Assert.That(stillMoved, Is.False);
        Assert.That(rumbleMoved, Is.True);
    }

    [Test]
    public void A_Reset_Stops_It()
    {
        var quake = new ViewQuake(new Random(1));
        quake.Start(50);
        quake.Reset();
        Assert.That(quake.Update(1, 0), Is.Zero);
    }
}
