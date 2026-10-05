using LiteNetLib.Utils;

namespace Wolf3D.Networking;

/*
=============================================================================

                                NETWORK PROTOCOL

A game with others has no server: one player hosts (NetSession.Host) and the rest join them
(NetSession.Join), finding the host on the local network (LanBrowser) or by its address.

Every message starts with a NetMessage byte. Connecting sends a Hello as the connection's own
data, so the host can turn a player away (another version, other content, a full or started
game) before they're in; the reason comes back with the refusal.

=============================================================================
*/

internal static class NetProtocol
{
    /// <summary>Changes whenever a message changes: players with different ones can't play together</summary>
    public const int Version = 1;

    /// <summary>The port games are hosted on, and looked for on the local network</summary>
    public const int DefaultPort = 10645;

    /// <summary>What starts every message that isn't over a connection (finding games)</summary>
    public const string Magic = "PFWOLF";

    /// <summary>How long a player can go unheard from before they're taken as gone (ms)</summary>
    public const int DisconnectTimeoutMs = 10000;

    /// <summary>The longest name a player can have</summary>
    public const int MaxNameLength = 16;

    /// <summary>The longest chat line</summary>
    public const int MaxChatLength = 60;

    public static NetDataWriter Message(NetMessage type)
    {
        var writer = new NetDataWriter();
        writer.Put((byte)type);
        return writer;
    }

    /// <summary>A player's name as it's shown: trimmed to MaxNameLength, and never empty</summary>
    public static string CleanName(string? name)
    {
        name = new string((name ?? "").Where(c => c >= ' ' && c < 127).ToArray()).Trim();
        if (name.Length > MaxNameLength)
            name = name[..MaxNameLength].TrimEnd();
        return name.Length == 0 ? "Player" : name;
    }
}

internal enum NetMessage : byte
{
    // Not over a connection: finding games on the local network
    DiscoveryRequest = 1,   // Magic, Version
    DiscoveryReply,         // Magic, HostInfo

    // Host to players
    Welcome,                // your slot
    Lobby,                  // LobbyState
    StartGame,              // StartGameInfo

    // Players to host
    LobbyChange,            // your class, ready

    // Either way: a player says something (the host passes it on to everyone)
    Chat,                   // from slot, text

    // Playing (Program.NetPlay.cs): each machine sends its player's controls for a frame; the
    // host sends everyone's, once it has them all, and every machine plays that frame with them
    Cmd,                    // player to host: level, step, TicCmd
    Tics,                   // host to all: TicBundle
    Checksum,               // player to host: level, step, the game's state summed up
    Notice,                 // host to all: a line to show (someone left, someone is out of sync)
}

/// <summary>Why a game couldn't be joined, as the host's refusal or the connection's end says</summary>
internal static class NetRefusal
{
    public static NetDataWriter Write(string reason)
    {
        var writer = new NetDataWriter();
        writer.Put(NetProtocol.Magic);
        writer.Put(reason);
        return writer;
    }

    public static string? Read(NetDataReader? reader)
    {
        if (reader == null || reader.AvailableBytes == 0)
            return null;
        try
        {
            return reader.GetString() == NetProtocol.Magic ? reader.GetString() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
