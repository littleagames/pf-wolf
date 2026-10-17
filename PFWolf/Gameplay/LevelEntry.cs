using System.Text;
using PFWolf.Extensions;

namespace PFWolf.Gameplay;

/// <summary>What entering and leaving levels needs from the game</summary>
/// <param name="ClearVictoryRun">A new level has no victory run going</param>
/// <param name="CaptureOverhead">The level's overhead map, for the elevator's floor list (Ui/ElevatorScreens.cs)</param>
/// <param name="WriteLevelBody">The level as it is: its tiles, doors, actors (SaveGames/LevelBodies.cs)</param>
/// <param name="ReadLevelBody">Reads one back</param>
/// <param name="ApplyLevelBody">Lays one over the level just built from its map</param>
internal sealed record LevelEntryHooks(
    Action ClearVictoryRun,
    Func<byte[]> CaptureOverhead,
    Action<BinaryWriter> WriteLevelBody,
    Func<BinaryReader, SaveGames.LevelBody> ReadLevelBody,
    Action<SaveGames.LevelBody> ApplyLevelBody);

/// <summary>
/// Entering and leaving a level: a hub level kept as it was left and built again as it was
/// (EnterLevel, LeaveLevel), the player as they came in, for death to put them back to
/// (MarkLevelStart, RestoreLevelStart), and arriving in a floor's elevator (AlignPlayerInElevator).
/// </summary>
internal sealed class LevelEntry(
    MapManager mapManager,
    InventoryManager inventoryManager,
    Hubs hubs,
    LevelSetup levelSetup,
    PlayerMovement movement,
    GameSession session,
    LevelEntryHooks hooks)
{
    /// <summary>
    /// Builds the level for GameState.MapOn (GameLoop): a kept hub level as it was left, or the
    /// map afresh. Moving to another cluster forgets the kept levels. True when it's a kept level.
    /// </summary>
    internal bool EnterLevel()
    {
        if (hubs.LevelMap != null && hubs.ClusterOf(hubs.LevelMap) != hubs.ClusterOf(session.GameState.MapOn))
        {
            hubs.HubLevels.Clear();
            hubs.FloorScores.Clear();
        }

        bool restored = RestoreHubLevel(session.GameState.MapOn);
        if (!restored)
        {
            levelSetup.SetupGameLevel();
            hubs.InformantTotal = mapManager.GetActors().Count(a => !a.IsRemoved && a is Entities.Actors.Informant);
        }

        hubs.LevelMap = session.GameState.MapOn;
        hubs.VisitedMaps.Add(session.GameState.MapOn);
        hooks.ClearVictoryRun();
        return restored;
    }

    /// <summary>Keeps the level being left, if it's a hub level (GameLoop, after PlayLoop)</summary>
    internal void LeaveLevel()
    {
        if (hubs.LevelMap == null || !hubs.IsHubMap(hubs.LevelMap))
            return;

        hubs.FloorScores[hubs.LevelMap] = hubs.FloorScore();
        hubs.FloorOverheads[hubs.LevelMap] = hooks.CaptureOverhead();

        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            bw.Write((int)session.GameState.TimeCount);
            bw.Write((int)session.GameState.KillCount);
            bw.Write((int)session.GameState.KillTotal);
            bw.Write((int)session.GameState.SecretCount);
            bw.Write((int)session.GameState.SecretTotal);
            bw.Write((int)session.GameState.TreasureCount);
            bw.Write((int)session.GameState.TreasureTotal);
            bw.Write(hubs.InformantTotal);
            hooks.WriteLevelBody(bw);
        }
        hubs.HubLevels[hubs.LevelMap] = ms.ToArray();
    }

    /// <summary>Rebuilds a kept hub level as it was left; false if there's none (or it can't be)</summary>
    internal bool RestoreHubLevel(string map)
    {
        if (!hubs.HubLevels.TryGetValue(map, out var data))
            return false;

        int timeCount, killCount, killTotal, secretCount, secretTotal, treasureCount, treasureTotal, informants;
        SaveGames.LevelBody body;
        try
        {
            using var br = new BinaryReader(new MemoryStream(data));
            timeCount = br.ReadInt32();
            killCount = br.ReadInt32();
            killTotal = br.ReadInt32();
            secretCount = br.ReadInt32();
            secretTotal = br.ReadInt32();
            treasureCount = br.ReadInt32();
            treasureTotal = br.ReadInt32();
            informants = br.ReadInt32();
            body = hooks.ReadLevelBody(br);

            if (mapManager.CheckLevelState(body.Level, map) is { } problem)
                throw new InvalidDataException(problem);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or FormatException)
        {
            Console.WriteLine($"The kept level {map} couldn't be restored, so it starts afresh: {e.Message}");
            hubs.HubLevels.Remove(map);
            return false;
        }

        // Built from the map without counting anything again, then laid over as it was
        var wasLoaded = session.LoadedGame;
        session.LoadedGame = true;
        levelSetup.SetupGameLevel();
        session.LoadedGame = wasLoaded;
        hooks.ApplyLevelBody(body);

        session.GameState.TimeCount = timeCount;
        session.GameState.KillCount = (short)killCount;
        session.GameState.KillTotal = (short)killTotal;
        session.GameState.SecretCount = (short)secretCount;
        session.GameState.SecretTotal = (short)secretTotal;
        session.GameState.TreasureCount = (short)treasureCount;
        session.GameState.TreasureTotal = (short)treasureTotal;
        hubs.InformantTotal = informants;
        session.PlayerState.WeaponSprite = null;
        return true;
    }

    /// <summary>Remembers the player as they come into the level (GameLoop), for death-restores-level-start</summary>
    internal void MarkLevelStart()
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            session.GameState.Write(bw, session.PlayerState);
            bw.Write(inventoryManager.Items.Count);
            foreach (var (item, count) in inventoryManager.Items)
            {
                bw.Write(item);
                bw.Write(count);
            }
            bw.Write(session.PlayerState.WeaponCharge);
        }
        hubs.LevelStart = ms.ToArray();
    }

    /// <summary>The items the player had coming into the level (MarkLevelStart), or null if it wasn't marked</summary>
    internal IReadOnlyDictionary<string, int>? LevelStartItems()
    {
        if (hubs.LevelStart == null)
            return null;
        using var br = new BinaryReader(new MemoryStream(hubs.LevelStart));
        GameState.Read(br);
        var inventory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = br.ReadCount(); i > 0; i--)
            inventory[br.ReadString()] = br.ReadInt32();
        return inventory;
    }

    /// <summary>
    /// Puts the player back as they came into the level, but with the lives they have now
    /// (Died, with death-restores-level-start). False if there's nothing to go back to.
    /// </summary>
    internal bool RestoreLevelStart()
    {
        if (hubs.LevelStart == null)
            return false;

        using var br = new BinaryReader(new MemoryStream(hubs.LevelStart));
        var (state, player) = GameState.Read(br);
        var inventory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = br.ReadCount(); i > 0; i--)
            inventory[br.ReadString()] = br.ReadInt32();
        int charge = br.ReadInt32();

        player.Lives = session.PlayerState.Lives;
        player.PlayerClass = session.PlayerState.PlayerClass;
        session.GameState = state;
        session.PlayerState.CopyStatsFrom(player);
        inventoryManager.Restore(inventory);
        session.PlayerState.WeaponCharge = charge;
        return true;
    }

    /// <summary>
    /// Puts the player in the elevator they arrive in (bstone's AlignPlayerInElevator): every
    /// floor's elevator is where the last floor's was, so from the tile the player left from,
    /// beside the panel wall, it finds the spot inside facing the doors.
    /// </summary>
    internal void AlignPlayerInElevator(int lastx, int lasty, int panelWall)
    {
        bool Panel(int x, int y) => x >= 0 && y >= 0 && x < MapManager.MapSize && y < MapManager.MapSize
            && mapManager.TileMap[x, y] != 0 && (mapManager.TileMap[x, y] & GameConstants.BitDoor) == 0
            && mapManager.MapSpot(x, y, 0) == panelWall;
        bool IsDoor(int x, int y) => x >= 0 && y >= 0 && x < MapManager.MapSize && y < MapManager.MapSize
            && (mapManager.TileMap[x, y] & GameConstants.BitDoor) != 0;

        int tilex = 0, ox = lastx, oy = lasty;
        if (!Panel(ox + 1, oy) && !Panel(ox - 1, oy))
        {
            if (!Panel(ox - 2, oy))
            {
                for (tilex = ox - 1; tilex > 0; tilex--)
                {
                    if (!Panel(tilex, oy))
                        continue;
                    tilex += Panel(tilex - 1, oy - 1) || Panel(tilex - 1, oy + 1) ? -1 : 1;
                    break;
                }
            }

            if (!Panel(ox + 2, oy) && tilex == 0)
            {
                for (tilex = ox + 1; tilex < MapManager.MapSize; tilex++)
                {
                    if (!Panel(tilex, oy))
                        continue;
                    tilex += Panel(tilex + 1, oy - 1) || Panel(tilex + 1, oy + 1) ? 1 : -1;
                    break;
                }
            }
        }

        if (tilex == 0)
            tilex = lastx;

        short angle = session.Player.Angle;
        if (tilex < MapManager.MapSize)
        {
            if (IsDoor(tilex + 1, oy + 1)) { oy++; tilex--; angle = 0; }
            else if (IsDoor(tilex + 1, oy - 1)) { oy--; tilex--; angle = 0; }
            else if (IsDoor(tilex - 1, oy + 1)) { oy++; tilex++; angle = 180; }
            else if (IsDoor(tilex - 1, oy - 1)) { oy--; tilex++; angle = 180; }
        }
        else
            tilex = ox;

        session.Player.SetPosition(tilex, oy);
        session.Player.Angle = angle;
        movement.Thrust(0, 0);       // settles the player's tile and area
    }
}
