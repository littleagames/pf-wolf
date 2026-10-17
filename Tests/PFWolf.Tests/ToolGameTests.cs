using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;

namespace PFWolf.Tests;

/// <summary>
/// The editor's tools on Wolf3D's own levels and mapdefs. Needs a built game folder (pfwolf.pk3
/// and the data files).
/// </summary>
[Category("GameData")]
public class ToolGameTests
{
    private static GameContent? _content;

    private static GameContent Content()
    {
        if (_content != null)
            return _content;
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        var folder = new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");

        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            return _content = GameContent.Load(folder!, "wolf3d", []);
        }
        catch (PFWolf.Exceptions.DataFilesException)
        {
            Assert.Ignore($"Wolf3D's data files aren't in {folder}");
            throw;
        }
        finally
        {
            Directory.SetCurrentDirectory(workingFolder);
        }
    }

    /// <summary>A level and the tools set up as the editor sets them for the walls layer</summary>
    private static (MapDocument Document, ToolController Tools) Open(string level)
    {
        var content = Content();
        var document = new MapDocument(level, content.FindMap(level)!);
        var (combine, read) = PaletteEntries.TileLayer(content.MapDefs);
        var tools = new ToolController
        {
            Document = document,
            Combine = combine,
            Read = read,
            EraseValue = p => PaletteEntries.EraseValue(content.MapDefs, p),
            MakeRemap = transform => new TileRemap(content.MapDefs, transform, content.IsWallSprite),
        };
        return (document, tools);
    }

    [Test]
    public void The_Magic_Wand_Selects_A_Run_Of_Walls()
    {
        // Arrange: E1M2's blue wall at (20, 54)
        var (document, tools) = Open("map02");
        tools.Tool = EditTool.MagicWand;
        var code = document[0, 20, 54] & 0xff;

        // Act
        tools.Press(20, 54);
        tools.Release();

        // Assert
        Assert.That(tools.Selection, Is.Not.Null);
        Assert.That(tools.Selection!.Contains(20, 54), Is.True);
        Assert.That(tools.Selection.Tiles.All(tile => (document[0, tile.X, tile.Y] & 0xff) == code), Is.True);
    }

    [Test]
    public void Every_Wolf3D_Guard_Door_And_Start_Turns_A_Quarter_Turn()
    {
        // Arrange
        var defs = Content().MapDefs;
        var remap = new TileRemap(defs, TileTransform.RotateClockwise, Content().IsWallSprite);

        // Act: the rotating enemies, doors and player starts
        var unmatched = defs.Things.Where(thing => Content().IsRotating(thing.Value.Class))
            .Select(thing => (Plane: 1, Value: (ushort)thing.Key))
            .Concat(defs.Doors.Select(door => (Plane: 0, Value: (ushort)door.Key)))
            .Concat(defs.PlayerStarts.Select(start => (Plane: 1, Value: (ushort)start.Key)))
            .Where(tile =>
            {
                remap.Remap(tile.Plane, tile.Value, out var outcome);
                return outcome.HasFlag(RemapOutcome.Unmatched);
            })
            .ToList();

        // Assert
        Assert.That(unmatched, Is.Empty);
    }
}
