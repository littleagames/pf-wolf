using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>Demos from the console: playdemo, demotest (the regression check) and recorddemo</summary>
internal sealed class DemoCommands(
    ConsoleManager console,
    MapManager map,
    Demos demoPlayback,
    DemoBuffer demo,
    GameSession session)
{
    internal void Register()
    {
        console.Register("playdemo", "Plays a demo: one recorded with that number (DEMO#.dmo in the demos folder), or else the game's own. Ends the game in progress, then goes back to the title.",
            "playdemo <0-9>", PlayDemoCommand, Solo,
            complete: (_, i) => i == 0 ? Enumerable.Range(0, 10).Where(demoPlayback.DemoExists).Select(n => n.ToString()) : []);
        console.Register("demotest", "Plays demos (all of them, or those given) back to back without waiting, and prints how each ended: score, kills, where the player and every actor finished. For checking a change to the game hasn't changed how it plays; run it from --exec.",
            "demotest [0-9 ...]", DemoTestCommand, Solo,
            complete: (_, _) => Enumerable.Range(0, 10).Where(demoPlayback.DemoExists).Select(n => n.ToString()));
        console.Register("recorddemo", "Records a demo on a level, on the hardest skill, until the level ends or you die; saves it as that demo number, or asks for one. Ends the game in progress first.",
            "recorddemo <MAP##> [0-9]", RecordDemoCommand, Solo,
            complete: (_, i) => i switch
            {
                0 => Enumerable.Range(1, demoPlayback.DemoMapCount()).Select(n => $"MAP{n:D2}"),
                1 => Enumerable.Range(0, 10).Select(n => n.ToString()),
                _ => []
            });
    }
    // Queues the demo for the title loop; in a level the game ends first (PlayLoop exits on the
    // abort, and GameLoop, seeing PendingDemo, goes back to the title without a death or scores)
    internal void PlayDemoCommand(string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out int demonumber) || demonumber < 0 || demonumber > 9)
            throw new ArgumentException("usage: playdemo <0-9>");

        if (!demoPlayback.DemoExists(demonumber))
        {
            console.Print($"There's no demo {demonumber}");
            return;
        }

        demoPlayback.PendingDemo = demonumber;
        if (session.InGame)
            session.PlayState = PlayState.Abort;
    }

    internal void DemoTestCommand(string[] args)
    {
        if (session.InGame)
        {
            console.Print("demotest runs from the title or --exec, not during a game");
            return;
        }

        var demos = args.Length > 0
            ? args.Select(a => int.TryParse(a, out var n) ? n : -1).ToList()
            : Enumerable.Range(0, 10).Where(demoPlayback.DemoExists).ToList();

        DemoTesting = true;
        try
        {
            foreach (var demonumber in demos)
            {
                if (demonumber < 0 || demonumber > 9 || !demoPlayback.DemoExists(demonumber))
                    Console.WriteLine($"demotest {demonumber}: no such demo");
                else
                    demoPlayback.PlayDemo(demonumber);
            }
        }
        finally
        {
            DemoTesting = false;
        }
    }

    /// <summary>Set while demotest runs: demos play without waiting, and each prints <see cref="DemoTestReport"/> as it ends</summary>
    internal bool DemoTesting;

    /// <summary>
    /// How a demo ended, for demotest: how far it got, the player's stats and spot, and a hash of
    /// every actor's class, position, health and state, so two builds can be compared line for line
    /// </summary>
    internal string DemoTestReport(int demonumber)
    {
        var actors = map.GetActors().Where(a => !a.IsRemoved && !ReferenceEquals(a, session.Player)).ToList();
        ulong hash = 14695981039346656037UL;    // FNV-1a
        foreach (var a in actors)
            foreach (var c in $"{a.Name},{a.X},{a.Y},{a.Hitpoints},{a.CurrentState?.StateName}|")
                hash = (hash ^ c) * 1099511628211UL;

        return $"demotest {demonumber}: data {demo.DemoPtr}/{demo.LastDemoPtr} score {session.PlayerState.Score} health {session.PlayerState.Health} "
            + $"kills {session.GameState.KillCount}/{session.GameState.KillTotal} treasure {session.GameState.TreasureCount}/{session.GameState.TreasureTotal} "
            + $"player {session.Player.X},{session.Player.Y},{session.Player.Angle} actors {actors.Count} hash {hash:x16}";
    }

    // Queued like playdemo. The map is MAP## or just its number; a demo can only name MAP01 onwards,
    // one byte's worth, so it has to be one of the maps counting up from MAP01
    internal void RecordDemoCommand(string[] args)
    {
        const string usage = "usage: recorddemo <MAP##> [0-9]";
        if (args.Length == 0)
            throw new ArgumentException(usage);

        var mapArg = args[0].StartsWith("MAP", StringComparison.OrdinalIgnoreCase) ? args[0][3..] : args[0];
        int maps = demoPlayback.DemoMapCount();
        if (!int.TryParse(mapArg, out int level) || level < 1 || level > maps)
        {
            console.Print(maps == 0 ? "No map can be recorded: there's no MAP01" : $"A demo can be recorded on MAP01 to MAP{maps:D2}");
            return;
        }

        int? demonumber = null;
        if (args.Length > 1)
        {
            if (!int.TryParse(args[1], out int number) || number < 0 || number > 9)
                throw new ArgumentException(usage);
            demonumber = number;
        }

        demoPlayback.PendingRecord = new DemoRecordRequest(level, demonumber);
        if (session.InGame)
            session.PlayState = PlayState.Abort;
    }
}
