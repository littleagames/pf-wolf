using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Definitions;
using PFWolf.Loaders;

namespace PFWolf.DocGen;

/// <summary>A definition file as the documentation shows it: where it goes, and its schema</summary>
/// <param name="Id">Its page's name (game-info)</param>
/// <param name="Title">The file as it's named (game-info.yaml, actordefs/*.yaml)</param>
/// <param name="Summary">What it holds, in a sentence</param>
/// <param name="ModPath">Where a mod keeps it; null when mods can't carry it</param>
/// <param name="BasePath">Where pfwolf.pk3 keeps it ({pack} is the game pack's id)</param>
/// <param name="Schema">Its fields</param>
/// <param name="Api">
/// Things that are called or typed (actions, script functions, commands, options), each with its
/// signature, shown as entries of their own rather than a table of fields
/// </param>
public sealed record DefinitionFile(string Id, string Title, string Summary, string? ModPath, string? BasePath, Func<FieldSchema> Schema,
    bool Api = false);

/// <summary>
/// The YAML files the game reads definitions from, and the types it reads them as (as
/// PfWolfPk3Loader.LoadEntry and the editor's YamlFileTypes read them)
/// </summary>
public static class DefinitionFiles
{
    public static IReadOnlyList<DefinitionFile> All { get; } =
    [
        new("modinfo", "modinfo.yaml", "About the mod: its name, version, author and the games it's made for.",
            "modinfo.yaml", null, () => FieldSchema.For(typeof(ModInfo))),
        new("gamepack-info", "gamepack-info.yaml", "The games (game packs) there are. A mod's adds games of its own; it can't change pfwolf.pk3's. How a game's files combine with those of the games it's built on is in the manual's How files combine.",
            "gamepack-info.yaml", "gamepacks/gamepack-info.yaml", () => Entries("Games by id", typeof(GamePack))),
        new("game-info", "game-info.yaml", "The game's episodes, levels, skills, screens and game-wide settings.",
            "game-info.yaml", "gamepacks/{pack}/game-info.yaml", () => FieldSchema.For(typeof(GameInfoAsset))),
        new("factions", "factions.yaml", "The sides actors are on, and how each treats the others.",
            "factions.yaml", "gamepacks/{pack}/factions.yaml", () => FieldSchema.For(typeof(FactionsAsset))),
        new("actordefs", "actordefs/*.yaml", "Actor classes: the player, enemies, items, decorations and projectiles.",
            "actordefs/*.yaml", "actordefs/{pack}/*.yaml", () => FieldSchema.Entries("", "Actor classes by name", ActorSchema.Build())),
        new("actor-properties", "Actor properties", "The properties an actor class can set under properties:, and what each does.",
            "actordefs/*.yaml", "actordefs/{pack}/*.yaml", ActorProperties.Schema),
        new("actor-flags", "Actor flags", "The flags an actor class can have under flags:, and what each does.",
            "actordefs/*.yaml", "actordefs/{pack}/*.yaml", ActorFlags.Schema),
        new("actions", "Actions", "The thinks and actions actor states and map switches can run, and what each does.",
            "actordefs/*.yaml", "actordefs/{pack}/*.yaml", GameActions.Schema, Api: true),
        new("scripting", "Lua scripting", "What a mod's Lua scripts can use: the game table, actors' fields and methods, and Lua's own functions.",
            "scripts/*.lua", null, Scripting.ScriptApi.Schema, Api: true),
        new("console", "Console commands", "The commands the in-game console runs, and what each does.",
            null, null, ConsoleCommandsSchema.Build, Api: true),
        new("command-line", "Command-line options", "The options PFWolf starts with: the game, mods, a level to warp to, video, and more.",
            null, null, CommandLineSchema.Build, Api: true),
        new("mapdefs", "mapdefs/*.yaml", "What each value on a map's planes is: walls, doors, things, floors and more.",
            "mapdefs/*.yaml", "mapdefs/{pack}/*.yaml", () => FieldSchema.For(typeof(MapObjectTranslationAsset))),
        new("animdefs", "animdefs/*.yaml", "Animated textures and pictures.",
            "animdefs/*.yaml", "animdefs/{pack}/*.yaml", () => FieldSchema.Entries("", "Animations by name", AnimationSchema.Build())),
        new("graphicdefs", "graphicdefs/*.yaml", "Pictures cut out of sprites, walls and other pictures.",
            "graphicdefs/*.yaml", "graphicdefs/{pack}/*.yaml", () => FieldSchema.Entries("", "Pictures by name", GraphicSchema.Build())),
        new("colors", "colors.yaml", "Named colors for the menus, automap and other screens.",
            "colors.yaml", "gamepacks/{pack}/colors.yaml", () => Entries("Colors by name: #RRGGBB or a palette index", typeof(string))),
        new("fonts", "fonts.yaml", "The game's fonts.",
            "fonts.yaml", "gamepacks/{pack}/fonts.yaml", () => Entries("Fonts by name", typeof(FontDefinition))),
        new("hud-messages", "hud-messages.yaml", "How on-screen messages look.",
            "hud-messages.yaml", "gamepacks/{pack}/hud-messages.yaml", () => Entries("Message styles by name", typeof(HudMessageStyleDefinition))),
        new("translations", "translations.yaml", "Sprite color translations: recolorings of the palette.",
            "translations.yaml", "gamepacks/{pack}/translations.yaml", () => Entries("Translations by name", typeof(List<string>))),
        new("statusbar", "statusbar.yaml", "The status bar's layout. statusbar-fullscreen.yaml is laid out the same way, for the full-screen view.",
            "statusbar.yaml", "gamepacks/{pack}/statusbar.yaml", () => Entries("Status bar elements by name", typeof(StatusBarElement))),
        new("intermission", "intermission.yaml", "The screens between levels and at the end of an episode.",
            "intermission.yaml", "gamepacks/{pack}/intermission.yaml", () => FieldSchema.For(typeof(IntermissionAsset))),
        new("movies", "movies.yaml", "Movies the game plays.",
            "movies.yaml", "gamepacks/{pack}/movies.yaml", () => Entries("Movies by name", typeof(MovieInfo))),
        new("alias", "alias.yaml", "Other names for the game's assets.",
            "alias.yaml", "gamepacks/{pack}/alias.yaml", () => FieldSchema.For(typeof(AliasAsset))),
        new("raw-data-map", "raw-data-map.yaml", "The names of the assets in the game's original data files.",
            "raw-data-map.yaml", "gamepacks/{pack}/raw-data-map.yaml", () => FieldSchema.For(typeof(RawDataMapAsset))),
        new("presenter", "presenter.yaml", "The presenter screens.",
            null, "gamepacks/{pack}/presenter.yaml", () => FieldSchema.For(typeof(PresenterAsset))),
        new("elevator", "elevator.yaml", "The elevator.",
            null, "gamepacks/{pack}/elevator.yaml", () => FieldSchema.For(typeof(ElevatorAsset))),
        new("sound-seq", "sounds/sound-seq.yaml", "The game's sounds by name, and the files and data they come from.",
            "sounds/sound-seq.yaml", "sounds/sound-seq.yaml", () => FieldSchema.For(typeof(SoundSequenceAsset))),
        new("menudefs", "menudefs/*.yaml", "The menus. A mod's are merged over the game's by their items' and components' ids.",
            "menudefs/*.yaml", "menudefs/*.yaml", () => FieldSchema.For(typeof(MenuAsset))),
        new("language", "language/*.yaml", "The game's text, by string id, one file per language (en-us.yaml).",
            "language/*.yaml", "gamepacks/{pack}/language/*.yaml", () => Entries("Text by string id", typeof(string))),
    ];

    // A file that's a mapping of names to one kind of value
    private static FieldSchema Entries(string description, Type value) => FieldSchema.Entries("", description, FieldSchema.For(value));
}
