using System.Globalization;
using System.Text;
using PFWolf.Assets;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PFWolf.Editor.Editing;

/// <summary>
/// A level's own definitions (a UWMF level's walls, doors, flats and triggers the game's mapdefs
/// have nothing like: MapAsset.LocalDefs) and its extra things, as mapdefs YAML to read: they're
/// the level's, kept in its file, so they're shown, not edited. An entry can be copied into a
/// mod's mapdefs to make it the game's.
/// </summary>
public static class LevelOwnDefinitions
{
    private static readonly ISerializer Yaml = new SerializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults | DefaultValuesHandling.OmitEmptyCollections | DefaultValuesHandling.OmitNull)
        .DisableAliases()
        .Build();

    /// <summary>Whether the level has anything of its own to show</summary>
    public static bool Any(MapAsset map) => map.LocalDefs != null || map.ExtraThings.Count > 0;

    /// <summary>The level's own definitions and extra things as YAML, with a word on what they are</summary>
    public static string Describe(MapAsset map, string levelName)
    {
        var text = new StringBuilder();
        if (!Any(map))
        {
            text.Append($"# {levelName} has nothing of its own: every wall, door, flat, trigger and thing on it\n");
            text.Append("# is one of the game's mapdefs.\n");
            return text.ToString();
        }

        text.Append($"# {levelName}'s own definitions, from its UWMF file: what the game's mapdefs have nothing like,\n");
        text.Append("# on codes the mapdefs leave free. They're the level's, so they're shown here, not edited:\n");
        text.Append("# change the level's tiles in its file, or copy an entry into a mod's mapdefs to make it the game's.\n");

        if (map.LocalDefs is { } local)
        {
            var sections = new Dictionary<string, object>();
            if (local.Walls.Count > 0)
                sections["walls"] = local.Walls.OrderBy(w => w.Key).ToDictionary(w => w.Key, w => Faces(w.Value, wall: true));
            if (local.Doors.Count > 0)
                sections["doors"] = local.Doors.OrderBy(d => d.Key).ToDictionary(d => d.Key, d => Faces(d.Value, wall: false));
            if (local.Flats.Floor.Count > 0 || local.Flats.Ceiling.Count > 0)
            {
                var flats = new Dictionary<string, object>();
                if (local.Flats.Floor.Count > 0)
                    flats["floor"] = local.Flats.Floor.OrderBy(f => f.Key).ToDictionary(f => f.Key, f => f.Value);
                if (local.Flats.Ceiling.Count > 0)
                    flats["ceiling"] = local.Flats.Ceiling.OrderBy(f => f.Key).ToDictionary(f => f.Key, f => f.Value);
                sections["flats"] = flats;
            }
            if (local.Triggers.Count > 0)
                sections["triggers"] = local.Triggers.OrderBy(t => t.Key).ToDictionary(t => t.Key, t => t.Value);
            if (local.Things.Count > 0)
                sections["things"] = local.Things.OrderBy(t => t.Key).ToDictionary(t => t.Key, t => t.Value);
            if (local.Floors.AreaCount is { } areas)
                sections["floors"] = new Dictionary<string, object> { ["area-count"] = areas };
            if (sections.Count > 0)
                text.Append('\n').Append(Yaml.Serialize(sections));
        }

        if (map.ExtraThings.Count > 0)
        {
            text.Append($"\n# Its {map.ExtraThings.Count} extra thing{(map.ExtraThings.Count == 1 ? "" : "s")}: where the object plane can't hold them\n");
            text.Append("# (off a tile's middle, a second on a tile, odd skills). The free-thing tool places and moves them.\n");
            text.Append("extra-things:\n");
            foreach (var thing in map.ExtraThings)
            {
                var skills = Enumerable.Range(1, 4).Where(s => (thing.Skills & (1 << (s - 1))) != 0);
                text.Append(CultureInfo.InvariantCulture,
                    $"  - {{ class: {thing.Class}, x: {thing.X:0.###}, y: {thing.Y:0.###}, angle: {thing.Angle}");
                if (thing.Patrol)
                    text.Append(", patrol: true");
                if (thing.Ambush)
                    text.Append(", ambush: true");
                if (thing.Skills != MapThing.AllSkills)
                    text.Append($", skills: [{string.Join(", ", skills)}]");
                text.Append(" }\n");
            }
        }
        return text.ToString();
    }

    // A wall's or door's mapdefs entry, as its YAML has it (faces capitalized, as walls.yaml)
    private static Dictionary<string, object> Faces(MapTextureTranslation def, bool wall)
    {
        var entry = new Dictionary<string, object>();
        foreach (var (name, texture) in new[] { ("North", def.North), ("South", def.South), ("East", def.East), ("West", def.West) })
            if (texture.Length > 0)
                entry[name] = texture;
        if (!wall && def.Vertical)
            entry["Vertical"] = true;
        if (def.Lock.Length > 0)
            entry["lock"] = def.Lock;
        if (def.OpenSound.Length > 0)
            entry["open-sound"] = def.OpenSound;
        if (def.CloseSound.Length > 0)
            entry["close-sound"] = def.CloseSound;
        if (def.Flip.Count > 0)
            entry["flip"] = def.Flip;
        if (def.Switch is { } wallSwitch)
            entry["switch"] = wallSwitch;
        return entry;
    }
}
