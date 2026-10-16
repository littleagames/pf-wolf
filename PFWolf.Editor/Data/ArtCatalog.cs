using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Editor.Data;

/// <summary>The kinds of picture the art browser lists</summary>
public enum ArtKind
{
    /// <summary>A wall texture: a VSWAP wall, or a pack's textures/ picture of any size</summary>
    Texture,
    /// <summary>A floor or ceiling texture from a pack's flats/ folder</summary>
    Flat,
    Sprite,
    /// <summary>A VGA graphic or a pack's graphics/ picture: title screens, the status bar, menus</summary>
    Picture,
}

/// <summary>
/// Where the palette can put down what uses a picture: a plane and the value on it. Keep is the
/// bits of the value picked before that stay (a flat's other half: the ceiling for a floor).
/// </summary>
public sealed record PaletteTarget(int Plane, ushort Value, ushort Keep = 0, bool Patch = false);

/// <summary>Something that shows a picture: a mapdefs wall or door, a flat index, a thing class, a level's sky</summary>
public sealed record ArtUse(string Label, PaletteTarget? Target = null)
{
    public override string ToString() => Label;
}

/// <summary>
/// One picture in the game's assets, where it came from and what shows it. A sprite frame seen
/// differently from each side is one entry named by its frame ("GARDA"), its sprites ("GARDA1" to
/// "GARDA8") in <see cref="Rotations"/>, and its size and origins those of the first.
/// </summary>
public sealed record ArtEntry(ArtKind Kind, string Name, int Width, int Height, IReadOnlyList<AssetOrigin> Origins, IReadOnlyList<ArtUse> Uses)
{
    /// <summary>A rotating sprite frame's sprites, side 1 (facing the viewer) first; empty for anything else</summary>
    public IReadOnlyList<string> Rotations { get; init; } = [];

    /// <summary>For an animdefs animation, its frames; null for a picture that isn't one</summary>
    public ArtAnimation? Animation { get; init; }

    /// <summary>The asset shown for the entry: the picture itself, or a rotating frame's first side</summary>
    public string AssetName => Rotations.Count > 0 ? Rotations[0] : Name;

    /// <summary>Whether this is the entry for a picture of this name, or has it among its sides</summary>
    public bool Holds(string assetName)
        => Name.Equals(assetName, StringComparison.OrdinalIgnoreCase)
            || Rotations.Any(rotation => rotation.Equals(assetName, StringComparison.OrdinalIgnoreCase));

    /// <summary>The file or pack the picture in use comes from</summary>
    public string Source => Origins.LastOrDefault(origin => origin.Action != AssetOrigin.LeftOut)?.Source ?? "";

    /// <summary>Whether a mod (or the game's own pk3) replaced the data files' picture</summary>
    public bool IsReplaced => Origins.Count(origin => origin.Action != AssetOrigin.LeftOut) > 1;

    public override string ToString() => Name;
}

/// <summary>An animdefs animation as the art browser shows it: its frames' names and tics, and whether it loops</summary>
public sealed record ArtAnimation(IReadOnlyList<(string Name, int Tics)> Frames, bool Loops)
{
    /// <summary>How long a preview holds the last frame of one that plays once, before it starts over</summary>
    public const int HoldTics = 70;

    public int TotalTics => Frames.Sum(frame => frame.Tics);

    /// <summary>
    /// The frame a preview shows <paramref name="tics"/> after it started: round and round, or
    /// for one that plays once, through to its last frame, held a moment, then over again
    /// </summary>
    public int FrameAt(int tics)
    {
        int length = TotalTics + (Loops ? 0 : HoldTics);
        if (length <= 0 || Frames.Count == 0)
            return 0;
        tics %= length;
        for (int i = 0; i < Frames.Count; i++)
        {
            tics -= Frames[i].Tics;
            if (tics < 0)
                return i;
        }
        return Frames.Count - 1;
    }

    /// <summary>The animation of this name and kind (a texture's or a picture's), or null when it's a plain one</summary>
    public static ArtAnimation? Of(GameContent content, string name, bool picture)
    {
        static ArtAnimation Make<T>(AnimatedAsset<T> animation) where T : Asset
            => new(animation.Frames.Select(frame => (frame.Name.ToUpperInvariant(), frame.Tics)).ToList(), animation.Loops);

        if (picture)
            return content.Assets.FindAnimation<GraphicAsset>(name) is { } pictures ? Make(pictures) : null;
        return content.Assets.FindAnimation<TextureAsset>(name) is { } textures ? Make(textures) : null;
    }
}

/// <summary>
/// Every wall texture, flat, sprite and picture the game's assets hold, as the engine merged them,
/// with what in mapdefs, actordefs and game-info uses each
/// </summary>
public static class ArtCatalog
{
    public static List<ArtEntry> Build(GameContent content)
    {
        var uses = Uses(content);
        var spriteUses = SpriteUses(content);
        var entries = new List<ArtEntry>();
        var spriteNames = new List<string>();

        foreach (var name in content.Assets.AssetNames.Order(StringComparer.OrdinalIgnoreCase))
        {
            var display = name.ToUpperInvariant();
            if (content.Find<TextureAsset>(name) is { } texture)
            {
                var origins = Origins(content, name, nameof(TextureAsset));
                // An animation (the editor shows its first frame) is a flat when its first frame is one
                var animation = ArtAnimation.Of(content, name, picture: false);
                var kindOrigins = animation != null ? Origins(content, animation.Frames[0].Name, nameof(TextureAsset)) : origins;
                var kind = kindOrigins.Any(origin => IsFlatPath(origin.Path)) ? ArtKind.Flat : ArtKind.Texture;
                entries.Add(new ArtEntry(kind, display, texture.Width, texture.Height, origins, uses.GetValueOrDefault(display, [])) { Animation = animation });
            }
            if (content.Assets.Exists<SpriteAsset>(name))
                spriteNames.Add(display);
            if (content.Find<GraphicAsset>(name) is { } picture)
            {
                entries.Add(new ArtEntry(ArtKind.Picture, display, picture.Width, picture.Height, Origins(content, name, nameof(GraphicAsset)),
                    uses.GetValueOrDefault(display, [])) { Animation = ArtAnimation.Of(content, name, picture: true) });
            }
        }

        // A rotating frame's sides make one entry
        foreach (var (name, rotations) in GroupRotations(spriteNames))
        {
            var first = rotations.Count > 0 ? rotations[0] : name;
            if (content.Find<SpriteAsset>(first) is not { } sprite)
                continue;
            var frame = rotations.Count > 0 ? name : SpriteFrame(name);
            entries.Add(new ArtEntry(ArtKind.Sprite, name, sprite.Width, sprite.Height, Origins(content, first, nameof(SpriteAsset)),
                frame != null ? spriteUses.GetValueOrDefault(frame, []) : []) { Rotations = rotations });
        }

        return entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Kind).ToList();
    }

    /// <summary>
    /// Sprite names as the browser lists them: a frame seen from sides 1 to 8 ("GARDA1"…"GARDA8")
    /// as one, named by its frame ("GARDA") with its sides in order; anything else on its own with none
    /// </summary>
    public static List<(string Name, IReadOnlyList<string> Rotations)> GroupRotations(IEnumerable<string> spriteNames)
    {
        var names = spriteNames.Select(name => name.ToUpperInvariant()).Distinct().ToList();
        var rotating = names
            .Where(name => name.Length >= 2 && name[^1] is >= '1' and <= '8')
            .GroupBy(name => name[..^1])
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.OrderBy(name => name[^1]).ToList());

        var grouped = new List<(string, IReadOnlyList<string>)>();
        foreach (var name in names)
        {
            if (name.Length >= 2 && name[^1] is >= '1' and <= '8')
                continue;
            grouped.Add((name, []));
        }
        foreach (var (frame, rotations) in rotating)
        {
            // A lone "1" isn't a set of sides (a sprite that's just numbered), so it stays as it is
            if (rotations.Count > 1)
                grouped.Add((frame, rotations));
            else
                grouped.Add((rotations[0], []));
        }
        return grouped;
    }

    /// <summary>Where a picture of this name and asset type came from: the data files, then each pack that replaced it</summary>
    public static IReadOnlyList<AssetOrigin> Origins(GameContent content, string name, string type)
        => content.Assets.FindAssetOrigins(name)
               .FirstOrDefault(asset => asset.Type == type || asset.Type == $"{type} (animated)").Origins ?? [];

    /// <summary>A sprite's name without the rotation digit on the end ("GARDA1" → "GARDA"): what actordefs names a frame by</summary>
    public static string? SpriteFrame(string spriteName)
        => spriteName.Length >= 2 && char.IsAsciiDigit(spriteName[^1]) ? spriteName[..^1].ToUpperInvariant() : null;

    /// <summary>Whether a pack file is somewhere in the flats/ folder ("flats/FLOOR1.png", "flats/tech/FLOOR2.png"), not textures/</summary>
    public static bool IsFlatPath(string path)
        => path.Replace('\\', '/').StartsWith("flats/", StringComparison.OrdinalIgnoreCase);

    /// <summary>Textures and pictures by name: the walls, doors, patches and flats of mapdefs, and game-info's skies and default flats</summary>
    public static Dictionary<string, List<ArtUse>> Uses(GameContent content)
    {
        var uses = new Dictionary<string, List<ArtUse>>(StringComparer.OrdinalIgnoreCase);
        void Add(string? name, ArtUse use)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            if (!uses.TryGetValue(name, out var list))
                uses[name] = list = [];
            if (!list.Contains(use))
                list.Add(use);
        }

        var defs = content.MapDefs;
        foreach (var (id, wall) in defs.Walls.OrderBy(wall => wall.Key))
        {
            var target = id is > 0 and <= ushort.MaxValue ? new PaletteTarget(0, (ushort)id) : null;
            var label = wall.Switch != null ? $"Switch wall {id}" : $"Wall {id}";
            foreach (var side in Sides(wall))
                Add(side.Texture, new ArtUse($"{label}, {side.Name}", target));
        }

        foreach (var (id, door) in defs.Doors.OrderBy(door => door.Key))
        {
            var target = id is > 0 and <= ushort.MaxValue ? new PaletteTarget(0, (ushort)id) : null;
            foreach (var side in Sides(door))
                Add(side.Texture, new ArtUse($"Door {id}, {side.Name}", target));
            if (door.Locked is { } locked)
            {
                foreach (var side in Sides(locked))
                    Add(side.Texture, new ArtUse($"Door {id} while locked, {side.Name}", target));
            }
        }

        foreach (var (id, patch) in defs.Patches.Where(patch => patch.Key is >= 1 and <= 255).OrderBy(patch => patch.Key))
        {
            var target = new PaletteTarget(0, (ushort)(id << PFWolf.Constants.MapConstants.PATCHSHIFT), Patch: true);
            Add(patch.Texture, new ArtUse(patch.Switch != null ? $"Switch patch {id}" : $"Patch {id}", target));
            if (patch.TextureEw.Length > 0)
                Add(patch.TextureEw, new ArtUse($"Patch {id} east/west", target));
        }

        foreach (var (index, name) in defs.Flats.Floor.Where(flat => flat.Key is >= 0 and <= 255).OrderBy(flat => flat.Key))
            Add(name, new ArtUse($"Floor flat {index}", new PaletteTarget(MapFlatPlane, (ushort)index, Keep: 0xff00)));
        foreach (var (index, name) in defs.Flats.Ceiling.Where(flat => flat.Key is >= 0 and <= 255).OrderBy(flat => flat.Key))
            Add(name, new ArtUse($"Ceiling flat {index}", new PaletteTarget(MapFlatPlane, (ushort)(index << 8), Keep: 0x00ff)));

        // An animation's frames are used by it
        foreach (var name in content.Assets.Animations.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            var animation = ArtAnimation.Of(content, name, picture: false) ?? ArtAnimation.Of(content, name, picture: true);
            for (int i = 0; i < (animation?.Frames.Count ?? 0); i++)
                Add(animation!.Frames[i].Name, new ArtUse($"Animation {name.ToUpperInvariant()}, frame {i + 1}"));
        }

        var defaults = content.DefaultMapInfo;
        Add(defaults.Sky, new ArtUse("Every level's sky"));
        Add(defaults.DefaultFloor, new ArtUse("Every level's floor"));
        Add(defaults.DefaultCeiling, new ArtUse("Every level's ceiling"));
        foreach (var (mapName, info) in content.GameInfo?.Maps ?? [])
        {
            var level = mapName.ToUpperInvariant();
            Add(info.Sky, new ArtUse($"{level}'s sky"));
            Add(info.DefaultFloor, new ArtUse($"{level}'s floor"));
            Add(info.DefaultCeiling, new ArtUse($"{level}'s ceiling"));
        }

        return uses;
    }

    private const int MapFlatPlane = PFWolf.Constants.MapConstants.FLATPLANE;

    private static IEnumerable<(string Name, string Texture)> Sides(MapTextureTranslation sides)
    {
        // North and south match, and east and west, in most of mapdefs: list a texture once per pair
        if (sides.North.Equals(sides.South, StringComparison.OrdinalIgnoreCase))
            yield return ("north and south", sides.North);
        else
        {
            yield return ("north", sides.North);
            yield return ("south", sides.South);
        }

        if (sides.East.Equals(sides.West, StringComparison.OrdinalIgnoreCase))
            yield return ("east and west", sides.East);
        else
        {
            yield return ("east", sides.East);
            yield return ("west", sides.West);
        }
    }

    /// <summary>
    /// Sprite frames ("GARDA") by the thing classes whose states show them, each class with its
    /// first object code (when mapdefs places it) for the palette
    /// </summary>
    public static Dictionary<string, List<ArtUse>> SpriteUses(GameContent content)
    {
        var codes = PaletteEntries.ThingVariants(content.MapDefs);
        var uses = new Dictionary<string, List<ArtUse>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (className, actor) in content.Actors.OrderBy(actor => actor.Key, StringComparer.OrdinalIgnoreCase))
        {
            var target = codes.TryGetValue(className, out var variants) ? new PaletteTarget(1, variants[0].Id) : null;
            var use = new ArtUse(target != null ? $"{className}  (thing {variants![0].Id})" : className, target);
            foreach (var frame in actor.States.Values.SelectMany(states => states).OfType<ActorStatesData>())
            {
                if (string.IsNullOrWhiteSpace(frame.Sprite))
                    continue;
                foreach (var letter in frame.Frames)
                {
                    var key = (frame.Sprite + letter).ToUpperInvariant();
                    if (!uses.TryGetValue(key, out var list))
                        uses[key] = list = [];
                    if (!list.Contains(use))
                        list.Add(use);
                }
            }
        }
        return uses;
    }
}
