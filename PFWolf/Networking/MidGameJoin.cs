using System.Text;
using PFWolf.SaveGames;

namespace PFWolf.Networking;

/// <summary>What joining a game mid-level needs from the game</summary>
/// <param name="PlayerColor">A player's color by name (null for none)</param>
/// <param name="StartGame">The game starts playing</param>
internal sealed record MidGameJoinHooks(
    Func<string, string?> PlayerColor,
    Action StartGame);

/// <summary>
/// Someone joining a game already being played comes in at a frame of the host's choosing
/// (the next one to go out: TicBundle.Join). Every machine adds them at that frame alike
/// (AddNetPlayer), at the start or (deathmatch) somewhere random. Once the frame has been
/// played, the host sends them the game as it stands (WriteNetState: what a save holds, for
/// every player, and everything else that decides how it goes on), and they play on from the
/// next frame, the controls for it already on their way (BeginJoinedGame).
/// </summary>
internal sealed class MidGameJoin(
    MapManager mapManager,
    InventoryManager inventoryManager,
    LevelBodies levelBodies,
    LevelSetup levelSetup,
    PushWalls pushWalls,
    Players playersManager,
    Deathmatch deathmatch,
    Ui.Scoreboard scoreboard,
    NetPlay netPlay,
    GameSession gameSession,
    MidGameJoinHooks hooks)
{
    /*
    ===============
    = The game as it stands
    ===============
    */

    internal const int NetStateVersion = 6;     // 6: each player's Prop Hunt disguise and part; 5: team scores and the mode's objective; 2: a pusher per moving pushwall; 3: targets and shooters in the level body; 4: each player's color

    /// <summary>
    /// Everything a player joining needs to take the game up from here: the level as a save
    /// keeps it, every player in full, who each enemy is after, and the frame counters, the
    /// random numbers' place and the deathmatch's rules and items coming back
    /// </summary>
    internal byte[] WriteNetState()
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            bw.Write(NetStateVersion);
            bw.Write(netPlay.NetLevel);
            bw.Write(netPlay.NetStep);
            bw.Write(gameSession.NetSeed);
            bw.Write(gameSession.Random.RndIndex);
            bw.Write((byte)gameSession.GameMode);
            bw.Write(deathmatch.NetRules.Monsters);
            bw.Write(deathmatch.NetRules.ScoreLimit);
            bw.Write(deathmatch.NetRules.TimeLimitMinutes);
            bw.Write(deathmatch.NetRules.ItemRespawn);
            bw.Write(deathmatch.MatchOver);
            bw.Write(deathmatch.TeamScores[0]);
            bw.Write(deathmatch.TeamScores[1]);
            gameSession.GameState.Write(bw, gameSession.Players[0]);

            var actors = mapManager.GetSavedActors();
            int IndexOf(Entities.Actors.Actor? actor) => actor == null ? -1 : actors.FindIndex(a => ReferenceEquals(a, actor));

            bw.Write(gameSession.Players.Count);
            foreach (var p in gameSession.Players)
                WriteNetPlayer(bw, p, IndexOf);

            bw.Write(inventoryManager.SharedItems.Count);
            foreach (var (item, count) in inventoryManager.SharedItems)
            {
                bw.Write(item);
                bw.Write(count);
            }

            // Whose each pawn is, before the level (ApplyLevelBody needs it)
            bw.Write(actors.Count);
            foreach (var actor in actors)
                bw.Write(actor is Entities.Actors.PlayerPawn pawn ? pawn.State.Number : -1);

            levelBodies.WriteLevelBody(bw);

            // What a save doesn't keep (each actor's target and shooter are in the level body):
            // who pushed each moving pushwall, in the level body's order
            foreach (var wall in pushWalls.MovingWalls)
                bw.Write(wall.Pusher is { } pusher && ReferenceEquals(pusher.State.Pawn, pusher) ? pusher.State.Number : -1);

            bw.Write(deathmatch.ItemRespawns.Count);
            foreach (var item in deathmatch.ItemRespawns)
            {
                bw.Write(item.ClassName);
                bw.Write(item.TileX);
                bw.Write(item.TileY);
                bw.Write(item.At);
            }

            // The mode's flags, points or waves (Gameplay/MatchModes)
            deathmatch.Objective?.Write(bw);
        }
        return ms.ToArray();
    }

    // One player in full (Networking/NetPlayerState.cs)
    internal void WriteNetPlayer(BinaryWriter bw, Entities.PlayerState p, Func<Entities.Actors.Actor?, int> indexOf) =>
        NetPlayerState.Write(bw, p, indexOf);

    /// <summary>
    /// Takes up a game already being played, as the host sent it: the players (this machine's
    /// the local one), the level, and where it had got to; the demo loop then plays on from the
    /// next frame. Returns why it couldn't, or null.
    /// </summary>
    internal string? BeginJoinedGame(NetSession session, JoinStateInfo join)
    {
        try
        {
            using var br = new BinaryReader(new MemoryStream(join.State));
            if (br.ReadInt32() != NetStateVersion)
                return "The host sent the game in a form this version can't read.";

            netPlay.NetLevel = br.ReadInt32();
            netPlay.NetStep = br.ReadInt32();
            gameSession.NetSeed = br.ReadByte();
            int rnd = br.ReadInt32();
            var mode = (GameMode)br.ReadByte();
            deathmatch.NetRules = new NetRules(br.ReadBoolean(), br.ReadInt32(), br.ReadInt32(), br.ReadBoolean());
            deathmatch.MatchOver = br.ReadBoolean();
            deathmatch.TeamScores[0] = br.ReadInt32();
            deathmatch.TeamScores[1] = br.ReadInt32();
            var (game, _) = GameState.Read(br);

            // The players, as the host has them
            playersManager.ResetPlayers();
            gameSession.Players.Clear();
            var lastAttackers = new List<int>();
            var weapons = new List<Entities.Actors.ActorSnapshot?>();
            for (int i = br.ReadInt32(), n = 0; n < i; n++)
            {
                var p = new Entities.PlayerState { Number = n };
                ReadNetPlayer(br, p, out var lastAttacker, out var weapon);
                gameSession.Players.Add(p);
                lastAttackers.Add(lastAttacker);
                weapons.Add(weapon);
            }
            session.BeginPlaying();
            gameSession.ConsolePlayer = Math.Max(0, session.LocalIndex);
            gameSession.SetActing(gameSession.LocalPlayer);
            gameSession.SetGameMode(mode);

            var shared = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = br.ReadInt32(); i > 0; i--)
                shared[br.ReadString()] = br.ReadInt32();
            inventoryManager.RestoreShared(shared);

            var owners = new int[br.ReadInt32()];
            for (int i = 0; i < owners.Length; i++)
                owners[i] = br.ReadInt32();

            // The level: built from its map, then laid over as the host has it
            gameSession.GameState = game;
            gameSession.LoadedGame = true;
            gameSession.NetGame = true;
            netPlay.NetLeft = false;
            levelSetup.SetupGameLevel();
            var body = levelBodies.ReadLevelBody(br);
            var actors = levelBodies.ApplyLevelBody(body, i => i < owners.Length && owners[i] >= 0 && owners[i] < gameSession.Players.Count ? gameSession.Players[owners[i]] : null);

            Entities.Actors.Actor? At(int index) => index >= 0 && index < actors.Count ? actors[index] : null;
            foreach (var wall in pushWalls.MovingWalls)
            {
                int pusher = br.ReadInt32();
                wall.Pusher = pusher >= 0 && pusher < gameSession.Players.Count ? gameSession.Players[pusher].Pawn : null;
            }

            deathmatch.ItemRespawns.Clear();
            for (int i = br.ReadInt32(); i > 0; i--)
                deathmatch.ItemRespawns.Add(new ItemRespawn(br.ReadString(), br.ReadInt32(), br.ReadInt32(), br.ReadInt32()));
            deathmatch.Objective?.Read(br);

            for (int i = 0; i < gameSession.Players.Count; i++)
            {
                var p = gameSession.Players[i];
                p.LastAttacker = At(lastAttackers[i]);
                if (weapons[i] is { } snapshot && inventoryManager.CreateActor(snapshot.ClassName) is { } weapon)
                {
                    snapshot.ApplyTo(weapon);
                    p.WeaponSprite = weapon;
                }
            }

            gameSession.Random.RndIndex = rnd;
            gameSession.GodMode = gameSession.NoClip = gameSession.AmmoCheat = 0;
            scoreboard.ShowScoreboard = false;
            netPlay.NetJoinResume = true;
            gameSession.Camera.FollowPlayer();
            hooks.StartGame();
            return null;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or FormatException or IndexOutOfRangeException or ArgumentException)
        {
            Console.WriteLine($"Network: couldn't take up the game: {e}");
            gameSession.NetGame = false;
            gameSession.LoadedGame = false;
            return $"The game couldn't be taken up: {e.Message}";
        }
    }

    internal void ReadNetPlayer(BinaryReader br, Entities.PlayerState p, out int lastAttacker, out Entities.Actors.ActorSnapshot? weapon) =>
        NetPlayerState.Read(br, p, hooks.PlayerColor, out lastAttacker, out weapon);
}
