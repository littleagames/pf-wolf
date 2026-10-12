using PFWolf.Assets;
using PFWolf.Loaders;
using YamlDotNet.Core;

namespace PFWolf.Editor.Definitions;

/// <summary>The definitions the editor edits, each a set of YAML files merged as the game merges them</summary>
public enum DefinitionKind
{
    /// <summary>The game pack's game-info.yaml (a mod's at its root): episodes, skills, levels, screens</summary>
    GameInfo,

    /// <summary>gamepacks/gamepack-info.yaml, the releases; a mod's root gamepack-info.yaml adds games</summary>
    GamePackInfo,

    /// <summary>actordefs/: the actor classes</summary>
    Actors,

    /// <summary>mapdefs/: what each map value is (walls, doors, things, floors...)</summary>
    MapDefs,

    /// <summary>animdefs/: animated textures and pictures</summary>
    Animations,
}

public static class DefinitionKinds
{
    public static IReadOnlyList<DefinitionKind> All { get; } = Enum.GetValues<DefinitionKind>();

    public static string Title(DefinitionKind kind) => kind switch
    {
        DefinitionKind.GameInfo => "Game info",
        DefinitionKind.GamePackInfo => "Game packs",
        DefinitionKind.Actors => "Actors",
        DefinitionKind.Animations => "Animations",
        _ => "Map definitions",
    };

    /// <summary>The file a mod keeps these in at its root, for the kinds that have one</summary>
    public static string? ModRootFile(DefinitionKind kind) => kind switch
    {
        DefinitionKind.GameInfo => "game-info.yaml",
        DefinitionKind.GamePackInfo => "gamepack-info.yaml",
        _ => null,
    };

    /// <summary>The folder a mod keeps these in (its files directly in it), for the kinds that have one</summary>
    public static string? ModFolder(DefinitionKind kind) => kind switch
    {
        DefinitionKind.Actors => "actordefs/",
        DefinitionKind.MapDefs => "mapdefs/",
        DefinitionKind.Animations => "animdefs/",
        _ => null,
    };

    /// <summary>Whether a mod's file (by its path in the mod) is one of this kind</summary>
    public static bool IsModFile(DefinitionKind kind, string entryPath)
    {
        var path = entryPath.Replace('\\', '/');
        if (ModRootFile(kind) is { } root)
            return path.Equals(root, StringComparison.OrdinalIgnoreCase);

        var folder = ModFolder(kind)!;
        return path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)
               && path.IndexOf('/', folder.Length) < 0
               && (path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Reads a document as the game reads this kind, throwing what's wrong when it can't (quietly:
    /// nothing goes to the warning log)
    /// </summary>
    public static void Read(DefinitionKind kind, string yaml)
    {
        switch (kind)
        {
            case DefinitionKind.GameInfo:
                YamlDataEntryLoader.DeserializeQuietly<GameInfoAsset>(yaml);
                break;
            case DefinitionKind.GamePackInfo:
                YamlDataEntryLoader.DeserializeQuietly<Dictionary<string, GamePack>>(yaml);
                break;
            case DefinitionKind.Actors:
                YamlDataEntryLoader.DeserializeQuietly<Dictionary<string, ActorData>>(yaml);
                break;
            case DefinitionKind.Animations:
                YamlDataEntryLoader.DeserializeQuietly<Dictionary<string, AnimationDefinition>>(yaml);
                break;
            default:
                YamlDataEntryLoader.DeserializeQuietly<MapObjectTranslationAsset>(yaml);
                break;
        }
    }

    /// <summary>What's wrong reading a document as this kind, or null when it reads</summary>
    public static string? Check(DefinitionKind kind, string yaml)
    {
        try
        {
            Read(kind, yaml);
            return null;
        }
        catch (YamlException e)
        {
            return Describe(e);
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException or ArgumentException or InvalidCastException)
        {
            return e.Message;
        }
    }

    /// <summary>A YAML error as a person reads it: where, then what (the innermost reason, which says the most)</summary>
    public static string Describe(YamlException e)
    {
        var reason = e.InnerException is { } inner && inner is not YamlException ? inner.Message : e.Message;
        while (e.InnerException is YamlException deeper)
        {
            e = deeper;
            reason = e.InnerException is { } innermost && innermost is not YamlException ? innermost.Message : e.Message;
        }
        return e.Start.Line > 0 ? $"Line {e.Start.Line}, column {e.Start.Column}: {reason}" : reason;
    }
}
