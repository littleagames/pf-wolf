using PFWolf.Assets;

namespace PFWolf.Tests;

public class MenuPackChainTests
{
    private static GamePackInfoAsset Info() => new(new Dictionary<string, GamePack>(StringComparer.OrdinalIgnoreCase)
    {
        ["wolf3d"] = new(),
        ["wolf3d-apogee"] = new() { BasePack = "wolf3d" },
        ["standalone"] = new(),
        ["my-game"] = new() { BasePack = "standalone", MenusLike = "wolf3d-apogee" },
    });

    [Test]
    public void A_Game_Answers_To_Its_Base_Packs_Then_Its_Menus_Like_Pack_Alone()
    {
        var chain = Info().GetMenuPackChain("my-game");
        Assert.That(chain, Is.EqualTo(new[] { "standalone", "wolf3d-apogee" }));
    }

    [Test]
    public void Its_Files_Still_Come_From_Its_Base_Packs_Only()
    {
        Assert.That(Info().GetBasePackChain("my-game"), Is.EqualTo(new[] { "standalone" }));
    }

    [Test]
    public void So_Apogees_Read_This_Is_Its_But_Wolf3Ds_Own_Items_Arent()
    {
        var chain = Info().GetMenuPackChain("my-game");
        Assert.That(GamePackList.Includes(["wolf3d-shareware", "wolf3d-apogee"], "my-game", chain), Is.True);
        Assert.That(GamePackList.Includes(["*", "!wolf3d-shareware", "!wolf3d-apogee"], "my-game", chain), Is.False);
        Assert.That(GamePackList.Includes(["wolf3d"], "my-game", chain), Is.False);
    }

    [Test]
    public void Without_Menus_Like_It_Is_The_Base_Chain()
    {
        Assert.That(Info().GetMenuPackChain("wolf3d-apogee"), Is.EqualTo(new[] { "wolf3d" }));
    }
}
