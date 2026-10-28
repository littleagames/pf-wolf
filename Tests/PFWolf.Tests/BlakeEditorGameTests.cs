using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;

namespace PFWolf.Tests;

/// <summary>
/// The editor reading Blake Stone's levels: map-info codes, the links its object-plane values
/// make, and every thing showing. Needs a built game folder with the Blake Stone data files.
/// </summary>
[Category("GameData")]
public class BlakeEditorGameTests
{
    private static readonly Dictionary<string, GameContent> Loaded = [];

    private static GameContent Load(string game)
    {
        if (Loaded.TryGetValue(game, out var cached))
            return cached;

        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        var folder = new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");

        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            return Loaded[game] = GameContent.Load(folder!, game, []);
        }
        catch (PFWolf.Exceptions.DataFilesException)
        {
            Assert.Ignore($"{game}'s data files aren't in {folder}");
            throw;
        }
        finally
        {
            Directory.SetCurrentDirectory(workingFolder);
        }
    }

    private static MapTiles Tiles(GameContent content, string name)
        => new(content, content.FindMap(name)!, () => MapProperties.From(content.MapInfoOf(name)));

    [Test]
    public void An_Aog_Barrier_Switch_Links_To_Its_Barrier()
    {
        // Arrange: floor 1's switches at 15,25 and 16,28 work the arc barrier at 11,28 (0xF8FF + its x,y)
        var tiles = Tiles(Load("blake"), "MAP02");

        // Act
        var link = tiles.LinkFrom(15, 25);

        // Assert
        Assert.That(link, Is.Not.Null);
        Assert.That(link!.Kind, Is.EqualTo(ObjectLinkKind.TagLink));
        Assert.That((link.ToX, link.ToY), Is.EqualTo((11, 28)));
        Assert.That(link.OnThisFloor);
        Assert.That(tiles.Thing(11, 28)?.Class, Is.EqualTo("ArcBarrier"));
        Assert.That(tiles.IsValueTile(16, 25), "the tile after the code holds the barrier's x,y");
        Assert.That(tiles.Thing(16, 25), Is.Null);
    }

    [Test]
    public void A_Planet_Strike_Barrier_Switch_Links_By_Its_Own_Value()
    {
        // Arrange
        var tiles = Tiles(Load("planetstrike"), "MAP03");

        // Act
        var link = tiles.LinkFrom(60, 25);

        // Assert
        Assert.That(link?.Kind, Is.EqualTo(ObjectLinkKind.SwitchLink));
        Assert.That((link!.ToX, link.ToY), Is.EqualTo((31, 52)));
    }

    [Test]
    public void A_Smart_Switch_Floor_Code_Links_To_The_Tile_It_Works()
    {
        // Arrange
        var tiles = Tiles(Load("planetstrike"), "MAP03");

        // Act
        var link = tiles.LinkFrom(50, 30);

        // Assert
        Assert.That(link?.Kind, Is.EqualTo(ObjectLinkKind.SmartSwitch));
        Assert.That((link!.ToX, link.ToY), Is.EqualTo((46, 25)));
        Assert.That(tiles.ObjectKnown(50, 30));
    }

    [Test]
    public void Map_Info_Codes_Say_What_They_Hold()
    {
        // Arrange
        var tiles = Tiles(Load("blake"), "MAP02");

        // Act
        var colors = string.Join(" | ", tiles.Describe(0, 0));
        var flats = string.Join(" | ", tiles.Describe(3, 0));
        var hint = string.Join(" | ", tiles.Describe(0, 5));

        // Assert
        Assert.That(colors, Does.Contain("ceiling color 114").And.Contain("floor color 11"));
        Assert.That(flats, Does.Contain("ceiling flat 10").And.Contain("floor flat 11"));
        Assert.That(hint, Does.Contain("hint 1 of NiceScientistHints").And.Contain("LEAVE ME ALONE"));
    }

    [Test]
    public void The_Level_List_Names_Floors_By_Mission_And_Floor()
    {
        // Arrange / Act
        var maps = Load("blake").Maps;

        // Assert: mission 1 is MAP01 (floor 0) to MAP15, its slots past floor 10 unused
        Assert.That(maps.First(map => map.Name == "MAP01").Label, Is.EqualTo("M1 F0"));
        Assert.That(maps.First(map => map.Name == "MAP03").Label, Is.EqualTo("M1 F2"));
        Assert.That(maps.First(map => map.Name == "MAP03").Unused, Is.False);
        Assert.That(maps.First(map => map.Name == "MAP12").Unused);
        Assert.That(maps.First(map => map.Name == "MAP12").Label, Is.EqualTo("M1 —"));
        Assert.That(maps.First(map => map.Name == "MAP17").Label, Is.EqualTo("M2 F1"));
    }

    [TestCase("blake")]
    [TestCase("blake-shareware")]
    [TestCase("planetstrike")]
    public void Every_Thing_On_The_Levels_Shows(string game)
    {
        // Arrange
        var content = Load(game);
        var broken = new List<string>();

        // Act
        foreach (var entry in content.Maps)
        {
            var tiles = Tiles(content, entry.Name);
            for (int y = 0; y < tiles.Height; y++)
                for (int x = 0; x < tiles.Width; x++)
                    if (tiles.Thing(x, y) is { } thing && content.BrokenThingReason(thing.Class) is { } reason)
                        broken.Add($"{entry.Name} ({x}, {y}) {reason}");
        }

        // Assert
        Assert.That(broken.Distinct(), Is.Empty);
    }

    [Test]
    public void The_Shareware_Has_Every_Sprite_Its_Things_Use()
    {
        // Arrange: its VSWAP has only some of the registered game's sprites, so every frame of
        // what its levels place (and what those spawn, drop or become) must be among them
        var content = Load("blake-shareware");
        var classes = new HashSet<string>();
        var pending = new Queue<string>();
        void Add(string? name)
        {
            if (!string.IsNullOrEmpty(name) && content.Actors.ContainsKey(name) && classes.Add(name))
                pending.Enqueue(name);
        }
        foreach (var entry in content.Maps)
        {
            var tiles = Tiles(content, entry.Name);
            for (int y = 0; y < tiles.Height; y++)
                for (int x = 0; x < tiles.Width; x++)
                    Add(tiles.Thing(x, y)?.Class);
        }

        // Act
        var missing = new SortedSet<string>();
        while (pending.TryDequeue(out var name))
        {
            for (var cls = name; !string.IsNullOrEmpty(cls) && content.Actors.TryGetValue(cls, out var actor); cls = actor.Parent)
            {
                foreach (var value in actor.Properties.Values)
                    foreach (var word in value is IEnumerable<object> many ? many.Select(v => v?.ToString()) : [value?.ToString()])
                        Add(word);
                foreach (var (state, frames) in actor.States)
                    foreach (var frame in frames.OfType<ActorStatesData>())
                    {
                        foreach (var word in (frame.Action ?? "").Split(['(', ')', ',', '"', '\'', ' '], StringSplitOptions.RemoveEmptyEntries))
                            Add(word);
                        if (string.IsNullOrEmpty(frame.Sprite) || frame.Sprite == "TNT1" || frame.Sprite.Contains('#'))
                            continue;
                        foreach (var letter in frame.Frames)
                            if (content.Find<SpriteAsset>($"{frame.Sprite}{letter}0") == null && content.Find<SpriteAsset>($"{frame.Sprite}{letter}1") == null)
                                missing.Add($"{cls}.{state}: {frame.Sprite}{letter}");
                    }
            }
        }

        // Assert
        TestContext.Out.WriteLine($"{classes.Count} classes: {string.Join(", ", classes.Order())}");
        Assert.That(missing, Is.Empty);
    }
}
