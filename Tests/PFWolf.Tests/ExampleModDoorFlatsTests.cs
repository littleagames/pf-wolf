using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>
/// The example mods' binary maps (examples/mods/*/maps/*.wad, made by examples/tools): a doorway
/// next to a tile with flats has flats of its own. A door tile left at 0 shows the bare floor
/// color and the sky through its frame, between rooms that are textured and roofed.
/// </summary>
public class ExampleModDoorFlatsTests
{
    // The stock doors' plane-0 codes: an even one is passed east-west, an odd one north-south
    private const int FirstDoor = 90, LastDoor = 101;

    private static IEnumerable<string> ExampleMaps() =>
        Directory.GetDirectories(Path.Combine(TestPaths.RepoRoot(), "examples", "mods"))
            .Where(mod => Directory.Exists(Path.Combine(mod, "maps")))
            .SelectMany(mod => Directory.GetFiles(Path.Combine(mod, "maps"), "*.wad"))
            .Order(StringComparer.Ordinal);

    [Test]
    public void Every_Doorway_Beside_Flats_Has_Flats()
    {
        // Arrange
        var problems = new List<string>();

        foreach (var file in ExampleMaps())
        {
            var map = EcWolfMapLoader.Load(File.ReadAllBytes(file));
            if (map.MapData == null)
                continue;                   // UWMF: its flats are sectors, not plane 2
            int width = map.Width, height = map.Height;
            var walls = map.MapData[0];
            var flats = map.MapData[2];

            // Act
            for (int y = 1; y < height - 1; y++)
                for (int x = 1; x < width - 1; x++)
                {
                    int tile = walls[y * width + x];
                    if (tile < FirstDoor || tile > LastDoor || flats[y * width + x] != 0)
                        continue;
                    bool eastWest = tile % 2 == 0;
                    int before = eastWest ? y * width + x - 1 : (y - 1) * width + x;
                    int after = eastWest ? y * width + x + 1 : (y + 1) * width + x;
                    if (flats[before] != 0 || flats[after] != 0)
                        problems.Add($"{Path.GetRelativePath(TestPaths.RepoRoot(), file)}: door {tile} at ({x},{y}) has no flats, beside 0x{flats[before]:X4} / 0x{flats[after]:X4}");
                }
        }

        // Assert
        Assert.That(problems, Is.Empty);
    }
}
