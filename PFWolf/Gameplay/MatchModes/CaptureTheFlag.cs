using PFWolf.Entities.Actors;
using PFWolf.Managers;

namespace PFWolf.Gameplay.MatchModes;

/*
=============================================================================

                            CAPTURE THE FLAG

    Each team's flag stands at its base (a RedFlagBase / BlueFlagBase marker,
    mapdefs 440/441; without them, the two open tiles farthest apart). Run
    over the other team's flag to take it: you're drawn carrying it (PLFG),
    the banner in its color. Bring it to your own base while your own flag is
    there and it's a capture, for your team; the flag goes home. Die carrying
    it and it drops where you fell: the other team runs over it to take it on,
    yours to send it home, and it goes home by itself after ReturnTics.

=============================================================================
*/

internal sealed class CaptureTheFlag(MapManager map, GameSession session, Deathmatch match, HudMessageManager hud, MatchModeHooks hooks)
    : MatchObjective(map, session, match, hud, hooks), IMatchObjective     // (named again: its own PlayerDied, TeamHome, StatusLine)
{
    public override GameMode Mode => GameMode.CaptureTheFlag;

    /// <summary>How long a dropped flag lies before it goes home by itself (tics)</summary>
    internal const int ReturnTics = 30 * 70;

    /// <summary>The flag carrier's sprite (sprites/wolf3d/player/flag)</summary>
    internal const string CarrySprite = "PLFG";

    internal enum FlagState { Home, Carried, Dropped }

    /// <summary>A team's flag: where its base is, where it is now, and who has it</summary>
    internal sealed class Flag
    {
        public (int x, int y) Home;
        public FlagState State;
        public (int x, int y) At;           // where it lies, dropped
        public int Carrier = -1;            // the player carrying it (their Number)
        public int DroppedAt;               // when it was dropped (GameState.TimeCount)
    }

    internal readonly Flag[] Flags = [new(), new()];

    static readonly string[] FlagClasses = ["RedFlag", "BlueFlag"];
    static readonly string[] BaseClasses = ["RedFlagBase", "BlueFlagBase"];

    public override void LevelSetup()
    {
        // The bases: the map's markers, else each team's starts' middle, else the farthest apart
        var floor = OpenFloor();
        var homes = new (int x, int y)?[2];
        for (int team = 0; team < 2; team++)
        {
            if (Markers(BaseClasses[team]) is { Count: > 0 } placed)
                homes[team] = placed[0];
            else if (Map.TeamStarts[team] is { Count: > 0 } starts)
                homes[team] = NearestOf(floor, Middle(starts.Select(s => (s.TileX, s.TileY)).ToList()));
        }
        if (homes[0] == null || homes[1] == null)
        {
            if (FarthestPair(floor) is { } pair)
            {
                // red takes the one nearer the top left, unless it has a base already
                var (a, b) = pair.a.y * 1000 + pair.a.x <= pair.b.y * 1000 + pair.b.x ? (pair.a, pair.b) : (pair.b, pair.a);
                if (homes[1] != null)
                    homes[0] ??= Math.Abs(a.x - homes[1]!.Value.x) + Math.Abs(a.y - homes[1]!.Value.y) >= Math.Abs(b.x - homes[1]!.Value.x) + Math.Abs(b.y - homes[1]!.Value.y) ? a : b;
                else if (homes[0] != null)
                    homes[1] = Math.Abs(a.x - homes[0]!.Value.x) + Math.Abs(a.y - homes[0]!.Value.y) >= Math.Abs(b.x - homes[0]!.Value.x) + Math.Abs(b.y - homes[0]!.Value.y) ? a : b;
                else
                    (homes[0], homes[1]) = (a, b);
            }
        }

        for (int team = 0; team < 2; team++)
        {
            Flags[team].Home = homes[team] ?? (1, 1);
            SendHome(team);
        }
        ShowFlags();
    }

    public (int x, int y)? TeamHome(int team) => team is 0 or 1 ? Flags[team].Home : null;

    // A flag back at its base
    void SendHome(int team)
    {
        var flag = Flags[team];
        flag.State = FlagState.Home;
        flag.Carrier = -1;
        flag.At = flag.Home;
    }

    Entities.PlayerState? CarrierOf(Flag flag) =>
        flag.State == FlagState.Carried && flag.Carrier >= 0 && flag.Carrier < Session.Players.Count ? Session.Players[flag.Carrier] : null;

    public override void Tick()
    {
        var time = Session.GameState.TimeCount;

        // A carrier gone from the game (or the level) drops it
        for (int team = 0; team < 2; team++)
            if (Flags[team].State == FlagState.Carried && CarrierOf(Flags[team]) is not { Gone: false, Pawn: not null, Health: > 0 })
                Drop(team, CarrierOf(Flags[team]));

        // A flag lying too long goes home
        for (int team = 0; team < 2; team++)
            if (Flags[team].State == FlagState.Dropped && time - Flags[team].DroppedAt >= ReturnTics)
            {
                SendHome(team);
                Announce($"The {GameModes.TeamLabel(team)} flag went back to base", "misc/end_bonus1");
                Hooks.Flag?.Invoke("home", null, team);
            }

        foreach (var pawn in LivingPawns.ToList())
        {
            var p = pawn.State;
            if (p.Team is not (0 or 1))
                continue;
            int own = p.Team, other = 1 - p.Team;

            // Taking the other team's flag (at its base, or where it was dropped)
            var theirs = Flags[other];
            if (theirs.State != FlagState.Carried && Touches(pawn, theirs.At.x, theirs.At.y))
            {
                theirs.State = FlagState.Carried;
                theirs.Carrier = p.Number;
                Announce($"{NameOf(p)} has the {GameModes.TeamLabel(other)} flag!", "misc/k_pkup");
                Hooks.Flag?.Invoke("taken", p, other);
            }

            // Sending your own home, where it was dropped
            var mine = Flags[own];
            if (mine.State == FlagState.Dropped && Touches(pawn, mine.At.x, mine.At.y))
            {
                SendHome(own);
                Announce($"{NameOf(p)} returned the {GameModes.TeamLabel(own)} flag", "misc/end_bonus1");
                Hooks.Flag?.Invoke("returned", p, own);
            }

            // A capture: theirs brought to your base, with yours there
            if (theirs.State == FlagState.Carried && theirs.Carrier == p.Number && mine.State == FlagState.Home
                && Touches(pawn, mine.Home.x, mine.Home.y))
            {
                SendHome(other);
                p.Objective++;
                Announce($"{NameOf(p)} captured the {GameModes.TeamLabel(other)} flag!", "misc/1up");
                Hooks.Flag?.Invoke("captured", p, other);
                Match.AddTeamScore(own, 1);
            }
        }

        ShowFlags();
    }

    /// <summary>A player carrying a flag has died: it drops where they fell</summary>
    public void PlayerDied(Entities.PlayerState victim)
    {
        for (int team = 0; team < 2; team++)
            if (Flags[team].State == FlagState.Carried && Flags[team].Carrier == victim.Number)
                Drop(team, victim);
        ShowFlags();
    }

    void Drop(int team, Entities.PlayerState? carrier)
    {
        var flag = Flags[team];
        if (carrier?.Pawn is not { } pawn)
        {
            SendHome(team);
            return;
        }
        flag.State = FlagState.Dropped;
        flag.Carrier = -1;
        flag.At = (pawn.TileX, pawn.TileY);
        flag.DroppedAt = Session.GameState.TimeCount;
        Announce($"{NameOf(carrier)} dropped the {GameModes.TeamLabel(team)} flag");
        Hooks.Flag?.Invoke("dropped", carrier, team);
    }

    // The flags where they stand or lie, and each carrier drawn with the one they carry
    void ShowFlags()
    {
        for (int team = 0; team < 2; team++)
        {
            var flag = Flags[team];
            if (flag.State == FlagState.Carried)
                HideFlags(FlagClasses[team]);
            else
            {
                HideFlags(FlagClasses[team], flag.At);
                ShowFlag(FlagClasses[team], flag.At.x, flag.At.y);
            }
        }

        foreach (var p in Session.Players)
        {
            int carried = Array.FindIndex(Flags, f => f.State == FlagState.Carried && f.Carrier == p.Number);
            p.CarrySprite = carried >= 0 ? CarrySprite : null;
            p.CarryTranslation = carried >= 0 && p.Team is 0 or 1 ? Hooks.Translation($"{GameModes.TeamNames[p.Team]}-carrier") : null;
        }
    }

    /// <summary>Where each flag is, as this machine's player would put it: "Red flag: home   Blue flag: Bob has it"</summary>
    public string? StatusLine() =>
        string.Join("   ", Enumerable.Range(0, 2).Select(team =>
        {
            var flag = Flags[team];
            var where = flag.State switch
            {
                FlagState.Home => "home",
                FlagState.Dropped => $"dropped ({Math.Max(0, ReturnTics - (Session.GameState.TimeCount - flag.DroppedAt)) / 70}s)",
                _ => CarrierOf(flag) is { } carrier
                    ? ReferenceEquals(carrier, Session.LocalPlayer) ? "you have it!" : $"{NameOf(carrier)} has it"
                    : "taken",
            };
            return $"{GameModes.TeamLabel(team)} flag: {where}";
        }));

    public string? Details() =>
        $"red base {Flags[0].Home.x},{Flags[0].Home.y}; blue base {Flags[1].Home.x},{Flags[1].Home.y}; "
        + $"red flag {Flags[0].State} at {Flags[0].At.x},{Flags[0].At.y}; blue flag {Flags[1].State} at {Flags[1].At.x},{Flags[1].At.y}";

    public override void Write(BinaryWriter bw)
    {
        foreach (var flag in Flags)
        {
            WriteTile(bw, flag.Home);
            bw.Write((byte)flag.State);
            WriteTile(bw, flag.At);
            bw.Write(flag.Carrier);
            bw.Write(flag.DroppedAt);
        }
    }

    public override void Read(BinaryReader br)
    {
        foreach (var flag in Flags)
        {
            flag.Home = ReadTile(br);
            flag.State = (FlagState)br.ReadByte();
            flag.At = ReadTile(br);
            flag.Carrier = br.ReadInt32();
            flag.DroppedAt = br.ReadInt32();
        }
        ShowFlags();
    }
}
