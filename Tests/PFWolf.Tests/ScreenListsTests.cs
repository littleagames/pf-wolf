using Row = PFWolf.Ui.CustomizeControlsScreen.CustomRow;
using Kind = PFWolf.Ui.CustomizeControlsScreen.CustomRowKind;

namespace PFWolf.Tests;

public class CustomizeControlsTests
{
    // Header, two controls, a blank, a header, a control, and the reset row
    private static readonly List<Row> Rows =
    [
        new(Kind.Header, "Movement"),
        new(Kind.Control, "Forward"),
        new(Kind.Control, "Back"),
        new(Kind.Blank, ""),
        new(Kind.Header, "Weapons"),
        new(Kind.Control, "Fire"),
        new(Kind.Reset, "Reset to defaults"),
    ];

    [TestCase(-1, 1, 1)]        // the first that can be picked
    [TestCase(1, 1, 2)]
    [TestCase(2, 1, 5)]         // over the blank and the header
    [TestCase(5, -1, 2)]
    [TestCase(6, 1, 1)]         // round from the reset row to the top
    [TestCase(1, -1, 6)]        // and back
    public void NextCustomRow_Skips_What_Cant_Be_Picked(int from, int step, int expected)
    {
        Assert.That(CustomizeControlsScreen.NextCustomRow(Rows, from, step), Is.EqualTo(expected));
    }

    [TestCase(6, 1, 6)]         // a page down from the last stays
    [TestCase(1, -1, 1)]
    public void Without_Wrapping_It_Stops_At_The_Ends(int from, int step, int expected)
    {
        Assert.That(CustomizeControlsScreen.NextCustomRow(Rows, from, step, wrap: false), Is.EqualTo(expected));
    }

    [Test]
    public void With_Nothing_To_Pick_There_Is_No_Row()
    {
        Assert.That(CustomizeControlsScreen.NextCustomRow([new(Kind.Header, "Only a header")], -1, 1), Is.EqualTo(-1));
    }

    [Test]
    public void Rows_Are_Placed_From_The_Top_Of_The_Scroll()
    {
        Assert.That(CustomizeControlsScreen.CustomRowY(5, 3) - CustomizeControlsScreen.CustomRowY(3, 3), Is.EqualTo(22));
    }
}

public class LoadSaveListTests
{
    private static SaveInfo Save(string name, string gamePack = "wolf3d") => new() { Name = name, GamePack = gamePack, Path = name + ".dat" };

    [Test]
    public void Names_Sort_As_A_Person_Would()
    {
        // Arrange
        List<string> names = ["test 10", "Test 2", "boss", "test 2b", "test 02", "Alpha"];

        // Act
        names.Sort(LoadSaveScreen.NaturalNameComparer.Instance);

        // Assert
        Assert.That(names, Is.EqualTo(new[] { "Alpha", "boss", "Test 2", "test 02", "test 2b", "test 10" }));
    }

    [Test]
    public void Only_This_Game_Packs_Saves_Unless_All_Are_Asked_For()
    {
        // Arrange
        List<SaveInfo> saves = [Save("a"), Save("b", "spear"), Save("c")];

        // Act / Assert
        Assert.That(LoadSaveScreen.SaveRows(saves, saving: false, allGames: false, byName: false, "wolf3d").Select(s => s!.Name),
            Is.EqualTo(new[] { "a", "c" }));
        Assert.That(LoadSaveScreen.SaveRows(saves, saving: false, allGames: true, byName: false, "WOLF3D"), Has.Count.EqualTo(3));
    }

    [Test]
    public void By_Name_Keeps_Newest_First_Within_A_Name()
    {
        // Arrange: newest first, as SaveFile.List gives them
        var newer = Save("same") with { Path = "newer.dat" };
        var older = Save("same") with { Path = "older.dat" };
        List<SaveInfo> saves = [Save("zed"), newer, older];

        // Act
        var rows = LoadSaveScreen.SaveRows(saves, saving: false, allGames: true, byName: true, "wolf3d");

        // Assert
        Assert.That(rows.Select(s => s!.Path), Is.EqualTo(new[] { "newer.dat", "older.dat", "zed.dat" }));
    }

    [Test]
    public void The_Save_Screen_Starts_With_A_New_Save_Row()
    {
        // Act
        var rows = LoadSaveScreen.SaveRows([Save("a")], saving: true, allGames: false, byName: false, "wolf3d");

        // Assert
        Assert.That(rows[0], Is.Null);
        Assert.That(rows[1]!.Name, Is.EqualTo("a"));
    }
}
