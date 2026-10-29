using PFWolf.Configuration;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>
/// The Game Options menu's settings (Configuration/GameOptions.cs, saved in config.cfg): freelook,
/// am_stats, autosave, weaponbob, alwaysrun and crosshair. Each takes default to go back to the
/// game pack's choice. msg_enabled is with the other message commands.
/// </summary>
internal sealed class GameOptionCommands(ConsoleManager console, GameOptions options)
{
    internal void Register()
    {
        Add(GameOption.Look, "Whether you can look up and down at all: the look keys, a controller's right stick and mouse look (m_look).");
        Add(GameOption.AutomapStats, "Whether the automap shows the level's name, kills, treasure, secrets and time.");
        Add(GameOption.Autosave, "Whether each new level saves itself as it starts, to the Autosave.");
        Add(GameOption.WeaponBob, "Whether the weapon in hand bobs as you walk.");
        Add(GameOption.WeaponBobSway, "Whether the weapon bob swings side to side, as Doom's; 0 bobs it up and down, as Planet Strike's.");
        Add(GameOption.AlwaysRun, "Whether you run without holding Run; holding it then walks.");
        Add(GameOption.Crosshair, "Whether a crosshair is drawn in the middle of the view.");
        Add(GameOption.Lighting, "Whether levels are shaded (fog, night, light zones); 0 draws everything at full light.");
        Add(GameOption.AttackInfo, "Whether who's attacking you is shown as a message (game-info attack-message: Blake Stone's REBA).");
        Add(GameOption.ShowCeilings, "Whether ceiling flats are drawn; 0 draws the plain ceiling color.");
        Add(GameOption.ShowFloors, "Whether floor flats are drawn; 0 draws the plain floor color.");
    }

    void Add(GameOption option, string help)
    {
        var name = GameOptions.CommandName(option);
        console.Register(name, help + " default goes back to the game's own choice.", $"{name} [0|1|default]",
            args => SetOrShow(option, args), complete: Values("0", "1", "default"));
    }

    internal void SetOrShow(GameOption option, string[] args)
    {
        if (args.Length > 0)
            options.SetChoice(option, ParseChoice(args[0]));

        console.Print($"{GameOptions.CommandName(option)} is {(options[option] ? 1 : 0)}" +
            (options.Choice(option) == null ? " (the game's default)" : ""));
    }
}
