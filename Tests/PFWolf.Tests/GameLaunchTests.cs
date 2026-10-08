using PFWolf.Editor.Editing;

namespace PFWolf.Tests;

public class GameLaunchTests
{
    [Test]
    public void Play_Warps_Past_The_Menus_With_The_Mods_In_Order()
    {
        // Act
        var arguments = GameLaunch.Arguments("spear", "map03", skill: null, start: null, [@"C:\mods\a", @"C:\mods\b c"]);

        // Assert
        Assert.That(arguments, Is.EqualTo(new[] { "--game", "spear", "--nowait", "--warp", "MAP03", "--file", @"C:\mods\a", "--file", @"C:\mods\b c" }));
    }

    [Test]
    public void Play_From_Here_Adds_The_Skill_And_Tile()
    {
        // Act
        var arguments = GameLaunch.Arguments("wolf3d", "MAP01", skill: 4, start: (30, 35), []);

        // Assert
        Assert.That(arguments, Is.EqualTo(new[] { "--game", "wolf3d", "--nowait", "--warp", "MAP01", "--skill", "4", "--start", "30,35" }));
    }
}
