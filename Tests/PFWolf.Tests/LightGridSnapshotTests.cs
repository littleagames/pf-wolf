using System.Security.Cryptography;
using System.Text;
using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

/// <summary>
/// The light grid as the game's actor lights leave it, on hand-made levels: open floor, walls,
/// shut and open doors, diagonals, corners, coloured lights overlapping, set lights bright and
/// dark, a zone, state lights, overrides and effects part way. Kept so moving the light grid and
/// casting into PFWolf.Common (docs/dev/halo-lights-in-common-plan.md phase 0) can be checked to
/// change nothing: set PFWOLF_LIGHT_SNAPSHOT to a file path and the snapshot is written there,
/// to compare before and after.
/// </summary>
public class LightGridSnapshotTests
{
    private const int Tile = 1 << MapConstants.TileShift;

    private MapManager _map = null!;
    private Doors _doors = null!;
    private Lighting _lighting = null!;
    private ActorLights _lights = null!;

    private void MakeLevel()
    {
        var session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, session, null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        typeof(MapManager).GetField("mapsegs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(_map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        _doors = new Doors(null!, null!, _map, null!, null!, session, null!);
        ActorLights? lights = null;
        _lighting = new Lighting(null!, LightingTests.GreyVideo(), _map,
            new LightingHooks(() => 0, () => lights?.ColorTintTables.Clear(), tics => lights?.TickActorLights(tics)));
        _lights = lights = new ActorLights(_map, _doors, _lighting, session);
    }

    private Monster Lamp(double x, double y, int intensity, double radius, string? color = null, string? mode = null, string? effect = null)
    {
        var lamp = new Monster
        {
            Name = $"Lamp{intensity}-{radius}-{color}-{mode}-{effect}",
            X = (int)(x * Tile), Y = (int)(y * Tile), TileX = (byte)x, TileY = (byte)y,
        };
        lamp.Properties["light.intensity"] = intensity;
        lamp.Properties["light.radius"] = radius;
        if (color != null)
            lamp.Properties["light.color"] = color;
        if (mode != null)
            lamp.Properties["light.mode"] = mode;
        if (effect != null)
        {
            lamp.Properties["light.effect"] = effect;
            lamp.Properties["light.tics"] = 20;
        }
        _map.GetActors().AddLast(lamp);
        return lamp;
    }

    private void Door(int x, int y, int number, DoorAction action)
    {
        _map.TileMap[x, y] = (ushort)(GameConstants.BitDoor | number);
        _doors.DoorObjList[number] = new DoorObject { TileX = (byte)x, TileY = (byte)y, Action = action };
    }

    // The scenes: each builds a level, lights it and says what to sample
    private static readonly (string Name, Action<LightGridSnapshotTests> Build)[] Scenes =
    [
        ("open floor, dim", t =>
        {
            t._lighting.SetShading(new ShadingSettings("#000000", 0, 16, 0, 64));
            t.Lamp(10.5, 10.5, 255, 3);
            t.Lamp(20.25, 30.75, 128, 2.5);
            t.Lamp(40.5, 40.5, 40, 6);
        }),
        ("walls, doors, a corner", t =>
        {
            t._lighting.SetShading(new ShadingSettings("#000000", 0, 16, 50, 80));
            for (int y = 5; y < 20; y++)
                t._map.TileMap[12, y] = 1;
            for (int x = 12; x < 20; x++)
                t._map.TileMap[x, 14] = 1;
            t.Door(12, 9, 0, DoorAction.Closed);
            t.Door(12, 11, 1, DoorAction.Open);
            t.Door(15, 14, 2, DoorAction.Opening);
            t.Lamp(10.5, 10.5, 220, 5, "#FFB060");
            t.Lamp(14.5, 12.5, 180, 4);
        }),
        ("diagonals", t =>
        {
            t._lighting.SetShading(new ShadingSettings("#000000", 0, 16, 0, 40));
            foreach (var (x, y, shape) in new[] { (11, 10, WallShape.SolidNE), (9, 10, WallShape.SolidSW), (10, 12, WallShape.SolidNW), (10, 8, WallShape.SolidSE) })
            {
                t._map.TileMap[x, y] = 1;
                t._map.WallShapes[x, y] = shape;
            }
            t.Lamp(10.5, 10.5, 255, 4, "#4060FF");
        }),
        ("coloured lights overlapping", t =>
        {
            t._lighting.SetShading(new ShadingSettings("#000000", 0, 16, 0, 30));
            t.Lamp(10.5, 10.5, 200, 4, "#FF0000");
            t.Lamp(12.5, 10.5, 150, 4, "#00FF00");
            t.Lamp(11.5, 12, 255, 2, "#0000FF");
        }),
        ("set lights at full light", t =>
        {
            t._lighting.SetShading(new ShadingSettings("#000000", 0, 16, 0, 255));
            t.Lamp(10.5, 10.5, 64, 3, mode: "set");
            t.Lamp(13.5, 10.5, 0, 3, mode: "set");
            t._lighting.UpdateLightGrid();
        }),
        ("set and added lights, dim, with a zone", t =>
        {
            t._lighting.SetShading(new ShadingSettings("#202020", 1, 10, 80, 90));
            for (int x = 20; x < 26; x++)
                for (int y = 20; y < 26; y++)
                    t._map.SetZone(x, y, 1);
            t._lighting.FadeZoneLight(1, 30, 0);
            t._lighting.SetZoneColor(1, "#FF4040");
            t._lighting.UpdateLightGrid();
            t.Lamp(22.5, 22.5, 255, 3, mode: "set", color: "#FFFFC0");
            t.Lamp(24.5, 23.5, 200, 3);
            t.Lamp(19.5, 22.5, 160, 2);
        }),
        ("state lights, overrides, effects", t =>
        {
            t._lighting.SetShading(new ShadingSettings("#000000", 0, 16, 0, 50));
            var flash = t.Lamp(10.5, 10.5, 100, 2);
            flash.CurrentState = new ActorStateFrame { StateName = "Fire", Sprite = "PISG", FrameLetter = "B", Light = StateLight.Parse("{ intensity: 200, radius: 3, color: \"#FFD890\" }") };
            var off = t.Lamp(20.5, 10.5, 150, 2);
            off.CurrentState = new ActorStateFrame { StateName = "Off", Sprite = "LAMP", FrameLetter = "B", Light = StateLight.Parse("none") };
            var green = t.Lamp(30.5, 10.5, 90, 2);
            t._lights.SetLightAction(green, ["170", "3.5", "#00FF00"]);
            var dark = t.Lamp(40.5, 10.5, 200, 2);
            t._lights.LightOff(dark);
            var pulse = t.Lamp(10.5, 30.5, 220, 3, effect: "pulse");
            t._lights.LightStateOf(pulse).Phase = 7;
            var strobe = t.Lamp(20.5, 30.5, 220, 3, effect: "strobe");
            t._lights.LightStateOf(strobe).Phase = 12;
            var flicker = t.Lamp(30.5, 30.5, 220, 3, effect: "flicker");
            (t._lights.LightStateOf(flicker).Phase, t._lights.LightStateOf(flicker).FlickerAmount) = (3, 180);
        }),
    ];

    [Test]
    public void Every_Scenes_Light_Grid_Is_Written()
    {
        var snapshot = new StringBuilder();
        foreach (var (name, build) in Scenes)
        {
            // Arrange
            MakeLevel();
            build(this);

            // Act: cast twice, as two frames do (the first frame's cells cleared by the second)
            _lights.CastActorLights();
            _lights.CastActorLights();

            // Assert / write: the grid, and the light rows LightAt gives across the lit area
            var rows = new StringBuilder();
            for (int gy = 0; gy < 48 * Tile; gy += Tile / 8)
                for (int gx = 0; gx < 48 * Tile; gx += Tile / 8)
                {
                    int tile = _map.TileIndexOrNone(gx / Tile, gy / Tile);
                    var row = _lighting.LightAt(tile, gx, gy);
                    rows.Append(Convert.ToHexString(SHA256.HashData(row))[..6]).Append(',');
                }
            snapshot.AppendLine($"{name}: grid {_lighting.GridHash()} lit {_lights.HasLights} shading {_lighting.Shading}"
                + $" tints {_lighting.Tints.Count} light-at {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rows.ToString())))[..16]}");
            Assert.That(_lighting.Shading, Is.True, name);
        }

        if (Environment.GetEnvironmentVariable("PFWOLF_LIGHT_SNAPSHOT") is { Length: > 0 } path)
            File.WriteAllText(path, snapshot.ToString());
        Assert.That(snapshot.ToString(), Does.Contain("lit True"));
    }
}

/// <summary>
/// The light grid of lit levels as the game plays them, hashed by the console's `lightgrid`
/// (flickering lights seeded), for the same before-and-after check on real content. Set
/// PFWOLF_LIGHTGRID_SNAPSHOT to a file path to write the hashes there. Needs the game's data files.
/// </summary>
[Category("GameData")]
public class LightGridGameTests
{
    private static readonly (string Release, string Pack, string Map, string? Mod)[] Levels =
    [
        ("wolf3d", "wolf3d", "MAP01", "moody-lights"),
        ("wolf3d", "wolf3d", "MAP01", "switch-demo"),
        ("wolf3d", "wolf3d", "MAP01", "weather-demo"),
        ("wolf3d", "wolf3d", "MAP01", "particles-demo"),
        ("wolf3d", "wolf3d", "MAP01", "hub-demo"),
        ("blake-aog", "blake", "MAP01", null),
        ("blake-ps", "planetstrike", "MAP01", null),
    ];

    [Test]
    public void Lit_Levels_Light_Grids_Are_Written()
    {
        var snapshot = new StringBuilder();
        foreach (var group in Levels.GroupBy(level => level.Release))
        {
            var workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-lightgrid-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workFolder);
            try
            {
                HeadlessGame.PrepareFolder(workFolder, group.Key);
                foreach (var level in group)
                {
                    // Act: the grid as the level starts, the enemies gone so no door they open
                    // changes where light goes
                    var mods = level.Mod == null ? [] : new[] { Path.Combine(TestPaths.RepoRoot(), "examples", "mods", level.Mod) };
                    var (exitCode, output) = HeadlessGame.Run(workFolder, level.Pack, "lightgrid seed 7; killall; wait 2; lightgrid; quit",
                        TimeSpan.FromMinutes(2), ["--warp", level.Map, "--cheats"], mods);
                    var hashes = output.Where(line => line.StartsWith("lightgrid ")).ToList();

                    // Assert
                    Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(20)));
                    Assert.That(hashes, Has.Count.EqualTo(1), string.Join(Environment.NewLine, output.TakeLast(20)));
                    snapshot.AppendLine($"{level.Pack} {level.Map} {level.Mod ?? "-"}: {string.Join(" | ", hashes)}");
                }
            }
            finally
            {
                try { Directory.Delete(workFolder, recursive: true); } catch (IOException) { }
            }
        }

        if (Environment.GetEnvironmentVariable("PFWOLF_LIGHTGRID_SNAPSHOT") is { Length: > 0 } path)
            File.WriteAllText(path, snapshot.ToString());
    }
}
