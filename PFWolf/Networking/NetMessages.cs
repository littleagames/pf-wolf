using LiteNetLib.Utils;
using GameMode = PFWolf.Program.GameMode;

namespace PFWolf.Networking;

/// <summary>
/// What a machine runs, which has to be the same on every machine in a game: each plays the
/// whole game itself from the players' controls, so the same build, game data and mods, or
/// they'd soon play it differently.
/// </summary>
internal sealed record NetIdentity(string Engine, string GamePack, string GameRelease, string Mods, string ContentHash)
{
    public void Write(NetDataWriter w)
    {
        w.Put(Engine);
        w.Put(GamePack);
        w.Put(GameRelease);
        w.Put(Mods);
        w.Put(ContentHash);
    }

    public static NetIdentity Read(NetDataReader r) => new(r.GetString(), r.GetString(), r.GetString(), r.GetString(), r.GetString());

    /// <summary>Why a player running <paramref name="other"/> can't join a game running this, or null if they can</summary>
    public string? Mismatch(NetIdentity other)
    {
        if (!string.Equals(GamePack, other.GamePack, StringComparison.OrdinalIgnoreCase))
            return $"The game is {GamePack}; you're running {other.GamePack}.";
        if (!string.Equals(GameRelease, other.GameRelease, StringComparison.OrdinalIgnoreCase))
            return $"The game's data files are {GameRelease}; yours are {other.GameRelease}.";
        if (Engine != other.Engine)
            return $"The host runs PFWolf {Engine}; you run {other.Engine}.";
        if (!string.Equals(Mods, other.Mods, StringComparison.OrdinalIgnoreCase))
            return $"The host's mods are {(Mods.Length == 0 ? "none" : Mods)}; yours are {(other.Mods.Length == 0 ? "none" : other.Mods)}.";
        if (ContentHash != other.ContentHash)
            return "The host's pfwolf.pk3 or mods aren't the same files as yours.";
        return null;
    }
}

/// <summary>
/// A hosted game, as finding games on the local network shows it. HostId tells one host from
/// another: a host answers on each of its addresses (loopback, IPv4, IPv6).
/// </summary>
internal sealed record HostInfo(int HostId, string HostName, string GamePack, int Players, int MaxPlayers, bool InGame, GameMode Mode, string Map)
{
    public void Write(NetDataWriter w)
    {
        w.Put(HostId);
        w.Put(HostName);
        w.Put(GamePack);
        w.Put((byte)Players);
        w.Put((byte)MaxPlayers);
        w.Put(InGame);
        w.Put((byte)Mode);
        w.Put(Map);
    }

    public static HostInfo Read(NetDataReader r) =>
        new(r.GetInt(), r.GetString(), r.GetString(), r.GetByte(), r.GetByte(), r.GetBool(), (GameMode)r.GetByte(), r.GetString());
}

/// <summary>What a player joining sends with their connection</summary>
internal sealed record Hello(int Version, NetIdentity Identity, string Name, string PlayerClass)
{
    public NetDataWriter Write()
    {
        var w = new NetDataWriter();
        w.Put(NetProtocol.Magic);
        w.Put(Version);
        Identity.Write(w);
        w.Put(Name);
        w.Put(PlayerClass);
        return w;
    }

    /// <summary>The Hello a connection came with, or null if it isn't one (or isn't from this game)</summary>
    public static Hello? Read(NetDataReader r)
    {
        try
        {
            if (r.GetString() != NetProtocol.Magic)
                return null;
            int version = r.GetInt();
            if (version != NetProtocol.Version)
                return new Hello(version, new NetIdentity("", "", "", "", ""), "", "");
            return new Hello(version, NetIdentity.Read(r), r.GetString(), r.GetString());
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>
/// The game the host has set up: how they'll play, the episode (its place in game-info's) and
/// the skill, and a deathmatch's rules (Program.NetRules): its enemies, frag limit, time limit
/// (minutes; 0 for none) and whether taken items come back, and the deathmatch arena played
/// (a game-info map with `deathmatch: true`; empty when the game has none: the episode's start)
/// </summary>
internal sealed record LobbySettings(GameMode Mode, int Episode, int Skill,
    bool Monsters = false, int FragLimit = 20, int TimeLimit = 0, bool ItemRespawn = true, string Map = "")
{
    public void Write(NetDataWriter w)
    {
        w.Put((byte)Mode);
        w.Put((byte)Episode);
        w.Put((byte)Skill);
        w.Put(Monsters);
        w.Put((byte)FragLimit);
        w.Put((byte)TimeLimit);
        w.Put(ItemRespawn);
        w.Put(Map);
    }

    public static LobbySettings Read(NetDataReader r) =>
        new((GameMode)r.GetByte(), r.GetByte(), r.GetByte(), r.GetBool(), r.GetByte(), r.GetByte(), r.GetBool(), r.GetString());
}

/// <summary>A player waiting in the lobby, in their slot (the host's is 0)</summary>
internal sealed record LobbyPlayer(int Slot, string Name, string PlayerClass, bool Ready)
{
    public void Write(NetDataWriter w)
    {
        w.Put((byte)Slot);
        w.Put(Name);
        w.Put(PlayerClass);
        w.Put(Ready);
    }

    public static LobbyPlayer Read(NetDataReader r) => new(r.GetByte(), r.GetString(), r.GetString(), r.GetBool());

    public static void WriteList(NetDataWriter w, IReadOnlyList<LobbyPlayer> players)
    {
        w.Put((byte)players.Count);
        foreach (var p in players)
            p.Write(w);
    }

    public static List<LobbyPlayer> ReadList(NetDataReader r)
    {
        var list = new List<LobbyPlayer>();
        for (int i = r.GetByte(); i > 0; i--)
            list.Add(Read(r));
        return list;
    }
}

/// <summary>A player's controls for one frame, as they go over the network</summary>
internal static class TicCmdCodec
{
    public static void Write(NetDataWriter w, in Entities.TicCmd cmd)
    {
        w.Put(cmd.Buttons);
        w.Put((short)Math.Clamp(cmd.ControlX, short.MinValue, short.MaxValue));
        w.Put((short)Math.Clamp(cmd.ControlY, short.MinValue, short.MaxValue));
        w.Put((short)Math.Clamp(cmd.ControlStrafe, short.MinValue, short.MaxValue));
        w.Put((float)cmd.Pitch);        // the view only: it needn't be exact
        w.Put(cmd.CenterView);
    }

    public static Entities.TicCmd Read(NetDataReader r) => new()
    {
        Buttons = r.GetUInt(),
        ControlX = r.GetShort(),
        ControlY = r.GetShort(),
        ControlStrafe = r.GetShort(),
        Pitch = r.GetFloat(),
        CenterView = r.GetBool(),
    };

    /// <summary>What a command comes out as once it has been over the network (the same on every machine)</summary>
    public static Entities.TicCmd RoundTrip(in Entities.TicCmd cmd)
    {
        var w = new NetDataWriter();
        Write(w, cmd);
        return Read(new NetDataReader(w));
    }
}

/// <summary>A player coming into a game already being played: their index (the next one), name and class</summary>
internal sealed record NetJoin(int Index, string Name, string PlayerClass);

/// <summary>
/// Everyone's controls for one frame (a step) of a level (the level'th one played, counting
/// from 1), indexed by player; a bit set in Left for each player who leaves the game at this
/// frame, and anyone joining it here (Join), who gets their first controls the frame after.
/// </summary>
internal sealed record TicBundle(int Level, int Step, byte Left, NetJoin? Join, Entities.TicCmd[] Cmds)
{
    public void Write(NetDataWriter w)
    {
        w.Put(Level);
        w.Put(Step);
        w.Put(Left);
        w.Put(Join != null);
        if (Join != null)
        {
            w.Put((byte)Join.Index);
            w.Put(Join.Name);
            w.Put(Join.PlayerClass);
        }
        w.Put((byte)Cmds.Length);
        foreach (var cmd in Cmds)
            TicCmdCodec.Write(w, cmd);
    }

    public static TicBundle Read(NetDataReader r)
    {
        int level = r.GetInt(), step = r.GetInt();
        byte left = r.GetByte();
        var join = r.GetBool() ? new NetJoin(r.GetByte(), r.GetString(), r.GetString()) : null;
        var cmds = new Entities.TicCmd[r.GetByte()];
        for (int i = 0; i < cmds.Length; i++)
            cmds[i] = TicCmdCodec.Read(r);
        return new TicBundle(level, step, left, join, cmds);
    }

    public bool HasLeft(int player) => (Left & (1 << player)) != 0;
}

/// <summary>
/// What a player joining a game already being played gets from the host: the game (as
/// StartGame had it, with everyone since), the frame it's at, and the game as it stands
/// after that frame (Program.NetJoin.cs)
/// </summary>
internal sealed record JoinStateInfo(StartGameInfo Start, int Level, int Step, byte[] State);

/// <summary>
/// The host starting the game: its settings, the random number table's starting place, and
/// everyone in it, in slot order, which is the order they play in (Program.players).
/// </summary>
internal sealed record StartGameInfo(LobbySettings Settings, byte Seed, List<LobbyPlayer> Players)
{
    public void Write(NetDataWriter w)
    {
        Settings.Write(w);
        w.Put(Seed);
        LobbyPlayer.WriteList(w, Players);
    }

    public static StartGameInfo Read(NetDataReader r) => new(LobbySettings.Read(r), r.GetByte(), LobbyPlayer.ReadList(r));
}
