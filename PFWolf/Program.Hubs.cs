using System.Text;
using PFWolf.Assets;
using PFWolf.Extensions;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                    HUB CLUSTERS

    The hub state (the levels kept, the floors been on or unlocked, their stats) is
    Gameplay/Hubs.cs (made in InitServices); these forward to it. Entering and leaving a level,
    and the level start death goes back to, are Gameplay/LevelEntry.cs.

    =============================================================================
    */

    static Hubs hubs = null!;

    internal static HashSet<string> unlockedMaps => hubs.unlockedMaps;

    static short ClusterOf(string? map) => hubs.ClusterOf(map);
    internal static void ResetHubs() => hubs.ResetHubs();
    private static void WriteHubState(BinaryWriter bw) => hubs.WriteHubState(bw);
    private static HubState ReadHubState(BinaryReader br) => Hubs.ReadHubState(br);
    private static void RestoreHubState(HubState state) => hubs.RestoreHubState(state);
    static int FloorScore() => hubs.FloorScore();

    // Entering and leaving levels, and the level start death goes back to (Gameplay/LevelEntry.cs)
    static LevelEntry levelEntry = null!;       // made in InitServices

    static LevelEntry MakeLevelEntry() => new(_mapManager, _inventoryManager, hubs, levelSetup, playerMovement, session,
        new LevelEntryHooks(() => loadedgame, on => loadedgame = on, () => victoryRunning = false, CaptureOverhead,
            WriteLevelBody, ReadLevelBody, body => ApplyLevelBody(body)));

    internal static bool EnterLevel() => levelEntry.EnterLevel();
    internal static void LeaveLevel() => levelEntry.LeaveLevel();
    internal static void MarkLevelStart() => levelEntry.MarkLevelStart();
    internal static bool RestoreLevelStart() => levelEntry.RestoreLevelStart();
    static void AlignPlayerInElevator(int lastx, int lasty, int panelWall) => levelEntry.AlignPlayerInElevator(lastx, lasty, panelWall);

    /*
    =============================================================================

                                TELEPORTERS

    =============================================================================
    */

    /// <summary>
    /// A_Teleport: Blake Stone's teleporter wall (32), by the object-plane value on it. 0xF4nn
    /// takes the player to floor nn of the cluster, where they come out at the same spot (the
    /// floors' teleporters line up); 0xF5nn moves them to the tile on this floor that the
    /// object-plane value after it gives, its x in the high byte and y in the low byte.
    /// </summary>
    static bool TeleportAction(Entities.TriggerActivation trigger, string[] args)
    {
        int value = _mapManager.MAPSPOT(trigger.TileX, trigger.TileY, 1);
        if (value == 0)
            return false;

        _audioManager.Play("bs/warpin");
        if ((value & 0xff00) == 0xf400)
        {
            int floor = value & 0xff;
            var gameInfo = _gameEngineManager.GetGameInfo();
            var cluster = ClusterOf(gamestate.mapon);
            var map = gameInfo.Maps.FirstOrDefault(m => m.Value.Cluster == cluster && m.Value.FloorNumber == floor && !string.IsNullOrEmpty(m.Value.Music)).Key;
            if (map == null || map.Equals(gamestate.mapon, StringComparison.OrdinalIgnoreCase))
                return false;

            pendingMapChange = new PendingMapChange(map, true, player.X, player.Y, player.Angle, WaitTics: 0);
            gamestate.mapon = map;
            playstate = playstatetypes.ex_warped;
            return true;
        }

        // 0xF5nn (mapinfo "value") keeps the tile in the object-plane tile after it
        if ((value & 0xff00) == 0xf500)
            value = _mapManager.MAPSPOT(trigger.TileX + 1, trigger.TileY, 1);

        int x = value >> 8, y = value & 0xff;
        if (x >= MapManager.MAPSIZE || y >= MapManager.MAPSIZE)
            return false;
        player.SetPosition(x, y);
        Thrust(0, 0);
        return true;
    }

    /*
    =============================================================================

                                    VICTORY

    =============================================================================
    */

    /// <summary>
    /// A_VictoryRun("Class", wall): the player has reached the way out (Blake Stone's win
    /// floor). The view turns toward the map's first tile of the given wall (the shuttle), and
    /// the class (Blake) runs past the player that way; six tiles on, the cluster is won.
    /// </summary>
    static bool VictoryRunAction(Entities.TriggerActivation trigger, string[] args)
    {
        if (gamestate.victoryflag || args.Length == 0)
            return false;

        int wall = args.Length > 1 && int.TryParse(args[1], out var w) ? w : 0;
        for (int y = 0; y < _mapManager.mapheight && wall > 0; y++)
        {
            for (int x = 0; x < _mapManager.mapwidth; x++)
            {
                if (_mapManager.MAPSPOT(x, y, 0) != wall)
                    continue;
                double dx = (x << MapConstants.TILESHIFT) + MapConstants.TILEGLOBAL / 2 - player.X;
                double dy = player.Y - ((y << MapConstants.TILESHIFT) + MapConstants.TILEGLOBAL / 2);
                var angle = Math.Atan2(dy, dx);
                if (angle < 0)
                    angle += Math.PI * 2;
                RotateView((int)(angle / (Math.PI * 2) * ANGLES), 2);
                y = _mapManager.mapheight;
                break;
            }
        }

        gamestate.victoryflag = true;
        victoryRunning = true;
        var runner = _mapManager.SpawnAtActor(args[0], player);
        if (runner == null)
        {
            playstate = playstatetypes.ex_victorious;
            return true;
        }

        // From two tiles behind the player
        double a = player.Angle * Math.PI / 180;
        runner.X = player.X - (int)(Math.Cos(a) * 2 * MapConstants.TILEGLOBAL);
        runner.Y = player.Y + (int)(Math.Sin(a) * 2 * MapConstants.TILEGLOBAL);
        runner.TileX = (byte)(runner.X >> MapConstants.TILESHIFT);
        runner.TileY = (byte)(runner.Y >> MapConstants.TILESHIFT);
        runner.SyncPosition();
        runner.Temp1 = runner.TileX;       // where it set off from
        runner.Temp3 = runner.TileY;
        runner.Temp2 = player.Angle;
        return true;
    }

    /// <summary>A_VictoryRun's runner is on its way: the player watches it rather than turning round (T_Player)</summary>
    internal static bool victoryRunning;

    /// <summary>T_VictoryRun: runs the way the player faced (Temp2); six tiles on from where it set off (Temp1, Temp3), or at a wall, the cluster is won</summary>
    internal static void T_VictoryRun(Entities.Actors.Actor ob)
    {
        const int speed = 3000;
        double a = ob.Temp2 * Math.PI / 180;
        int nx = ob.X + (int)(Math.Cos(a) * speed * tics);
        int ny = ob.Y - (int)(Math.Sin(a) * speed * tics);
        int tx = nx >> MapConstants.TILESHIFT, ty = ny >> MapConstants.TILESHIFT;

        // Starting behind the player can be inside a wall: it runs on out of it, and only a wall
        // it runs into stops it
        bool IsWall(int x, int y) => _mapManager.tilemap[x, y] != 0 && (_mapManager.tilemap[x, y] & BIT_DOOR) == 0;
        bool blocked = tx < 0 || ty < 0 || tx >= MapManager.MAPSIZE || ty >= MapManager.MAPSIZE
            || (IsWall(tx, ty) && !IsWall(ob.TileX, ob.TileY));
        if (!blocked)
        {
            ob.X = nx;
            ob.Y = ny;
            ob.TileX = (byte)tx;
            ob.TileY = (byte)ty;
            ob.SyncPosition();
        }

        if (blocked || Math.Abs(ob.TileX - ob.Temp1) >= 6 || Math.Abs(ob.TileY - ob.Temp3) >= 6)
            playstate = playstatetypes.ex_victorious;
    }

    /// <summary>Turns the view to <paramref name="destAngle"/> at <paramref name="speed"/> degrees a tic, drawing as it goes</summary>
    static void RotateView(int destAngle, int speed)
    {
        destAngle = ((destAngle % ANGLES) + ANGLES) % ANGLES;
        int diff = destAngle - player.Angle;
        if (diff > ANGLES / 2) diff -= ANGLES;
        if (diff < -ANGLES / 2) diff += ANGLES;

        while (diff != 0)
        {
            int step = Math.Min(Math.Abs(diff), Math.Max(1, (int)(speed * tics))) * Math.Sign(diff);
            diff -= step;
            player.Angle = (short)(((player.Angle + step) % ANGLES + ANGLES) % ANGLES);
            ThreeDRefresh();
            CalcTics();
        }
    }

    /*
    =============================================================================

                                LEVEL START MESSAGE

    =============================================================================
    */

    /// <summary>
    /// game-info's level-start-message in presenter.yaml's message box as the level comes up
    /// (Blake Stone's "Get Ready, Blake!"), in place of the "get psyched" picture
    /// </summary>
    internal static void ShowLevelStartMessage(string script)
    {
        DrawPlayScreen();       // the new level's bars, then the view blacked out under the message
        _videoManager.BarScaledCoord(0, PlayAreaTop, _videoManager.screenWidth, PlayAreaAndStatusLine, "0");
        using (_videoManager.UseUiOriginAboveBottom(STATUSLINES))
            PresenterMessageBox(script.Replace("\\r", "\r").Replace("\n", "\r\n"));
        _videoManager.Update();
        if (_videoManager.screenfaded)
            _videoManager.FadeIn();
        _inputManager.UserInput(35);
        _videoManager.FadeOut();
        DrawPlayScreen();
        _videoManager.Update();
    }
}
