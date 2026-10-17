using PFWolf.Networking;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>Games with others from the console: net_find, net_portcheck, net_status and scoreboard</summary>
internal sealed class NetCommands(
    ConsoleManager console,
    MultiplayerMenu multiplayerMenu,
    Scoreboard scoreboard,
    NetPlay netPlay,
    GameSession game)
{
    internal void Register()
    {
        console.Register("net_find", "Looks for games hosted on the local network (two seconds) and lists them.", "net_find", args => multiplayerMenu.NetFind(args));
        console.Register("net_portcheck", "Checks whether players on the internet can reach a game hosted here (the UDP port, default 10645): needs the port free, so not while hosting.", "net_portcheck [port]", args => multiplayerMenu.NetPortCheck(args));
        console.Register("net_status", "Shows the game with others: the level and frame, the players, and (hosting) how many checks found every machine agreeing.", "net_status", Cmd_NetStatus);
        console.Register("scoreboard", "Shows or hides the scoreboard over the view, playing with others (bind it to a key).", "scoreboard [0|1]", args => scoreboard.Cmd_Scoreboard(args), complete: Values("0", "1"));
    }
    /// <summary>`net_status`: the game with others as this machine has it</summary>
    internal void Cmd_NetStatus(string[] args)
    {
        if (!game.netgame || NetSession.Current is not { } session)
        {
            console.Print("Not playing with others");
            return;
        }

        var lines = new List<string> { $"{(session.IsHost ? "Hosting" : "Joined")}, level {netPlay.netlevel}, frame {netPlay.netstep}, game sum {netPlay.GameChecksum():x8}" };
        for (int i = 0; i < game.players.Count; i++)
        {
            var p = game.players[i];
            lines.Add($"  {i + 1}. {p.Name}{(i == game.consoleplayer ? " (you)" : "")}: "
                + (p.Pawn is { } pawn ? $"{pawn.X},{pawn.Y} facing {pawn.Angle}, health {p.health}" : p.Gone ? "left" : "-"));
        }
        if (session.IsHost)
            lines.Add($"  {session.SumsMatched} of {session.SumsCompared} checks matched");

        foreach (var line in lines)
        {
            console.Print(line);
            Console.WriteLine($"net_status: {line}");
        }
    }
}
