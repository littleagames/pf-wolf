using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Menus;

namespace PFWolf.Editor.Rendering;

/// <summary>
/// What the game's code puts on a menu that menudefs don't say, made up for the menu window's
/// preview: the rows an items-source builds (the game's own episodes, skills and classes; sample
/// mods and network games), the New Game menus' pictures and stats panel, and what the code-built
/// screens draw over their menus' components
/// </summary>
public static class MenuSamples
{
    /// <summary>The rows an items-source builds, as the game builds them from the loaded game's data</summary>
    /// <param name="count">How many to make up in place of the game's (to try fit-items), or null for the game's</param>
    public static ControlPanelItem[] SourceRows(GameContent content, MenuMetadata menu, string source, int? count)
    {
        var info = content.GameInfo;
        string Text(string? text) => MenuCatalog.Shown(content, text);

        ControlPanelItem[] rows = source.ToLowerInvariant() switch
        {
            "episodes" => Episodes(content, menu),
            "skills" => [.. (info?.Skills.Values ?? Enumerable.Empty<SkillInfo>()).Select(skill => new ControlPanelItem(1, Text(skill.Name), null, skill))],
            "player-classes" => [.. PlayerClasses(content).Select(pair =>
                new ControlPanelItem(1, Text(pair.Value?.Name ?? pair.Key), null, pair.Value ?? new PlayerClassInfo()))],
            "mods" => [.. Enumerable.Range(1, 6).Select(i => new ControlPanelItem(1, $"Sample mod {i}", null))],
            "lan-games" => [.. Enumerable.Range(1, 3).Select(i => new ControlPanelItem(1, $"Sample game {i} (1/4)", null))],
            _ => [],
        };

        if (count is int wanted && wanted >= 0 && wanted != rows.Length)
        {
            // Made-up rows, the first ones like the game's (their pictures too)
            var made = new List<ControlPanelItem>();
            for (int i = 0; i < wanted; i++)
            {
                var like = rows.Length > 0 ? rows[i % rows.Length] : null;
                made.Add(new ControlPanelItem(1, i < rows.Length ? rows[i].Text : $"Sample {i + 1}", null, like?.Data!));
            }
            rows = [.. made];
        }
        return rows;
    }

    // Episodes as the game lists them: a blank row after each for its second line, unless spacer-rows: false
    private static ControlPanelItem[] Episodes(GameContent content, MenuMetadata menu)
    {
        var episodes = content.GameInfo?.Episodes.Values ?? Enumerable.Empty<EpisodeInfo>();
        var rows = new List<ControlPanelItem>();
        foreach (var episode in episodes)
        {
            if (rows.Count > 0 && menu.SpacerRows != false)
                rows.Add(new ControlPanelItem(0, "", null));
            rows.Add(new ControlPanelItem(1, MenuCatalog.Shown(content, episode.Name), null, episode) { ShortKey = episode.Key });
        }
        return [.. rows];
    }

    // game-info's player classes, else the player's own (one class: the game skips the menu)
    private static IEnumerable<KeyValuePair<string, PlayerClassInfo?>> PlayerClasses(GameContent content)
        => content.GameInfo?.PlayerClasses is { Count: > 0 } classes
            ? classes.Select(pair => new KeyValuePair<string, PlayerClassInfo?>(pair.Key, pair.Value))
            : [new("Player", null)];

    /// <summary>The level menu's rows: the first episode with a level select's levels, by select-name, else name, else the map's own</summary>
    public static ControlPanelItem[] LevelRows(GameContent content)
    {
        var info = content.GameInfo;
        var episode = info?.Episodes.Values.FirstOrDefault(episode => episode.LevelSelect.Count > 0);
        if (info == null || episode == null)
            return [.. Enumerable.Range(1, 6).Select(i => new ControlPanelItem(1, $"Sample level {i}", null))];
        return [.. episode.LevelSelect.Select(map =>
        {
            var mapInfo = info.Maps.GetValueOrDefault(map);
            return new ControlPanelItem(1, MenuCatalog.Shown(content, mapInfo?.SelectName ?? mapInfo?.Name ?? map), null, map);
        })];
    }

    /// <summary>A highlighted row's picture: a skill's, a class's or an episode's pic-name</summary>
    public static string? PictureOf(ControlPanelItem row) => row.Data switch
    {
        SkillInfo skill => skill.PicName,
        PlayerClassInfo playerClass => playerClass.PicName,
        EpisodeInfo episode => episode.PicName,
        _ => null,
    };

    /// <summary>A made-up best for a stats panel: an hour and a bit, most kills, some secrets, one of each objective</summary>
    public static LevelStats Stats { get; } = new(3723, 85, 40, new(StringComparer.OrdinalIgnoreCase)
    {
        ["kilroy"] = (50, 1, 2), ["kilroys"] = (50, 1, 2), ["intel"] = (100, 3, 3), ["treasure"] = (75, 3, 4),
    });

    /// <summary>A ChoiceMenuItem's value, as one of the game's might read</summary>
    public const string ChoiceValue = "Choice";

    /// <summary>A SliderMenuItem's level, of SliderMax</summary>
    public const int SliderValue = 6, SliderMax = 9;

    /// <summary>What the game's code draws on a menu besides its rows, for the window to say (null for nothing more)</summary>
    public static string? CodeDrawn(string menu) => menu.ToLowerInvariant() switch
    {
        "load-game" or "save-game" => "The game draws the saved games' list and the highlighted one's picture over this menu's components.",
        "customize" => "The game draws the Customize controls rows (each control's keys and buttons) over this menu's components.",
        "mods" => "The game draws the mods' list over this menu (here, sample mods).",
        "join-game" => "The game lists the games found on the network (here, sample ones).",
        "lobby" => "The game draws the lobby's players and settings over this menu's components.",
        "net-rules" => "The game fills in the rules' values (here, sample values).",
        "video" or "sound" or "game-options" or "control" or "controller" => "The game fills in each toggle, slider and choice's value (here, sample ones).",
        "mouse-sensitivity" => "The game draws the sensitivity bar over this menu.",
        "jukebox" => "The game lists the music tracks.",
        _ => null,
    };
}
