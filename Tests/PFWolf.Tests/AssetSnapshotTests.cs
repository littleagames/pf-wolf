using System.Diagnostics;
using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace PFWolf.Tests;

/// <summary>
/// Snapshots of every YAML asset the loader builds, pack by pack, for checking that a change to
/// how the YAML files merge leaves the game's data as it was. Run by hand:
/// set PFWOLF_SNAPSHOT_OUT to a folder, run this class, change the loader, run it again into
/// another folder and diff the two (git diff --no-index). Each pack is written twice: from
/// pfwolf-pk3 alone, and with the mods that suit it plus a probe mod touching every kind of
/// YAML file a mod can carry (so the mods' merge path rebuilds each asset).
/// </summary>
[Explicit("Writes snapshots for a before/after comparison; set PFWOLF_SNAPSHOT_OUT")]
[Category("Snapshot")]
public class AssetSnapshotTests
{
    private static readonly (string Pack, string Release)[] Games =
    [
        ("wolf3d", "wolf3d"),
        ("spear", "spear"),
        ("blake", "blake-aog"),
        ("planetstrike", "blake-ps"),
        ("wolf3d-shareware", "wolf3d-shareware"),
        ("wolf3d-apogee", "wolf3d-apogee"),
        ("spear-demo", "spear-demo"),
    ];

    // The assets read from YAML files (the rest are pictures, sounds, maps and the like)
    private static readonly HashSet<Type> YamlAssetTypes =
    [
        typeof(AliasAsset), typeof(GameInfoAsset), typeof(RawDataMapAsset), typeof(ColorThemeAsset),
        typeof(FontDefinitionsAsset), typeof(HudMessageStylesAsset), typeof(StatusBarAsset),
        typeof(SoundSequenceAsset), typeof(IntermissionAsset), typeof(PresenterAsset), typeof(MoviesAsset),
        typeof(ElevatorAsset), typeof(LanguageAsset), typeof(ActorTranslationAsset),
        typeof(MapObjectTranslationAsset), typeof(MenuAsset), typeof(GamePackInfoAsset),
    ];

    private static IEnumerable<TestCaseData> GameCases() =>
        Games.Select(game => new TestCaseData(game.Pack, game.Release).SetArgDisplayNames(game.Pack));

    [TestCaseSource(nameof(GameCases))]
    public void Write_Snapshot(string pack, string release)
    {
        var outFolder = Environment.GetEnvironmentVariable("PFWOLF_SNAPSHOT_OUT");
        if (string.IsNullOrWhiteSpace(outFolder))
            Assert.Ignore("Set PFWOLF_SNAPSHOT_OUT to the folder to write the snapshots to");
        Directory.CreateDirectory(outFolder);

        var probeFolder = WriteProbeMod();
        try
        {
            var baseSources = new IAssetSource[] { new DirectoryAssetSource(TestPaths.Pk3SourceFolder()) };
            var mods = ModsFor(pack, release).Append(probeFolder).Select(folder => (IAssetSource)new DirectoryAssetSource(folder)).ToList();

            var timer = Stopwatch.StartNew();
            var plain = new PfWolfPk3Loader(baseSources, pack, release);
            var plainMs = timer.ElapsedMilliseconds;
            timer.Restart();
            var modded = new PfWolfPk3Loader(baseSources, pack, release, mods);
            var moddedMs = timer.ElapsedMilliseconds;

            File.WriteAllText(Path.Combine(outFolder, $"{pack}.yaml"), Snapshot(plain));
            File.WriteAllText(Path.Combine(outFolder, $"{pack}+mods.yaml"), Snapshot(modded));
            File.WriteAllText(Path.Combine(outFolder, $"{pack}.warnings.txt"),
                string.Join(Environment.NewLine, plain.Warnings.Concat(modded.Warnings.Select(w => "[mods] " + w))));
            TestContext.Out.WriteLine($"{pack}: {plainMs} ms alone, {moddedMs} ms with {mods.Count} mods");
        }
        finally
        {
            Directory.Delete(probeFolder, recursive: true);
        }
    }

    // The repository's own mods whose modinfo game-packs take in the pack (or its base packs,
    // as ModSource picks them)
    private static IEnumerable<string> ModsFor(string pack, string release)
    {
        var root = TestPaths.RepoRoot();
        var packs = PfWolfPk3Loader.ReadBasePackIds(new DirectoryAssetSource(TestPaths.Pk3SourceFolder()), release)
            .Prepend(pack).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = Directory.GetDirectories(Path.Combine(root, "examples", "mods"))
            .Append(Path.Combine(root, "multiplayer-pk3"));
        foreach (var folder in candidates)
        {
            var info = YamlTree.Parse(File.ReadAllText(Path.Combine(folder, "modinfo.yaml")));
            var gamePacks = info?.Children.FirstOrDefault(c => (c.Key as YamlScalarNode)?.Value == "game-packs").Value as YamlSequenceNode;
            if (gamePacks != null && gamePacks.Children.OfType<YamlScalarNode>().Any(p => packs.Contains(p.Value!)))
                yield return folder;
        }
    }

    // A mod with one harmless new key in each kind of YAML file a mod can carry: unknown keys
    // the readers skip, or new entries that are the same before and after
    private static string WriteProbeMod()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"pfwolf-snapshot-probe-{Guid.NewGuid():N}");
        void Write(string path, string yaml)
        {
            var full = Path.Combine(folder, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, yaml);
        }

        Write("modinfo.yaml", "name: Snapshot probe\nversion: \"1\"\n");
        Write("actordefs/probe.yaml", "ZzSnapshotProbe:\n  radius: 1\n");
        Write("mapdefs/probe.yaml", "zz-probe: 1\n");
        Write("game-info.yaml", "zz-probe: 1\n");
        Write("statusbar.yaml", "zz-probe:\n  x: 1\n");
        Write("colors.yaml", "ZZPROBE: \"#000000\"\n");
        Write("fonts.yaml", "zz-probe: {}\n");
        Write("hud-messages.yaml", "zz-probe: {}\n");
        Write("intermission.yaml", "zz-probe: 1\n");
        Write("raw-data-map.yaml", "zz-probe: {}\n");
        Write("movies.yaml", "zz-probe: {}\n");
        Write("alias.yaml", "zz-probe: 1\n");
        Write("language/en-us.yaml", "$ZZ_PROBE: \"probe\"\n");
        return folder;
    }

    /// <summary>
    /// The loader's YAML assets as one document, keyed by asset key, every mapping's keys sorted
    /// so that only what the assets hold shows up in a diff, not the order it was merged in
    /// </summary>
    internal static string Snapshot(PfWolfPk3Loader loader)
    {
        var serializer = new SerializerBuilder()
            .DisableAliases()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
            .Build();

        var root = new YamlMappingNode();
        foreach (var (key, asset) in loader.PeekAssets().OrderBy(a => a.Key, StringComparer.Ordinal))
        {
            if (!YamlAssetTypes.Contains(asset.GetType()))
                continue;

            var stream = new YamlStream();
            stream.Load(new StringReader(serializer.Serialize(asset)));
            root.Add(key, Sorted(stream.Documents[0].RootNode));
        }

        return YamlTree.ToText(root);
    }

    private static YamlNode Sorted(YamlNode node) => node switch
    {
        YamlMappingNode mapping => new YamlMappingNode(mapping.Children
            .OrderBy(c => (c.Key as YamlScalarNode)?.Value ?? c.Key.ToString(), StringComparer.Ordinal)
            .Select(c => new KeyValuePair<YamlNode, YamlNode>(c.Key, Sorted(c.Value)))),
        YamlSequenceNode sequence => new YamlSequenceNode(sequence.Children.Select(Sorted)),
        _ => node,
    };
}
