using System.Text;
using PFWolf.Networking;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                JOINING MID-GAME

    Someone joining a game already being played comes in at a frame of the host's choosing
    (the next one to go out: TicBundle.Join). Every machine adds them at that frame alike
    (AddNetPlayer), at the start or (deathmatch) somewhere random. Once the frame has been
    played, the host sends them the game as it stands (WriteNetState: what a save holds, for
    every player, and everything else that decides how it goes on), and they play on from the
    next frame, the controls for it already on their way (BeginJoinedGame).

    =============================================================================
    */

    // Host: a player who came in at the frame just played, to send the game to before the next
    static (int Index, int Step)? pendingjoinstate;

    // A player joining: the game they've just taken up carries on, rather than a level starting
    static bool netJoinResume;

    // A player joins the game at this frame, on every machine alike: AddNetPlayer (Gameplay/Players.cs)

    /*
    ===============
    = The game as it stands
    ===============
    */

    const int NETSTATEVERSION = 4;     // 2: a pusher per moving pushwall; 3: targets and shooters in the level body; 4: each player's color

    /// <summary>
    /// Everything a player joining needs to take the game up from here: the level as a save
    /// keeps it, every player in full, who each enemy is after, and the frame counters, the
    /// random numbers' place and the deathmatch's rules and items coming back
    /// </summary>
    static byte[] WriteNetState()
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            bw.Write(NETSTATEVERSION);
            bw.Write(netlevel);
            bw.Write(netstep);
            bw.Write(netseed);
            bw.Write(rndindex);
            bw.Write((byte)gamemode);
            bw.Write(netrules.Monsters);
            bw.Write(netrules.FragLimit);
            bw.Write(netrules.TimeLimitMinutes);
            bw.Write(netrules.ItemRespawn);
            bw.Write(matchover);
            gamestate.Write(bw, players[0]);

            var actors = _mapManager.GetSavedActors();
            int IndexOf(Entities.Actors.Actor? actor) => actor == null ? -1 : actors.FindIndex(a => ReferenceEquals(a, actor));

            bw.Write(players.Count);
            foreach (var p in players)
                WriteNetPlayer(bw, p, IndexOf);

            bw.Write(_inventoryManager.SharedItems.Count);
            foreach (var (item, count) in _inventoryManager.SharedItems)
            {
                bw.Write(item);
                bw.Write(count);
            }

            // Whose each pawn is, before the level (ApplyLevelBody needs it)
            bw.Write(actors.Count);
            foreach (var actor in actors)
                bw.Write(actor is Entities.Actors.PlayerPawn pawn ? pawn.State.Number : -1);

            WriteLevelBody(bw);

            // What a save doesn't keep (each actor's target and shooter are in the level body):
            // who pushed each moving pushwall, in the level body's order
            foreach (var wall in pushwalls)
                bw.Write(wall.Pusher is { } pusher && ReferenceEquals(pusher.State.Pawn, pusher) ? pusher.State.Number : -1);

            bw.Write(itemrespawns.Count);
            foreach (var item in itemrespawns)
            {
                bw.Write(item.ClassName);
                bw.Write(item.TileX);
                bw.Write(item.TileY);
                bw.Write(item.At);
            }
        }
        return ms.ToArray();
    }

    // One player in full (Networking/NetPlayerState.cs)
    static void WriteNetPlayer(BinaryWriter bw, Entities.PlayerState p, Func<Entities.Actors.Actor?, int> indexOf) =>
        NetPlayerState.Write(bw, p, indexOf);

    /// <summary>
    /// Takes up a game already being played, as the host sent it: the players (this machine's
    /// the local one), the level, and where it had got to; the demo loop then plays on from the
    /// next frame. Returns why it couldn't, or null.
    /// </summary>
    static string? BeginJoinedGame(NetSession session, JoinStateInfo join)
    {
        try
        {
            using var br = new BinaryReader(new MemoryStream(join.State));
            if (br.ReadInt32() != NETSTATEVERSION)
                return "The host sent the game in a form this version can't read.";

            netlevel = br.ReadInt32();
            netstep = br.ReadInt32();
            netseed = br.ReadByte();
            int rnd = br.ReadInt32();
            var mode = (GameMode)br.ReadByte();
            netrules = new NetRules(br.ReadBoolean(), br.ReadInt32(), br.ReadInt32(), br.ReadBoolean());
            matchover = br.ReadBoolean();
            var (game, _) = gametype.Read(br);

            // The players, as the host has them
            ResetPlayers();
            players.Clear();
            var lastAttackers = new List<int>();
            var weapons = new List<Entities.Actors.ActorSnapshot?>();
            for (int i = br.ReadInt32(), n = 0; n < i; n++)
            {
                var p = new Entities.PlayerState { Number = n };
                ReadNetPlayer(br, p, out var lastAttacker, out var weapon);
                players.Add(p);
                lastAttackers.Add(lastAttacker);
                weapons.Add(weapon);
            }
            session.BeginPlaying();
            consoleplayer = Math.Max(0, session.LocalIndex);
            SetActing(localplayer);
            SetGameMode(mode);

            var shared = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = br.ReadInt32(); i > 0; i--)
                shared[br.ReadString()] = br.ReadInt32();
            _inventoryManager.RestoreShared(shared);

            var owners = new int[br.ReadInt32()];
            for (int i = 0; i < owners.Length; i++)
                owners[i] = br.ReadInt32();

            // The level: built from its map, then laid over as the host has it
            gamestate = game;
            loadedgame = true;
            netgame = true;
            netLeft = false;
            SetupGameLevel();
            var body = ReadLevelBody(br);
            var actors = ApplyLevelBody(body, i => i < owners.Length && owners[i] >= 0 && owners[i] < players.Count ? players[owners[i]] : null);

            Entities.Actors.Actor? At(int index) => index >= 0 && index < actors.Count ? actors[index] : null;
            foreach (var wall in pushwalls)
            {
                int pusher = br.ReadInt32();
                wall.Pusher = pusher >= 0 && pusher < players.Count ? players[pusher].Pawn : null;
            }

            itemrespawns.Clear();
            for (int i = br.ReadInt32(); i > 0; i--)
                itemrespawns.Add(new ItemRespawn(br.ReadString(), br.ReadInt32(), br.ReadInt32(), br.ReadInt32()));

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                p.LastAttacker = At(lastAttackers[i]);
                if (weapons[i] is { } snapshot && _inventoryManager.CreateActor(snapshot.ClassName) is { } weapon)
                {
                    snapshot.ApplyTo(weapon);
                    p.WeaponSprite = weapon;
                }
            }

            rndindex = rnd;
            godmode = noclip = ammocheat = 0;
            showscoreboard = false;
            netJoinResume = true;
            camera.FollowPlayer();
            StartGame = 1;
            return null;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or FormatException or IndexOutOfRangeException or ArgumentException)
        {
            Console.WriteLine($"Network: couldn't take up the game: {e}");
            netgame = false;
            loadedgame = false;
            return $"The game couldn't be taken up: {e.Message}";
        }
    }

    static void ReadNetPlayer(BinaryReader br, Entities.PlayerState p, out int lastAttacker, out Entities.Actors.ActorSnapshot? weapon) =>
        NetPlayerState.Read(br, p, PlayerColor, out lastAttacker, out weapon);
}
