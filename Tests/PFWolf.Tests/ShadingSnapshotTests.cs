using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>
/// The game's distance shading as every game, the Multiplayer mod and each example mod have it:
/// the light rows built from each palette for every fade color and tint they use, each map's
/// resolved shading and the fade steps it gives at distances and heights, each map's resolved
/// light zones, and bright / lighten for every frame of every class. Kept so moving the shading
/// math into PFWolf.Common (docs/dev/lighting-in-common-plan.md phase 0) can be checked to change
/// nothing: set PFWOLF_SHADING_SNAPSHOT to a file path and the snapshot is written there, to
/// compare before and after.
/// </summary>
[Category("Content")]
public class ShadingSnapshotTests
{
    private const long Tile = MapConstants.TileGlobal;

    private static readonly (string Pack, string Release, string Palette)[] Games =
    [
        ("wolf3d", "wolf3d", "wolfpal"),
        ("spear", "spear", "spearpal"),
        ("blake", "blake-aog", "blake"),
        ("planetstrike", "blake-ps", "blake"),
        ("wolf3d-shareware", "wolf3d-shareware", "wolfpal"),
        ("wolf3d-apogee", "wolf3d-apogee", "wolfpal"),
        ("spear-demo", "spear-demo", "spearpal"),
    ];

    // View height numerators (ViewSetup: TileGlobal × scale >> 6) for a few view widths
    private static readonly int[] HeightNumerators = [10240, 26624, 53248];

    private static readonly int[] Lightens = [-2, 0, 5, 6, 26];

    [Test]
    public void Every_Games_Shading_Is_Written()
    {
        // Arrange
        var root = TestPaths.RepoRoot();
        var pk3 = TestPaths.Pk3SourceFolder();
        List<(string Title, string Pack, string Release, string Palette, string? Mod)> runs =
        [
            .. Games.Select(game => (game.Pack, game.Pack, game.Release, game.Palette, (string?)null)),
            ("wolf3d + multiplayer-pk3", "wolf3d", "wolf3d", "wolfpal", Path.Combine(root, "multiplayer-pk3")),
            .. Directory.GetDirectories(Path.Combine(root, "examples", "mods")).Order(StringComparer.Ordinal)
                .Select(mod => (Path.GetFileName(mod), ModPack(mod), ReleaseOf(ModPack(mod)), PaletteOf(ModPack(mod)), (string?)mod)),
        ];

        var output = new StringWriter();
        var console = Console.Out;
        Console.SetOut(output);
        var snapshot = new StringBuilder();
        // Each palette (by its bytes) with the fade colors and tints any run used with it
        var palettes = new Dictionary<string, (PaletteColor[] Colors, SortedSet<int> Fades, SortedSet<int> Tints)>(StringComparer.Ordinal);
        int frames = 0, maps = 0;
        try
        {
            // Act
            foreach (var run in runs)
            {
                snapshot.AppendLine($"=== {run.Title}");
                var colors = LoadPalette(pk3, run.Mod, run.Palette);
                var key = Hash(PaletteFile.ToRaw(colors));
                if (!palettes.TryGetValue(key, out var used))
                    palettes[key] = used = (colors, new SortedSet<int> { 0x000000, 0x808080, 0xFFFFFF }, new SortedSet<int> { 0xFFFFFF, 0xFF0000, 0x4060FF, 0xFFFF80 });
                snapshot.AppendLine($"  palette {run.Palette} {key}");

                PfWolfPk3Loader loader;
                try
                {
                    loader = new PfWolfPk3Loader([new DirectoryAssetSource(pk3)], run.Pack, run.Release, run.Mod != null ? [new DirectoryAssetSource(run.Mod)] : null);
                }
                catch (Exception e)
                {
                    snapshot.AppendLine($"  doesn't load: {e.GetType().Name}");
                    continue;
                }

                var gameInfo = TryLoad<GameInfoAsset>(loader, $"{run.Pack}/game-info") ?? new GameInfoAsset();
                maps += WriteShading(snapshot, gameInfo, used.Fades, used.Tints);
                frames += WriteFrames(snapshot, loader, run.Pack, run.Release, gameInfo);
                Flush(snapshot, output);
            }

            snapshot.AppendLine("=== light rows");
            foreach (var (key, (colors, fades, tints)) in palettes)
                WriteRows(snapshot, key, colors, fades, tints);
            Flush(snapshot, output);
        }
        finally
        {
            Console.SetOut(console);
        }

        if (Environment.GetEnvironmentVariable("PFWOLF_SHADING_SNAPSHOT") is { Length: > 0 } path)
            File.WriteAllText(path, snapshot.ToString());

        // Assert: Blake's lamps are lightened and its levels shaded; every game's frames are there
        Assert.That(frames, Is.GreaterThan(10000));
        Assert.That(maps, Is.GreaterThan(20));
        Assert.That(snapshot.ToString(), Does.Contain("lighten 6"));
    }

    // Each map's (and the default map's) resolved shading, its fade steps, and its zones
    private static int WriteShading(StringBuilder snapshot, GameInfoAsset gameInfo, SortedSet<int> fades, SortedSet<int> tints)
    {
        var defaultMap = gameInfo.DefaultMap;
        snapshot.AppendLine($"  lighten-levels {string.Join(", ", gameInfo.LightenLevels.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"))}");
        int count = 0;
        foreach (var (name, map) in new[] { ("(default map)", (MapInfo?)null) }
                     .Concat(gameInfo.Maps.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (pair.Key, (MapInfo?)pair.Value))))
        {
            var settings = Rendering.Lighting.ResolveShading(map, defaultMap);
            var zones = Rendering.Lighting.ResolveZones(map, defaultMap);
            if (settings == null && zones.Count == 0 && map != null)
                continue;
            count++;

            snapshot.AppendLine($"  map {name}: {(settings == null ? "unshaded" : settings.ToString())}");
            foreach (var (id, zone) in zones.OrderBy(pair => pair.Key))
            {
                using var bytes = new MemoryStream();
                zone.Write(new BinaryWriter(bytes));
                snapshot.AppendLine($"    zone {id}: light {zone.Light} color {zone.Color ?? "-"} effect {zone.Effect} low {zone.Low} tics {zone.Tics}"
                    + $" bright-tics {zone.BrightTics} current {zone.Current} level {Rendering.Lighting.LightLevelOf(zone.Current)} saved {Hash(bytes.ToArray())}");
                var tint = Rendering.Lighting.ParseRgb(zone.Color, Rendering.Lighting.NoTint, "zone color");
                tints.Add(tint.R << 16 | tint.G << 8 | tint.B);
            }

            foreach (int heightNumerator in HeightNumerators)
            {
                var lighting = NewLighting(heightNumerator);
                lighting.LevelZones = zones;
                lighting.SetShading(settings);
                if (heightNumerator == HeightNumerators[0])
                {
                    snapshot.AppendLine($"    shading {lighting.Shading} fade #{lighting.FadeR:X2}{lighting.FadeG:X2}{lighting.FadeB:X2}"
                        + $" start {lighting.FadeStart} end {lighting.FadeEnd} max {lighting.MaxFadeStep} inverse {lighting.InverseFade} ambient {lighting.AmbientLevel}");
                    fades.Add(lighting.FadeR << 16 | lighting.FadeG << 8 | lighting.FadeB);

                    var distances = new StringBuilder();
                    for (long nx = 0; nx <= 64 * Tile; nx += Tile / 16)
                        distances.Append(lighting.ShadeOffset(nx)).Append(',');
                    foreach (long nx in new[] { 1L, Tile - 1, Tile + 1, 1000 * Tile, long.MaxValue })
                        distances.Append(lighting.ShadeOffset(nx)).Append(',');
                    snapshot.AppendLine($"    distances {Hash(distances.ToString())} at 8 tiles {lighting.ShadeOffset(8 * Tile) / 256}");
                }

                var heights = new StringBuilder();
                for (int height = 0; height <= 8192; height += height < 512 ? 1 : 8)
                {
                    heights.Append(lighting.ShadeOffsetForHeight(height)).Append(',');
                    foreach (int lighten in Lightens)
                        heights.Append(lighting.LightenedShadeOffset(height, lighten)).Append(',');
                }
                snapshot.AppendLine($"    heights@{heightNumerator} {Hash(heights.ToString())}");
            }
        }
        return count;
    }

    // Bright and lighten for each frame of every class, with the game's lighten levels
    private static int WriteFrames(StringBuilder snapshot, PfWolfPk3Loader loader, string pack, string release, GameInfoAsset gameInfo)
    {
        Rendering.Lighting.SetLightenLevels(gameInfo.LightenLevels);
        var metadata = new ActorMetadata();
        if (TryLoad<ActorTranslationAsset>(loader, "actordefs") is { } shared)
            metadata.AddActors(shared.Actors);
        if (new[] { pack }.Concat(PfWolfPk3Loader.ReadBasePackIds(new DirectoryAssetSource(TestPaths.Pk3SourceFolder()), release))
                .Select(id => TryLoad<ActorTranslationAsset>(loader, $"{id}/actordefs")).FirstOrDefault(actors => actors != null) is { } own)
            metadata.AddActors(own.Actors);

        var sink = WarningLog.Sink;
        WarningLog.Sink = _ => { };      // the actor snapshot has the class warnings
        int count = 0;
        var all = new StringBuilder();
        try
        {
            foreach (var (className, data) in metadata.Actors.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToList())
            {
                Entities.Actors.Actor actor;
                try
                {
                    actor = metadata.CreateActor(className, data);
                }
                catch (Exception)
                {
                    continue;
                }

                actor.CurrentState = null;
                var none = (Rendering.Lighting.IsBright(actor), Rendering.Lighting.Lighten(actor));
                all.Append($"{className}:{none};");
                if (none != (false, 0))
                    snapshot.AppendLine($"  {className} (no state): bright {none.Item1} lighten {none.Item2}");

                foreach (var (state, first) in actor.ResolvedStates.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    var seen = new HashSet<ActorStateFrame>(ReferenceEqualityComparer.Instance);
                    int index = 0;
                    for (var frame = first; frame != null && seen.Add(frame); frame = frame.Next, index++)
                    {
                        actor.CurrentState = frame;
                        bool bright = Rendering.Lighting.IsBright(actor);
                        int lighten = Rendering.Lighting.Lighten(actor);
                        all.Append($"{state}#{index}:{bright},{lighten};");
                        if (bright || lighten != 0)
                            snapshot.AppendLine($"  {className} {state}#{index}: bright {bright} lighten {lighten}");
                        count++;
                    }
                }
            }
        }
        finally
        {
            WarningLog.Sink = sink;
        }
        snapshot.AppendLine($"  {count} frames {Hash(all.ToString())}");
        return count;
    }

    // Every light level of each fade color and tint, hashed a (fade, tint) pair at a time
    private static void WriteRows(StringBuilder snapshot, string key, PaletteColor[] colors, SortedSet<int> fades, SortedSet<int> tints)
    {
        var video = Video(colors);
        snapshot.AppendLine($"  palette {key}");
        foreach (int fade in fades)
            foreach (int tint in tints)
            {
                using var rows = new MemoryStream();
                for (int level = 0; level < Rendering.Lighting.LightLevels; level++)
                    rows.Write(video.GetLightRow(level, Rendering.Lighting.LightLevels, Rendering.Lighting.FadeSteps,
                        (byte)(fade >> 16), (byte)(fade >> 8), (byte)fade, (byte)(tint >> 16), (byte)(tint >> 8), (byte)tint));
                snapshot.AppendLine($"    fade #{fade:X6} tint #{tint:X6}: {Hash(rows.ToArray())}");
            }

        foreach (float brightness in new[] { 0.25f, 0.5f, 0.6f, 0.75f })
            snapshot.AppendLine($"    darken {brightness}: {Hash(video.GetDarkenTable(brightness))}");
    }

    private static Lighting NewLighting(int heightNumerator) =>
        new(null!, LightingTests.GreyVideo(), new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession(), null),
            new LightingHooks(() => heightNumerator, () => { }, _ => { }));

    // A VideoManager drawing in these colors (the game sets its palette up with SDL)
    private static VideoManager Video(PaletteColor[] colors)
    {
        var video = new VideoManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")));
        var property = typeof(VideoManager).GetProperty("GamePal", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var colorType = property.PropertyType.GetElementType()!;
        var palette = Array.CreateInstance(colorType, 256);
        for (int i = 0; i < 256; i++)
        {
            object color = Activator.CreateInstance(colorType)!;
            colorType.GetField("r")!.SetValue(color, colors[i].Red);
            colorType.GetField("g")!.SetValue(color, colors[i].Green);
            colorType.GetField("b")!.SetValue(color, colors[i].Blue);
            colorType.GetField("a")!.SetValue(color, (byte)255);
            palette.SetValue(color, i);
        }
        property.SetValue(video, palette);
        return video;
    }

    // The mod's own palettes/NAME.pal, else the pk3's
    private static PaletteColor[] LoadPalette(string pk3, string? mod, string name)
    {
        var own = mod == null ? null : Path.Combine(mod, PaletteFile.EntryPath(name));
        var path = own != null && File.Exists(own) ? own : Path.Combine(pk3, PaletteFile.EntryPath(name));
        return PaletteFile.Read(File.ReadAllBytes(path)).Colors;
    }

    // The pack an example mod is for: the first of its modinfo game-packs, else Wolf3D
    private static string ModPack(string mod)
    {
        var info = Path.Combine(mod, "modinfo.yaml");
        var line = File.Exists(info) ? File.ReadLines(info).FirstOrDefault(l => l.StartsWith("game-packs:")) : null;
        var first = line?.Split(':', 2)[1].Trim(' ', '[', ']').Split(',')[0].Trim();
        return string.IsNullOrEmpty(first) ? "wolf3d" : first;
    }

    private static string ReleaseOf(string pack) => Games.FirstOrDefault(game => game.Pack == pack).Release ?? pack;

    private static string PaletteOf(string pack) => Games.FirstOrDefault(game => game.Pack == pack).Palette ?? "wolfpal";

    private static T? TryLoad<T>(PfWolfPk3Loader loader, string name) where T : Asset
    {
        try
        {
            return loader.Load<T>(name);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    // The console lines the shading printed (its warnings), where they came
    private static void Flush(StringBuilder snapshot, StringWriter output)
    {
        foreach (var line in output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
            snapshot.AppendLine($"  ! {line.TrimEnd('\r')}");
        output.GetStringBuilder().Clear();
    }

    private static string Hash(string text) => Hash(Encoding.UTF8.GetBytes(text));

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes))[..16];
}
