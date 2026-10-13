using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>factions.yaml: who's on what side, and how each side treats the others (FactionTable)</summary>
public class FactionTests
{
    private const string GamePackInfo = """
        alpha:
          title: Alpha
          game-palette: pal
        beta:
          title: Beta
          game-palette: pal
          base-pack: alpha
        """;

    private static FactionTable Table(string yaml) =>
        new(YamlDataEntryLoader.Deserialize<FactionsAsset>(yaml));

    [Test]
    public void Without_A_File_Players_And_Monsters_Are_Hostile_Sides()
    {
        // Act
        var table = new FactionTable();

        // Assert
        Assert.That(table.PlayerFaction, Is.EqualTo(FactionTable.DefaultPlayerFaction));
        Assert.That(table.MonsterFaction, Is.EqualTo(FactionTable.DefaultMonsterFaction));
        Assert.That(table.Relation("players", "monsters"), Is.EqualTo(FactionRelation.Hostile));
        Assert.That(table.Relation("monsters", "players"), Is.EqualTo(FactionRelation.Hostile));
        Assert.That(table.Relation("monsters", "monsters"), Is.EqualTo(FactionRelation.Ally));
        Assert.That(table.IsKnown("anything"), Is.True, "with none listed, any name is a side of its own");
        Assert.That(table.Warnings, Is.Empty);
    }

    [Test]
    public void Relations_Are_One_Way_And_Fall_Back_To_The_Factions_Default_Then_The_Files()
    {
        // Arrange
        var table = Table("""
            player-faction: allies
            monster-faction: axis
            default-relation: hostile
            factions:
              allies: {}
              axis:
                relations: { mutants: neutral }
              mutants:
                default: neutral
                relations: { allies: hostile }
              resistance:
                relations: { allies: ally }
            """);

        // Assert
        Assert.That(table.Relation("axis", "mutants"), Is.EqualTo(FactionRelation.Neutral), "axis's own relation");
        Assert.That(table.Relation("mutants", "axis"), Is.EqualTo(FactionRelation.Neutral), "mutants' default");
        Assert.That(table.Relation("mutants", "allies"), Is.EqualTo(FactionRelation.Hostile), "mutants' relation beats its default");
        Assert.That(table.Relation("resistance", "allies"), Is.EqualTo(FactionRelation.Ally));
        Assert.That(table.Relation("allies", "resistance"), Is.EqualTo(FactionRelation.Hostile), "one way: allies don't say so");
        Assert.That(table.Relation("allies", "axis"), Is.EqualTo(FactionRelation.Hostile), "the file's default");
        Assert.That(table.Relation("Axis", "AXIS"), Is.EqualTo(FactionRelation.Ally), "names ignore case");
        Assert.That(table.Warnings, Is.Empty);
    }

    [Test]
    public void No_Faction_Is_No_Side_Which_Everyone_Leaves_Be()
    {
        // Arrange
        var table = new FactionTable();

        // Assert
        Assert.That(table.Relation(null, "monsters"), Is.EqualTo(FactionRelation.Neutral));
        Assert.That(table.Relation("monsters", ""), Is.EqualTo(FactionRelation.Neutral));
    }

    [Test]
    public void Mistakes_In_The_File_Are_Warned_About()
    {
        // Arrange
        var table = Table("""
            player-faction: heroes
            default-relation: grumpy
            factions:
              allies:
                relations: { nobody: hostile, axis: friendly }
              axis: {}
            """);

        // Assert
        Assert.That(table.Warnings, Has.Some.Contains("grumpy"));
        Assert.That(table.Warnings, Has.Some.Contains("\"nobody\""));
        Assert.That(table.Warnings, Has.Some.Contains("friendly"));
        Assert.That(table.Warnings, Has.Some.Contains("player-faction \"heroes\""));
        Assert.That(table.Warnings, Has.Some.Contains("monster-faction \"monsters\""));
        Assert.That(table.Relation("allies", "axis"), Is.EqualTo(FactionRelation.Hostile), "an unreadable relation falls back");
        Assert.That(table.IsKnown("heroes"), Is.False);
    }

    [Test]
    public void A_Faction_Shows_Its_Name_And_Translation()
    {
        // Arrange
        var table = Table("factions:\n  allies: { name: The Allies, translation: blue }\n  axis: {}\n");

        // Assert
        Assert.That(table.DisplayName("allies"), Is.EqualTo("The Allies"));
        Assert.That(table.DisplayName("axis"), Is.EqualTo("axis"));
        Assert.That(table.Translation("allies"), Is.EqualTo("blue"));
        Assert.That(table.Translation("axis"), Is.Null);
    }

    [Test]
    public void A_Packs_Factions_Merge_Over_Its_Base_Packs_And_A_Mods_Over_Both()
    {
        // Arrange
        var pk3 = new MemoryAssetSource("pfwolf.pk3", new()
        {
            ["gamepacks/gamepack-info.yaml"] = GamePackInfo,
            ["gamepacks/alpha/factions.yaml"] = """
                player-faction: allies
                monster-faction: axis
                factions:
                  allies: { name: Allies }
                  axis: { name: Axis }
                """,
            ["gamepacks/beta/factions.yaml"] = "factions:\n  axis:\n    relations: { mutants: neutral }\n  mutants: {}\n",
        });
        var mod = new MemoryAssetSource("mod.pk3", new()
        {
            ["modinfo.yaml"] = "name: Mod\nversion: \"1\"\n",
            ["factions.yaml"] = "factions:\n  allies: { name: Resistance }\n  mutants: !remove\n",
        });

        // Act
        var plain = new PfWolfPk3Loader([pk3], "beta", "beta");
        var modded = new PfWolfPk3Loader([pk3], "beta", "beta", [mod]);

        // Assert: beta keeps alpha's factions and adds its own
        var table = new FactionTable(plain.Load<FactionsAsset>("beta/factions"));
        Assert.That(table.Names, Is.EquivalentTo(new[] { "allies", "axis", "mutants" }));
        Assert.That(table.PlayerFaction, Is.EqualTo("allies"));
        Assert.That(table.DisplayName("axis"), Is.EqualTo("Axis"));
        Assert.That(table.Relation("axis", "mutants"), Is.EqualTo(FactionRelation.Neutral));

        // ...and the mod renames one and takes one out
        var moddedTable = new FactionTable(modded.Load<FactionsAsset>("beta/factions"));
        Assert.That(moddedTable.Names, Is.EquivalentTo(new[] { "allies", "axis" }));
        Assert.That(moddedTable.DisplayName("allies"), Is.EqualTo("Resistance"));
        Assert.That(modded.Warnings, Is.Empty);
    }
}
