using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>Sides and colors from the console: factions, setfaction, settranslation, translations and playercolor</summary>
internal sealed class FactionCommands(
    ConsoleManager console,
    AssetManager assets,
    MapManager map,
    FactionManager factions,
    TranslationManager translations,
    ConsoleTargets targets,
    GameSession session)
{
    internal void Register()
    {
        console.Register("factions", "Lists the factions (factions.yaml), how each treats the others, and who's on each in the level.",
            "factions", Cmd_Factions);
        console.Register("setfaction", "Puts the nearest living enemy, or every actor of a class, on a faction (for trying factions out).",
            "setfaction <faction> [class]", Cmd_SetFaction, Cheat | InLevel,
            complete: (_, i) => i == 0 ? factions.Table.Names : i == 1 ? assets.GetActorMetadata().Actors.Keys : []);
        console.Register("settranslation", "Draws the nearest living enemy, or every actor of a class, through a color translation (translations.yaml); none goes back to its own (or its faction's).",
            "settranslation <translation|none> [class]", Cmd_SetTranslation, Cheat | InLevel,
            complete: (_, i) => i == 0 ? translations.Names.Append("none") : i == 1 ? assets.GetActorMetadata().Actors.Keys : []);
        console.Register("translations", "Lists the color translations (translations.yaml).", "translations",
            _ => console.Print(string.Join(", ", translations.Names.DefaultIfEmpty("(none)"))));
        console.Register("playercolor", "Shows a player (default: you) in a color (a translation, as game-info player-colors offers); none for their class's own.",
            "playercolor <color|none> [n]", Cmd_PlayerColor, Cheat | InLevel,
            complete: (_, i) => i == 0 ? translations.Names.Append("none") : []);
    }
    internal void Cmd_Factions(string[] args)
    {
        var table = factions.Table;

        // Who's on what side in the level: the living players and enemies, by faction
        var members = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (map.Player != null)
        {
            foreach (var actor in map.GetActors())
            {
                bool alive = actor is Entities.Actors.PlayerPawn pawn ? pawn.State.health > 0
                    : actor is Entities.Actors.Monster && actor.RuntimeFlags.HasFlag(ObjFlags.Shootable);
                if (alive && !actor.IsRemoved && actor.Faction != null)
                    members[actor.Faction] = members.GetValueOrDefault(actor.Faction) + 1;
            }
        }

        var names = table.Names.Concat(members.Keys).Append(table.PlayerFaction).Append(table.MonsterFaction)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        console.Print($"Players are {table.PlayerFaction} and enemies {table.MonsterFaction} unless their class says otherwise"
            + (table.Names.Count == 0 ? " (no factions.yaml)" : ""));
        foreach (var faction in names)
        {
            var relations = names.Where(other => !other.Equals(faction, StringComparison.OrdinalIgnoreCase))
                .Select(other => $"{other} {table.Relation(faction, other).ToString().ToLowerInvariant()}");
            var shown = table.DisplayName(faction);
            console.Print($"  {faction}{(shown != faction ? $" ({shown})" : "")}: {members.GetValueOrDefault(faction)} in the level"
                + (relations.Any() ? $"; {string.Join(", ", relations)}" : ""));
        }
    }

    internal void Cmd_SetFaction(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: setfaction <faction> [class]");
        var faction = args[0];
        if (!factions.Table.IsKnown(faction))
            console.Print($"{faction} isn't in factions.yaml; it's a side of its own");

        var chosen = targets.ChosenActors(args.Skip(1).FirstOrDefault());
        foreach (var monster in chosen)
            monster.SetFaction(faction);
        console.Print(chosen.Count == 1
            ? $"{chosen[0].Name} at {chosen[0].TileX},{chosen[0].TileY} is now {faction}"
            : $"{chosen.Count} {chosen[0].Name}s are now {faction}");
    }

    internal void Cmd_SetTranslation(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: settranslation <translation|none> [class]");
        string? translation = args[0].Equals("none", StringComparison.OrdinalIgnoreCase) ? null : args[0];
        if (translation != null && !translations.Exists(translation))
            throw new ArgumentException($"no translation {translation} in translations.yaml ({string.Join(", ", translations.Names)})");

        var chosen = targets.ChosenActors(args.Skip(1).FirstOrDefault());
        foreach (var monster in chosen)
            monster.Translation = translation;
        console.Print($"{(chosen.Count == 1 ? $"{chosen[0].Name} at {chosen[0].TileX},{chosen[0].TileY} is" : $"{chosen.Count} {chosen[0].Name}s are")} "
            + (translation == null ? "back to their own colors" : $"drawn through {translation}"));
    }

    internal void Cmd_PlayerColor(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: playercolor <color|none> [n]");
        string? color = args[0].Equals("none", StringComparison.OrdinalIgnoreCase) ? null : args[0];
        if (color != null && !translations.Exists(color))
            throw new ArgumentException($"no translation {color} in translations.yaml ({string.Join(", ", translations.Names)})");
        int n = args.Length > 1 ? ParseInt(args[1], 1, session.players.Count) : session.consoleplayer + 1;

        session.players[n - 1].Color = color;
        console.Print($"Player {n} is {(color == null ? "in their class's own colors" : $"drawn in {color}")}");
    }
}
