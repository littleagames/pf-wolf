using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Loaders;
using PFWolf.Managers;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.Definitions;

/// <summary>A definition the editor lists: its key path in the merged document, its name, and what it's listed under</summary>
public sealed record DefinitionEntry(IReadOnlyList<string> Path, string Name, string Group)
{
    public override string ToString() => Name;
}

/// <summary>
/// A change worked out, ready to save: the file it goes in, the value it sets there (null takes
/// the key out), the file's new text, and what the editor should know (a later file that
/// changes the same definition, say)
/// </summary>
public sealed record DefinitionWrite(DefinitionFile File, IReadOnlyList<string> Path, YamlNode? Value, string NewText, IReadOnlyList<string> Notes);

/// <summary>
/// The files of one kind of definition, in the order the game loads them (pfwolf.pk3's, then
/// each mod's), and what they merge into: the game's own rules for pfwolf.pk3's files (actordefs
/// class by class, mapdefs entry by entry, game-info all the way down) and a mod's laid over
/// those all the way down. Every file's part in a definition can be found, and a change to a
/// definition worked out as the least that does it: in a mod, the keys that differ from what
/// the files before it make (keys taken out tagged !remove). pfwolf.pk3's own files are
/// read-only: changes only ever go in a mod.
/// </summary>
public sealed class DefinitionSet
{
    private static readonly FieldSchema MapDefsSchema = FieldSchema.For(typeof(MapObjectTranslationAsset));

    private YamlMappingNode? _merged, _mergedBase;

    public DefinitionSet(DefinitionKind kind, string packId, IReadOnlyList<DefinitionFile> files)
    {
        Kind = kind;
        PackId = packId;
        Files = files;
        foreach (var file in files)
            CheckReads(file);
    }

    public DefinitionKind Kind { get; }
    public string PackId { get; }

    /// <summary>Every file, in load order; those with an Error are left out of the merge, as the game leaves them out</summary>
    public IReadOnlyList<DefinitionFile> Files { get; }

    /// <summary>
    /// The files of a kind the loaded game has: pfwolf.pk3's that went into its assets (the
    /// running pack's and the base packs' it's built on), then each mod's, as they are now
    /// </summary>
    public static DefinitionSet Load(GameContent content, DefinitionKind kind)
    {
        var files = new List<DefinitionFile>();
        var basePk3 = new Pk3AssetSource(Path.Combine(content.GameFolder, AssetManager.BasePk3FileName));
        void AddBase(string path, string layer = "")
        {
            if (!files.Any(file => file.Is(null, path)))
                files.Add(DefinitionFile.Read(basePk3, null, path, layer));
        }

        if (kind == DefinitionKind.GamePackInfo)
            AddBase("gamepacks/gamepack-info.yaml");
        else
        {
            foreach (var (assetName, assetType, layer) in BaseAssets(kind, content.PackId))
            {
                var origins = content.Assets.FindAssetOrigins(assetName)
                    .Where(asset => asset.Type == assetType)
                    .SelectMany(asset => asset.Origins)
                    .Where(origin => origin.Action != AssetOrigin.LeftOut && origin.Source.Equals(basePk3.Name, StringComparison.OrdinalIgnoreCase));
                foreach (var origin in origins)
                    AddBase(origin.Path, layer);
            }
        }

        foreach (var mod in content.Assets.LoadedMods)
            files.AddRange(ReadModFiles(kind, mod.FullPath));
        return new DefinitionSet(kind, content.PackId, files);
    }

    /// <summary>A mod's files of a kind, read again (as it is now, saved to since it loaded or not)</summary>
    public static IEnumerable<DefinitionFile> ReadModFiles(DefinitionKind kind, string modPath)
    {
        var source = ModSource.TryOpen(modPath, [])?.Source;
        if (source == null)
            yield break;
        foreach (var path in source.EntryPaths.Where(path => DefinitionKinds.IsModFile(kind, path)))
            yield return DefinitionFile.Read(source, modPath, path);
    }

    // The assets pfwolf.pk3's files of a kind load into, and the type they're read as
    private static IEnumerable<(string Name, string Type, string Layer)> BaseAssets(DefinitionKind kind, string packId) => kind switch
    {
        DefinitionKind.GameInfo => [($"{packId}/game-info", nameof(GameInfoAsset), "")],
        DefinitionKind.Actors =>
        [
            ("actordefs", nameof(ActorTranslationAsset), DefinitionFile.SharedLayer),
            ($"{packId}/actordefs", nameof(ActorTranslationAsset), ""),
        ],
        DefinitionKind.MapDefs => [($"{packId}/mapdefs", nameof(MapObjectTranslationAsset), "")],
        DefinitionKind.Animations =>
        [
            ("animdefs", nameof(AnimationDefinitionsAsset), DefinitionFile.SharedLayer),
            ($"{packId}/animdefs", nameof(AnimationDefinitionsAsset), ""),
        ],
        _ => [],
    };

    // A file that's YAML but can't be read as its kind is left out too (the game leaves out a mod's)
    private void CheckReads(DefinitionFile file)
    {
        if (file.Tree == null || file.Error != null)
            return;
        if (DefinitionKinds.Check(Kind, YamlTree.ToText(YamlTree.WithoutMergeTags(file.Tree))) is { } error)
            file.SetReadError(error);
    }

    //
    // Merging
    //

    /// <summary>What every file makes, mods and all: what the game plays</summary>
    public YamlMappingNode Merged => _merged ??= Replay(Files);

    /// <summary>What pfwolf.pk3's files make on their own, which the mods change</summary>
    public YamlMappingNode MergedBase => _mergedBase ??= Replay(Files.Where(file => file.IsBase));

    /// <summary>What these files make, merged as the game merges them</summary>
    public YamlMappingNode Replay(IEnumerable<DefinitionFile> files)
    {
        var (shared, pack) = ReplayLayers(files);
        if (Kind == DefinitionKind.Animations)
        {
            // The pack's animation replaces a shared one of the same name (AssetManager.BuildAnimations)
            var animations = (YamlMappingNode)YamlNodes.Clone(shared);
            foreach (var (key, value) in pack.Children)
                YamlTree.Set(animations, key, value);
            return animations;
        }
        if (Kind != DefinitionKind.Actors)
            return pack;

        // A pack's class replaces a shared one of the same name, or with extend: true changes it (ActorData.Combine)
        var combined = (YamlMappingNode)YamlNodes.Clone(shared);
        ActorTranslationAsset.MergeYaml(combined, pack, deep: false);
        return combined;
    }

    /// <summary>The shared actordefs and the pack's (for other kinds, all in the second)</summary>
    private (YamlMappingNode Shared, YamlMappingNode Pack) ReplayLayers(IEnumerable<DefinitionFile> files)
    {
        var shared = new YamlMappingNode();
        var pack = new YamlMappingNode();
        foreach (var file in files)
        {
            if (file.Tree == null || file.Error != null)
                continue;
            // Merged from a copy: the merge puts the file's own nodes into what it makes, and later files change those
            var tree = (YamlMappingNode)YamlNodes.Clone(file.Tree);
            var target = file.Layer == DefinitionFile.SharedLayer ? shared : pack;
            switch (Kind)
            {
                case DefinitionKind.GameInfo:
                    YamlTree.DeepMerge(target, tree);
                    break;
                case DefinitionKind.MapDefs:
                    if (file.IsBase)
                        YamlTree.MergeLevels(target, tree, 2);
                    else
                        YamlTree.DeepMerge(target, tree);
                    break;
                case DefinitionKind.Animations:
                    // pfwolf.pk3's files replace animations name by name; a mod's merge all the way down
                    if (file.IsBase)
                        YamlTree.MergeLevels(target, tree, 1);
                    else
                        YamlTree.DeepMerge(target, tree);
                    break;
                case DefinitionKind.Actors:
                    ActorTranslationAsset.MergeYaml(target, tree, deep: !file.IsBase);
                    break;
                default:
                    // The releases: a mod's games are added, but it can't change pfwolf.pk3's
                    foreach (var (key, value) in YamlTree.WithoutMergeTags(tree).Children)
                    {
                        if (file.IsBase || YamlTree.FindKey(target, key) == null)
                            YamlTree.Set(target, key, value);
                    }
                    break;
            }
        }
        return (shared, pack);
    }

    //
    // Entries
    //

    /// <summary>
    /// The definitions listed: game-info's sections, the releases, the actor classes, and each
    /// mapdefs table's entries (or the table, for one of single values: floors, map-info)
    /// </summary>
    public IReadOnlyList<DefinitionEntry> Entries(bool baseOnly)
    {
        var merged = baseOnly ? MergedBase : Merged;
        var entries = new List<DefinitionEntry>();
        foreach (var (key, value) in merged.Children)
        {
            if (key is not YamlScalarNode { Value: { } name })
                continue;

            // flats is a record of tables (floor, ceiling), listed whole so they show inside it
            if (Kind == DefinitionKind.MapDefs && value is YamlMappingNode { Children.Count: > 0 } table
                && table.Children.Values.All(child => child is YamlMappingNode)
                && MapDefsSchema.Field(name) is not { Type: FieldType.Record })
            {
                foreach (var (entryKey, _) in table.Children)
                {
                    if (entryKey is YamlScalarNode { Value: { } id })
                        entries.Add(new DefinitionEntry([name, id], id, name));
                }
                continue;
            }

            var group = Kind switch
            {
                DefinitionKind.Actors or DefinitionKind.GamePackInfo or DefinitionKind.Animations => LastFileDefining([name], baseOnly)?.SourceName ?? "",
                _ => "",
            };
            entries.Add(new DefinitionEntry([name], name, group));
        }
        return entries;
    }

    /// <summary>The value a definition has: as the game has it, or (base only) as pfwolf.pk3's files alone make it</summary>
    public YamlNode? ValueOf(IReadOnlyList<string> path, bool baseOnly) => YamlNodes.Get(baseOnly ? MergedBase : Merged, path);

    /// <summary>The files that have a part in a definition, in load order (the last one's is used, or merged over the rest)</summary>
    public IReadOnlyList<DefinitionFile> FilesDefining(IReadOnlyList<string> path, bool baseOnly = false)
        => Files.Where(file => (!baseOnly || file.IsBase) && file.Error == null && YamlNodes.Get(file.Tree, path) != null).ToList();

    private DefinitionFile? LastFileDefining(IReadOnlyList<string> path, bool baseOnly = false) => FilesDefining(path, baseOnly).LastOrDefault();

    /// <summary>A copy with a file's text changed (as saved), or a new file added after its mod's others</summary>
    public DefinitionSet With(DefinitionFile file)
    {
        var files = Files.ToList();
        int index = files.FindIndex(existing => existing.Is(file.ModPath, file.EntryPath));
        if (index >= 0)
            files[index] = file;
        else
        {
            int last = files.FindLastIndex(existing => DefinitionFile.SameMod(existing.ModPath, file.ModPath));
            files.Insert(last >= 0 ? last + 1 : files.Count, file);
        }
        return new DefinitionSet(Kind, PackId, files);
    }

    //
    // Changes
    //

    /// <summary>
    /// Works out how to save a definition as edited (null: taken out) into the mod
    /// <paramref name="modPath"/> (pfwolf.pk3's own files are read-only). Throws
    /// InvalidDataException when the result wouldn't read as the game reads it.
    /// </summary>
    /// <param name="edited">The definition as it should be: all of it as the game would have it</param>
    public DefinitionWrite PlanWrite(IReadOnlyList<string> path, YamlNode? edited, string modPath)
    {
        if (path.Count == 0)
            throw new ArgumentException("A definition needs a key path", nameof(path));

        var target = ModTarget(path, modPath);
        if (target.Error != null && !target.IsNew)
            throw new InvalidDataException($"{target.Label} doesn't read, so the editor can't change it: fix it in its YAML first. {target.Error}");

        var notes = new List<string>();
        var before = FilesBefore(target).ToList();
        var existing = YamlNodes.Get(target.Tree, path);
        // What the files before it make of the definition (for extend: true, what it changes)
        var baseValue = YamlNodes.Get(Replay(before), path);
        YamlNode? value;
        if (Kind == DefinitionKind.GamePackInfo)
            value = edited;
        else
        {
            // What differs from the files before it; for a class, from the pack's own
            // (a pack class of the name replaces a shared one whole)
            // (and for an animation: a mod's go in with the pack's, whose replace shared ones whole)
            var overlayBase = Kind is DefinitionKind.Actors or DefinitionKind.Animations ? YamlNodes.Get(ReplayLayers(before).Pack, path) : baseValue;
            value = ActorOverlay(existing, baseValue, edited, path) ?? YamlNodes.Overlay(overlayBase, edited, path.Count);
        }

        return Finish(target, path, value, notes);
    }

    /// <summary>
    /// Works out taking a mod's own say in a definition out of it (its file's part, not a
    /// !remove), so the definition is what the files before the mod make it
    /// </summary>
    public DefinitionWrite PlanTakeOut(IReadOnlyList<string> path, string modPath)
    {
        var target = ModTarget(path, modPath);
        if (target.IsNew || YamlNodes.Get(target.Tree, path) == null)
            throw new InvalidDataException($"{Path.GetFileName(modPath.TrimEnd('\\', '/'))} doesn't change {YamlNodes.Describe(path)}");
        return Finish(target, path, null, []);
    }

    /// <summary>
    /// Works out copying a definition whole, as the game has it now, into a mod (the file of it
    /// the mod would save it in), so it can be changed there. Throws InvalidDataException when it
    /// can't be: the mod has its own of it already (that's what to edit), the game has none, or
    /// it's one of pfwolf.pk3's games, which a mod can't change.
    /// </summary>
    public DefinitionWrite PlanCopyToMod(IReadOnlyList<string> path, string modPath)
    {
        if (path.Count == 0)
            throw new ArgumentException("A definition needs a key path", nameof(path));
        var modName = Path.GetFileName(modPath.TrimEnd('\\', '/'));
        if (ModChanges(path, modPath))
            throw new InvalidDataException($"{modName} has its own {YamlNodes.Describe(path)} already");
        if (YamlNodes.Get(Merged, path) is not { } value)
        {
            // A game-info section the game doesn't set (a standalone game has none of Wolf3D's):
            // the mod gets one to fill in
            if (Kind == DefinitionKind.GameInfo && path.Count == 1 && GameInfoSchema.Field(path[0]) is { } section)
                return Finish(ModTarget(path, modPath), path, section.StarterValue(), [$"The game had no {path[0]}: it's started in {modName} for you to fill in"]);
            throw new InvalidDataException($"The game has no {YamlNodes.Describe(path)} to copy");
        }
        if (Kind == DefinitionKind.GamePackInfo)
            throw new InvalidDataException("A mod can't change pfwolf.pk3's games; its own gamepack-info.yaml adds games of its own (New…)");

        return Finish(ModTarget(path, modPath), path, YamlNodes.Clone(YamlTree.WithoutMergeTags(value)), []);
    }

    private static FieldSchema? _gameInfoSchema;
    private static FieldSchema GameInfoSchema => _gameInfoSchema ??= FieldSchema.For(typeof(GameInfoAsset));

    /// <summary>The mod's file with its own of a definition, or null when it has none</summary>
    public DefinitionFile? ModFileDefining(IReadOnlyList<string> path, string modPath)
        => Files.LastOrDefault(file => DefinitionFile.SameMod(file.ModPath, modPath) && file.Error == null && YamlNodes.Get(file.Tree, path) != null);

    /// <summary>Whether a mod has a say in a definition</summary>
    public bool ModChanges(IReadOnlyList<string> path, string modPath)
        => Files.Any(file => DefinitionFile.SameMod(file.ModPath, modPath) && file.Error == null && YamlNodes.Get(file.Tree, path) != null);

    // The file's new text with the value set, checked as the game would read it
    private DefinitionWrite Finish(DefinitionFile target, IReadOnlyList<string> path, YamlNode? value, List<string> notes)
    {
        var newText = YamlText.Sync(target.Text, path, value);
        var changed = target.WithText(newText);
        if (changed.Error != null)
            throw new InvalidDataException($"The change doesn't make {target.Label} YAML the editor can read: {changed.Error}");

        // What the game would make of it
        var after = With(changed);
        var savedFile = after.Files.First(file => file.Is(changed.ModPath, changed.EntryPath));
        if (savedFile.Error != null)
            throw new InvalidDataException($"{target.Label} wouldn't read as {DefinitionKinds.Title(Kind).ToLowerInvariant()}: {savedFile.Error}");
        if (DefinitionKinds.Check(Kind, YamlTree.ToText(after.Merged)) is { } error)
            throw new InvalidDataException($"With the change, the {DefinitionKinds.Title(Kind).ToLowerInvariant()} wouldn't read: {error}");

        // A file loaded after it that has its own say
        int targetIndex = after.Files.ToList().IndexOf(savedFile);
        foreach (var later in after.Files.Skip(targetIndex + 1))
        {
            if (later.Error == null && YamlNodes.Get(later.Tree, path) != null)
                notes.Add($"{later.Label} is loaded after it and changes {YamlNodes.Describe(path)} too, so that's what the game has");
        }

        return new DefinitionWrite(target, path, value, newText, notes);
    }

    /// <summary>
    /// For a class written with extend: true, which changes the class before it rather than
    /// replacing it: its properties and states by name, and its flags added (~FLAG takes one
    /// away). Null for anything else.
    /// </summary>
    private YamlNode? ActorOverlay(YamlNode? existing, YamlNode? baseValue, YamlNode? edited, IReadOnlyList<string> path)
    {
        if (Kind != DefinitionKind.Actors || path.Count != 1 || existing is not YamlMappingNode existingClass
            || !IsExtend(existingClass) || edited is not YamlMappingNode editedClass)
            return null;

        // properties and states are merged a key down; anything below that is replaced
        var overlay = YamlNodes.Overlay(baseValue, edited, depth: 1, wholeAtDepth: 3) as YamlMappingNode ?? new YamlMappingNode();
        overlay.Children.Remove(YamlNodes.FindKey(overlay, "extend") ?? new YamlScalarNode("\0"));
        var extend = new YamlMappingNode { { "extend", "true" } };
        foreach (var (key, value) in overlay.Children)
            extend.Children.Add(key, value);

        // Flags are added to the class's: the ones taken out are written ~FLAG
        if (YamlNodes.FindKey(extend, "flags") is { } flagsKey)
        {
            var had = Flags(YamlNodes.Get(baseValue, ["flags"]));
            var now = Flags(YamlNodes.Get(editedClass, ["flags"]));
            var written = now.Where(flag => !had.Contains(flag, StringComparer.OrdinalIgnoreCase))
                .Concat(had.Where(flag => !now.Contains(flag, StringComparer.OrdinalIgnoreCase)).Select(flag => "~" + flag))
                .Select(flag => (YamlNode)new YamlScalarNode(flag))
                .ToList();
            if (written.Count == 0)
                extend.Children.Remove(flagsKey);
            else
                extend.Children[flagsKey] = new YamlSequenceNode(written) { Style = YamlDotNet.Core.Events.SequenceStyle.Flow };
        }
        return extend;
    }

    // The files loaded before one (for a new one, before where it would go: after its mod's others)
    private IEnumerable<DefinitionFile> FilesBefore(DefinitionFile target)
    {
        int index = Files.ToList().IndexOf(target);
        if (index < 0)
        {
            int last = Files.ToList().FindLastIndex(file => DefinitionFile.SameMod(file.ModPath, target.ModPath));
            index = last >= 0 ? last + 1 : Files.Count;
        }
        return Files.Take(index);
    }

    private static List<string> Flags(YamlNode? node)
        => node is YamlSequenceNode sequence ? sequence.Children.OfType<YamlScalarNode>().Select(flag => flag.Value ?? "").ToList() : [];

    private static bool IsExtend(YamlMappingNode actor)
        => YamlNodes.Get(actor, ["extend"]) is YamlScalarNode { Value: var value } && bool.TryParse(value, out var extend) && extend;

    /// <summary>
    /// The mod file a definition goes in: the one of the mod that has it already, else its
    /// root file (game-info.yaml), its file with the mapdefs table, or a new one
    /// (actordefs/actors.yaml, mapdefs/TABLE.yaml)
    /// </summary>
    private DefinitionFile ModTarget(IReadOnlyList<string> path, string modPath)
    {
        var modFiles = Files.Where(file => DefinitionFile.SameMod(file.ModPath, modPath)).ToList();
        if (DefinitionKinds.ModRootFile(Kind) is { } root)
            return modFiles.FirstOrDefault(file => file.Is(modPath, root)) ?? new DefinitionFile(modPath, root, "", "", isNew: true);

        var defining = modFiles.LastOrDefault(file => YamlNodes.Get(file.Tree, path) != null)
                       ?? (Kind == DefinitionKind.MapDefs ? modFiles.LastOrDefault(file => YamlNodes.Get(file.Tree, [path[0]]) != null) : null);
        if (defining != null)
            return defining;

        var name = Kind switch
        {
            DefinitionKind.Actors => "actordefs/actors.yaml",
            DefinitionKind.Animations => "animdefs/animdefs.yaml",
            _ => $"mapdefs/{FileNameFor(path[0])}.yaml",
        };
        return modFiles.FirstOrDefault(file => file.Is(modPath, name)) ?? new DefinitionFile(modPath, name, "", "", isNew: true);
    }

    private static string FileNameFor(string key)
    {
        var name = new string(key.Select(c => char.IsLetterOrDigit(c) || c == '-' ? char.ToLowerInvariant(c) : '-').ToArray()).Trim('-');
        return name.Length > 0 ? name : "mapdefs";
    }
}
