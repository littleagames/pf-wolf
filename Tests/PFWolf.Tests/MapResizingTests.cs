using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Editing;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Tests;

public class MapResizingTests
{
    [TestCase("64x64", 64, 64)]
    [TestCase(" 100 X 70 ", 100, 70)]
    [TestCase("128", 128, 128)]
    [TestCase("90,40", 90, 40)]
    public void Sizes_Are_Read_As_Width_By_Height(string text, int width, int height)
    {
        Assert.That(MapResizing.ParseSize(text), Is.EqualTo((width, height)));
    }

    [TestCase("")]
    [TestCase("big")]
    [TestCase("64x")]
    public void Text_That_Isnt_A_Size_Isnt_Read(string text)
    {
        Assert.That(MapResizing.ParseSize(text), Is.Null);
        Assert.That(MapResizing.CheckSize(text), Is.Not.Null);
    }

    [TestCase("2x64", "small")]
    [TestCase("64x300", "big")]
    public void Sizes_Past_The_Limits_Are_Refused(string text, string why)
    {
        Assert.That(MapResizing.CheckSize(text), Does.Contain(why));
    }

    [TestCase("top-left", MapAnchor.TopLeft)]
    [TestCase("Center", MapAnchor.Center)]
    [TestCase("middle", MapAnchor.Center)]
    [TestCase("bottom right", MapAnchor.BottomRight)]
    [TestCase("nowhere", null)]
    public void Anchors_Are_Read_By_Name(string text, MapAnchor? anchor)
    {
        Assert.That(MapResizing.ParseAnchor(text), Is.EqualTo(anchor));
    }

    [TestCase(MapAnchor.TopLeft, 0, 0)]
    [TestCase(MapAnchor.Center, 18, 3)]
    [TestCase(MapAnchor.BottomRight, 36, 6)]
    [TestCase(MapAnchor.Right, 36, 3)]
    public void The_Old_Level_Goes_Where_The_Anchor_Keeps_It(MapAnchor anchor, int x, int y)
    {
        Assert.That(MapResizing.Offset(64, 64, 100, 70, anchor), Is.EqualTo((x, y)));
    }

    [Test]
    public void Growing_A_Level_Keeps_Its_Tiles_And_Walls_The_New_Ones()
    {
        // Arrange: a 64x64 level with a thing at 10,10
        var document = EditorMaps.Document();
        using (var edit = document.BeginEdit("thing"))
            edit.Set(1, 10, 10, 23);

        // Act
        var planes = MapResizing.Resized(document.Map, 100, 70, MapAnchor.BottomRight, fill: 1);

        // Assert: moved 36 right and 6 down; the new strip is wall on plane 0, nothing elsewhere
        Assert.That(planes[0], Has.Length.EqualTo(100 * 70));
        Assert.That(planes[1][(10 + 6) * 100 + 10 + 36], Is.EqualTo(23));
        Assert.That(planes[0][0], Is.EqualTo(1));
        Assert.That(planes[1][0], Is.Zero);
        Assert.That(planes[0][69 * 100 + 99], Is.EqualTo(document[0, 63, 63]));
    }

    [Test]
    public void Shrinking_A_Level_Cuts_Off_What_No_Longer_Fits()
    {
        // Arrange
        var document = EditorMaps.Document();
        using (var edit = document.BeginEdit("thing"))
            edit.Set(1, 40, 40, 23);

        // Act: a 32x32 piece out of the middle (16 to 47 each way)
        var planes = MapResizing.Resized(document.Map, 32, 32, MapAnchor.Center, fill: 1);

        // Assert
        Assert.That(planes[1], Has.Length.EqualTo(32 * 32));
        Assert.That(planes[1][(40 - 16) * 32 + 40 - 16], Is.EqualTo(23));
    }

    [Test]
    public void A_Resize_Is_One_Undo_Step()
    {
        // Arrange
        var document = EditorMaps.Document();
        var before = document.Map.MapData;

        // Act
        document.Resize("Resize", 100, 70, MapResizing.Resized(document.Map, 100, 70, MapAnchor.TopLeft, fill: 1));

        // Assert
        Assert.That((document.Width, document.Height), Is.EqualTo((100, 70)));
        Assert.That(document.Contains(99, 69), Is.True);
        Assert.That(document.IsDirty, Is.True);

        document.Undo();
        Assert.That((document.Width, document.Height), Is.EqualTo((MapConstants.ClassicMapSize, MapConstants.ClassicMapSize)));
        Assert.That(document.Map.MapData, Is.SameAs(before));

        document.Redo();
        Assert.That((document.Width, document.Height), Is.EqualTo((100, 70)));
        Assert.That(document.Map.MapData[0], Has.Length.EqualTo(100 * 70));
    }

    [Test]
    public void A_New_Level_Can_Be_Any_Size()
    {
        // Act
        var map = MapFiles.NewMap("WIDE", wall: 1, floor: 107, width: 90, height: 40);

        // Assert
        Assert.That((map.Width, map.Height), Is.EqualTo(((ushort)90, (ushort)40)));
        Assert.That(map.MapData[0][39 * 90 + 89], Is.EqualTo(1));
        Assert.That(map.MapData[0][20 * 90 + 45], Is.EqualTo(107));
        Assert.That(map.MapData[5], Has.Length.EqualTo(90 * 40));
    }

    [Test]
    public void The_Size_Fields_Start_On_The_Level_And_Keep_The_Middle()
    {
        // Act
        var size = new MapSizeViewModel(64, 64);

        // Assert
        Assert.That(size.IsChanged, Is.False);
        Assert.That(size.Anchor, Is.EqualTo(MapAnchor.Center));
        Assert.That(size.Build(), Is.EqualTo((64, 64)));
    }

    [Test]
    public void The_Size_Fields_Take_A_Non_Square_Size_And_An_Anchor()
    {
        // Arrange
        var size = new MapSizeViewModel(64, 64);

        // Act
        size.Width = 128;
        size.Height = 40;
        size.AnchorIndex = (int)MapAnchor.TopRight;

        // Assert
        Assert.That(size.IsChanged, Is.True);
        Assert.That(size.Build(), Is.EqualTo((128, 40)));
        Assert.That(size.Anchor, Is.EqualTo(MapAnchor.TopRight));
        Assert.That(size.Summary, Does.Contain("128x40").And.Contain("top-right"));
    }

    [Test]
    public void The_Properties_Dialog_Refuses_A_Size_Past_The_Limits()
    {
        // Arrange
        var size = new MapSizeViewModel(64, 64) { Width = 300 };
        var dialog = new MapPropertiesViewModel("MAP01", new MapProperties(), new DefaultMapInfo(), [], [], size: size);

        // Act
        var properties = dialog.Build();

        // Assert
        Assert.That(properties, Is.Null);
        Assert.That(dialog.Error, Does.Contain("256"));
    }
}
