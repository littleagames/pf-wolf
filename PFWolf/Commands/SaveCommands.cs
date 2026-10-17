using PFWolf.SaveGames;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>Saved games from the console: saves, autosave, save and load</summary>
internal sealed class SaveCommands(
    ConsoleManager console,
    GameEngineManager engine,
    GameSaves gameSaves,
    PlayScreen playScreen,
    GameSession session)
{
    internal void Register()
    {
        console.Register("saves", "Lists the saved games, newest first, numbered for load.", "saves", Cmd_Saves);
        console.Register("autosave", "Whether each new level saves itself as it starts, to the Autosave.", "autosave [0|1]",
            Cmd_AutoSave, complete: Values("0", "1"));
        console.Register("save", "Saves the game. A name that's already saved is saved over; with none, it's a new save named for the level.",
            "save [name]", Cmd_Save, InLevel | Solo, complete: CompleteSaveName);
        console.Register("load", "Loads a saved game, by its number in saves or its name.", "load <number|name>", Cmd_Load, InLevel | Solo,
            complete: CompleteSaveName);
    }
    internal IEnumerable<string> CompleteSaveName(string[] args, int index) =>
        index == 0 ? gameSaves.ListSaveGames().Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase) : [];

    internal void Cmd_Saves(string[] args)
    {
        var saves = gameSaves.ListSaveGames();
        if (saves.Count == 0)
        {
            console.Print($"No saved games in {engine.ConfigDirectories.SaveGameDirectory}");
            return;
        }

        for (int i = 0; i < saves.Count; i++)
        {
            var s = saves[i];
            var kind = s.Kind == SaveKind.Normal ? "" : $" [{s.Kind.ToString().ToLowerInvariant()}]";
            console.Print($"{i + 1,3}. {s.Name}{kind} - {s.MapName}, {s.SavedAt.ToLocalTime():yyyy-MM-dd HH:mm}, played {SaveFile.FormatPlayTime(s.PlayTime)}");
        }
    }


    internal void Cmd_AutoSave(string[] args)
    {
        if (args.Length > 0)
            gameSaves.autosaveEnabled = ParseBool(args[0]);

        console.Print($"autosave is {(gameSaves.autosaveEnabled ? 1 : 0)}");
    }

    // A save by its number in the saves list or its name (the newest, if several share it)
    internal SaveInfo FindSaveGame(string arg)
    {
        var saves = gameSaves.ListSaveGames();
        if (int.TryParse(arg, out var number))
        {
            if (number < 1 || number > saves.Count)
                throw new ArgumentException($"there's no save {number}; saves lists them");
            return saves[number - 1];
        }

        return saves.FirstOrDefault(s => string.Equals(s.Name, arg, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"there's no save called \"{arg}\"; saves lists them");
    }

    internal void Cmd_Save(string[] args)
    {
        var name = args.Length > 0 ? string.Join(' ', args) : playScreen.GetMapDisplayName(session.gamestate.mapon);
        if (name.Length > GameConstants.MaxGameName - 1)
            name = name[..(GameConstants.MaxGameName - 1)];

        var path = args.Length > 0
            && gameSaves.ListSaveGames().FirstOrDefault(s => s.Kind == SaveKind.Normal
                && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) is { } existing
            ? existing.Path
            : gameSaves.NewSaveGamePath();

        if (!gameSaves.SaveTheGame(path, name, 0, 0))
            throw new ArgumentException($"couldn't write {path}");

        console.Print($"Saved \"{name}\" to {Path.GetFileName(path)}");
    }

    internal void Cmd_Load(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: load <number|name>");

        var save = FindSaveGame(string.Join(' ', args));

        // Between frames, not mid-command: the load replaces every actor, the player included.
        console.Defer(() =>
        {
            session.loadedgame = true;
            if (gameSaves.LoadTheGame(save.Path, 0, 0))
                session.playstate = PlayState.ex_abort;    // GameLoop redraws and restarts music for the loaded level
            else
                session.loadedgame = false;
        });
    }
}
