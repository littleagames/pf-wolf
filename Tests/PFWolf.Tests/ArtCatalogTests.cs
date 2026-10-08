using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Data;

namespace PFWolf.Tests;

public class ArtCatalogTests
{
    private static GameContent Content() => GameContent.FromDefinitions(
        new MapObjectTranslationAsset
        {
            Walls =
            {
                [1] = new() { North = "GSTONEA1", South = "GSTONEA1", East = "GSTONEA2", West = "GSTONEA2" },
                [2] = new() { North = "GSTONEA1", South = "GSTONEA1", East = "BLUEWALL", West = "BLUEWALL" },
            },
            Doors = { [90] = new() { East = "DOOR", West = "DOOR", North = "DOORSIDE", South = "DOORSIDE", Vertical = true } },
            Flats = new MapFlatsTranslation { Floor = { [1] = "FLOOR1" }, Ceiling = { [2] = "CEIL1" } },
            Things =
            {
                [108] = new MapActorTranslation { Class = "Guard", Angles = 0 },
                [109] = new MapActorTranslation { Class = "Guard", Angles = 90 },
            },
        },
        actors: new Dictionary<string, ActorData>
        {
            ["Guard"] = new()
            {
                States =
                {
                    ["Spawn"] = [new ActorStatesData { Sprite = "GARD", Frames = ["A"] }],
                    ["Pain"] = [new ActorStatesData { Sprite = "GARD", Frames = ["H", "I"] }],
                },
            },
            ["Lamp"] = new() { States = { ["Spawn"] = [new ActorStatesData { Sprite = "LAMP", Frames = ["A"] }] } },
        });

    [Test]
    public void A_Texture_Lists_Every_Wall_That_Shows_It_With_Its_Palette_Value()
    {
        // Act
        var uses = ArtCatalog.Uses(Content());

        // Assert
        Assert.That(uses["GSTONEA1"].Select(use => use.Label), Is.EqualTo(new[] { "Wall 1, north and south", "Wall 2, north and south" }));
        Assert.That(uses["gstonea2"].Single().Target, Is.EqualTo(new PaletteTarget(0, 1)));
        Assert.That(uses["DOOR"].Single().Label, Is.EqualTo("Door 90, east and west"));
    }

    [Test]
    public void A_Flat_Keeps_The_Other_Half_Of_The_Flats_Value()
    {
        // Act
        var uses = ArtCatalog.Uses(Content());

        // Assert: floor flats are the low byte, ceilings the high one
        Assert.That(uses["FLOOR1"].Single().Target, Is.EqualTo(new PaletteTarget(MapConstants.FLATPLANE, 1, Keep: 0xff00)));
        Assert.That(uses["CEIL1"].Single().Target, Is.EqualTo(new PaletteTarget(MapConstants.FLATPLANE, 2 << 8, Keep: 0x00ff)));
    }

    [Test]
    public void A_Sprite_Frame_Lists_The_Classes_Whose_States_Show_It()
    {
        // Act
        var uses = ArtCatalog.SpriteUses(Content());

        // Assert: the guard is placed by its first object code; the lamp isn't in mapdefs
        Assert.That(uses["GARDA"].Single().Target, Is.EqualTo(new PaletteTarget(1, 108)));
        Assert.That(uses.ContainsKey("GARDI"), Is.True);
        Assert.That(uses["LAMPA"].Single().Target, Is.Null);
    }

    [TestCase("flats/FLOOR1.png", true)]
    [TestCase("textures/FLOOR1.png", false)]
    [TestCase("flats.png", false)]
    public void Flats_Come_From_A_Flats_Folder(string path, bool flat)
        => Assert.That(ArtCatalog.IsFlatPath(path), Is.EqualTo(flat));

    [TestCase("GARDA1", "GARDA")]
    [TestCase("lampa0", "LAMPA")]
    [TestCase("TITLEPIC", null)]
    public void A_Sprites_Frame_Is_Its_Name_Without_The_Rotation(string sprite, string? frame)
        => Assert.That(ArtCatalog.SpriteFrame(sprite), Is.EqualTo(frame));
}
