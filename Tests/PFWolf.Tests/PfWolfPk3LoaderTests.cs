using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Tests;

public class PfWolfPk3LoaderTests
{
    // alpha is a pack of its own; beta is built on alpha; gamma has nothing to do with either
    private const string GamePackInfo = """
        alpha:
          title: Alpha
          game-palette: pal
        beta:
          title: Beta
          game-palette: pal
          base-pack: alpha
        gamma:
          title: Gamma
          game-palette: pal
        """;

    private static MemoryAssetSource BasePk3() => new("pfwolf.pk3", new()
    {
        ["gamepacks/gamepack-info.yaml"] = GamePackInfo,
        ["actordefs/native.yaml"] = "Inventory:\n  radius: 1\n",
        ["actordefs/alpha/guards.yaml"] = """
            Guard:
              radius: 10
              properties:
                hp: 25
                speed: 512
            Dog:
              radius: 11
            """,
        ["actordefs/beta/guards.yaml"] = "Guard:\n  radius: 20\n  properties:\n    hp: 50\n",
        ["actordefs/gamma/guards.yaml"] = "Robot:\n  radius: 30\n",
        ["language/en-us.yaml"] = "SHARED: shared\n",
        ["gamepacks/alpha/language/en-us.yaml"] = "HELLO: alpha hello\nBYE: alpha bye\n",
        ["gamepacks/beta/language/en-us.yaml"] = "HELLO: beta hello\n",
    });

    private static Dictionary<string, ActorData> Actors(PfWolfPk3Loader loader, string name) =>
        loader.Load<ActorTranslationAsset>(name).Actors;

    private static Dictionary<string, string> Strings(PfWolfPk3Loader loader, string name) =>
        loader.Load<LanguageAsset>(name).Strings;

    [Test]
    public void Shared_Actordefs_Load_Under_Their_Own_Name()
    {
        // Act
        var loader = new PfWolfPk3Loader([BasePk3()], "alpha", "alpha");

        // Assert
        Assert.That(Actors(loader, "actordefs").Keys, Is.EqualTo(new[] { "Inventory" }));
        Assert.That(Strings(loader, "language/en-us")["SHARED"], Is.EqualTo("shared"));
    }

    [Test]
    public void Running_Pack_Leaves_Out_Other_Packs()
    {
        // Act
        var loader = new PfWolfPk3Loader([BasePk3()], "alpha", "alpha");

        // Assert
        Assert.That(Actors(loader, "alpha/actordefs").Keys, Is.EquivalentTo(new[] { "Guard", "Dog" }));
        Assert.Throws<KeyNotFoundException>(() => loader.Load<ActorTranslationAsset>("beta/actordefs"));
        Assert.Throws<KeyNotFoundException>(() => loader.Load<ActorTranslationAsset>("gamma/actordefs"));
    }

    [Test]
    public void A_Pack_Starts_From_Its_Base_Packs_Files()
    {
        // Act
        var loader = new PfWolfPk3Loader([BasePk3()], "beta", "beta");

        // Assert: alpha's classes are there under beta's name, beta's own replace them class by class
        var actors = Actors(loader, "beta/actordefs");
        Assert.That(actors.Keys, Is.EquivalentTo(new[] { "Guard", "Dog" }));
        Assert.That(actors["Guard"].Radius, Is.EqualTo(20));
        Assert.That(actors["Guard"].Properties.ContainsKey("speed"), Is.False);
        Assert.That(actors["Dog"].Radius, Is.EqualTo(11));
        Assert.Throws<KeyNotFoundException>(() => loader.Load<ActorTranslationAsset>("alpha/actordefs"));
    }

    [Test]
    public void A_Packs_Language_Merges_Over_Its_Base_Packs()
    {
        // Act
        var loader = new PfWolfPk3Loader([BasePk3()], "beta", "beta");

        // Assert
        var strings = Strings(loader, "beta/language/en-us");
        Assert.That(strings["HELLO"], Is.EqualTo("beta hello"));
        Assert.That(strings["BYE"], Is.EqualTo("alpha bye"));
    }

    [Test]
    public void No_Running_Pack_Loads_Every_Pack()
    {
        // Act
        var loader = new PfWolfPk3Loader([BasePk3()], null, "alpha");

        // Assert
        Assert.That(Actors(loader, "alpha/actordefs").Keys, Does.Contain("Dog"));
        Assert.That(Actors(loader, "beta/actordefs").Keys, Is.EqualTo(new[] { "Guard" }));
        Assert.That(Actors(loader, "gamma/actordefs").Keys, Is.EqualTo(new[] { "Robot" }));
    }

    [Test]
    public void ReadBasePackIds_Follows_The_Chain()
    {
        // Act / Assert
        Assert.That(PfWolfPk3Loader.ReadBasePackIds(BasePk3(), "beta"), Is.EqualTo(new[] { "alpha" }));
        Assert.That(PfWolfPk3Loader.ReadBasePackIds(BasePk3(), "alpha"), Is.Empty);
        Assert.That(PfWolfPk3Loader.ReadBasePackIds(BasePk3(), "unknown"), Is.Empty);
        Assert.That(PfWolfPk3Loader.ReadBasePackIds(new MemoryAssetSource("empty", []), "beta"), Is.Empty);
    }

    [Test]
    public void A_Mods_Actordefs_Merge_Deep_Into_The_Running_Packs()
    {
        // Arrange: the mod only changes the guard's speed
        var mod = new MemoryAssetSource("mymod.pk3", new()
        {
            ["actordefs/tweaks.yaml"] = "Guard:\n  properties:\n    speed: 1024\n",
        });

        // Act
        var loader = new PfWolfPk3Loader([BasePk3()], "alpha", "alpha", [mod]);

        // Assert
        var guard = Actors(loader, "alpha/actordefs")["Guard"];
        Assert.That(guard.Properties["speed"].ToString(), Is.EqualTo("1024"));
        Assert.That(guard.Properties["hp"].ToString(), Is.EqualTo("25"));
        Assert.That(guard.Radius, Is.EqualTo(10));
        Assert.That(loader.Warnings, Is.Empty);
    }

    [Test]
    public void A_Mods_Language_Changes_Only_The_Strings_It_Gives()
    {
        // Arrange
        var mod = new MemoryAssetSource("mymod", new() { ["language/en-us.yaml"] = "BYE: mod bye\n" });

        // Act
        var loader = new PfWolfPk3Loader([BasePk3()], "beta", "beta", [mod]);

        // Assert
        var strings = Strings(loader, "beta/language/en-us");
        Assert.That(strings["BYE"], Is.EqualTo("mod bye"));
        Assert.That(strings["HELLO"], Is.EqualTo("beta hello"));
    }

    [Test]
    public void A_Mods_Unreadable_File_Is_Left_Out_With_A_Warning()
    {
        // Arrange
        var mod = new MemoryAssetSource("broken.pk3", new() { ["actordefs/bad.yaml"] = "Guard: [unclosed\n" });

        // Act
        var loader = new PfWolfPk3Loader([BasePk3()], "alpha", "alpha", [mod]);

        // Assert
        Assert.That(loader.Warnings, Has.Count.EqualTo(1));
        Assert.That(loader.Warnings[0], Does.Contain("broken.pk3").And.Contain("actordefs/bad.yaml"));
        Assert.That(Actors(loader, "alpha/actordefs")["Guard"].Radius, Is.EqualTo(10));
    }

    [Test]
    public void A_Mods_Folders_It_Cannot_Use_Are_Warned_About_Once_Each()
    {
        // Arrange
        var mod = new MemoryAssetSource("odd.pk3", new()
        {
            ["actordefs/sub/a.yaml"] = "A: {}\n",
            ["actordefs/sub/b.yaml"] = "B: {}\n",
            ["menudefs/main-menu.yaml"] = "x: 1\n",
            ["gamepacks/alpha/game-info.yaml"] = "x: 1\n",
            ["readme.txt"] = "not an asset",
        });

        // Act
        var loader = new PfWolfPk3Loader([BasePk3()], "alpha", "alpha", [mod]);

        // Assert
        Assert.That(loader.Warnings, Has.Count.EqualTo(3));
        Assert.That(loader.Warnings, Has.Exactly(1).Contains("actordefs/sub/"));
        Assert.That(loader.Warnings, Has.Exactly(1).Contains("menudefs/"));
        Assert.That(loader.Warnings, Has.Exactly(1).Contains("gamepacks/"));
    }

    [Test]
    public void Mods_Need_A_Running_Pack()
    {
        // Arrange
        var mod = new MemoryAssetSource("mymod", new() { ["actordefs/x.yaml"] = "X: {}\n" });

        // Act
        var loader = new PfWolfPk3Loader([BasePk3()], null, "alpha", [mod]);

        // Assert
        Assert.That(loader.Warnings, Has.Count.EqualTo(1));
    }

    [Test]
    public void Origins_Record_Each_Step()
    {
        // Arrange
        var mod = new MemoryAssetSource("mymod.pk3", new() { ["actordefs/x.yaml"] = "Guard:\n  radius: 99\n" });

        // Act
        var loader = new PfWolfPk3Loader([BasePk3()], "beta", "beta", [mod]);

        // Assert
        var origins = loader.GetAssetOrigins()["actortranslationasset:beta/actordefs"];
        Assert.That(origins.Select(o => (o.Source, o.Path, o.Action)), Is.EqualTo(new[]
        {
            ("pfwolf.pk3", "actordefs/alpha/guards.yaml", "added"),
            ("pfwolf.pk3", "actordefs/beta/guards.yaml", "merged"),
            ("mymod.pk3", "actordefs/x.yaml", "merged"),
        }));
    }

    [Test]
    public void Texts_Load_By_File_Name_And_A_Mods_Replace_Them()
    {
        // Arrange: the base pk3 adds a text, a mod replaces it and adds another, with a byte order mark
        var basePk3 = new MemoryAssetSource("pfwolf.pk3", new()
        {
            ["gamepacks/gamepack-info.yaml"] = GamePackInfo,
            ["texts/ENDART1.txt"] = "^P\nbase\n^E\n",
        });
        var mod = new MemoryAssetSource("mymod.pk3", new() { ["texts/endart1.txt"] = "^P\nmod\n^E\n" },
            new() { ["texts/MYSTORY.txt"] = [0xEF, 0xBB, 0xBF, .. "^P\nnew\n^E\n"u8.ToArray()] });

        // Act
        var loader = new PfWolfPk3Loader([basePk3], "alpha", "alpha", [mod]);

        // Assert
        Assert.That(loader.Load<TextAsset>("ENDART1").ToText(), Is.EqualTo("^P\nmod\n^E\n"));
        Assert.That(loader.Load<TextAsset>("mystory").ToText(), Is.EqualTo("^P\nnew\n^E\n"));
        Assert.That(loader.GetAssetOrigins()["textasset:endart1"].Select(o => (o.Source, o.Action)), Is.EqualTo(new[]
        {
            ("pfwolf.pk3", "added"),
            ("mymod.pk3", "replaced"),
        }));
        Assert.That(loader.Warnings, Is.Empty);
    }

    [TestCase("actordefs/wolf3d/guards.yaml", "wolf3d/actordefs")]
    [TestCase("actordefs/native.yaml", "actordefs")]
    [TestCase("mapdefs/spear/things.yaml", "spear/mapdefs")]
    [TestCase("actordefs/a/b/c.yaml", "a/b/actordefs")]
    public void GetPackUniqueAssetName_Puts_The_Pack_First(string path, string expected)
    {
        Assert.That(PfWolfPk3Loader.GetPackUniqueAssetName(path), Is.EqualTo(expected));
    }
}
