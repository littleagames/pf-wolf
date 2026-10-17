using System.Text;
using PFWolf.Constants;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>
/// Debugging aids and information from the console: overhead, where, count, actors, maps, fps,
/// slowmo, vbls, fade and screenshot
/// </summary>
internal sealed class DebugCommands(
    ConsoleManager console,
    GameEngineManager engine,
    VideoManager video,
    MapManager map,
    Doors doors,
    FrameRefresh frame,
    DebugScreens debugScreens,
    PlayScreen playScreen,
    RenderState render,
    ConsoleTargets targets,
    GameSession session,
    Configuration.GameSettings settings)
{
    internal void Register()
    {
        console.Register("overhead", "Shows the raw and filtered map overview.", "overhead", OverheadCommand, Cheat | InLevel);
        console.Register("where", "Shows the player's position and what's at their tile.", "where", WhereCommand, InLevel, aliases: ["pos"]);
        console.Register("count", "Counts doors and actors.", "count", CountCommand, InLevel);
        console.Register("actors", "Lists actors, optionally only those whose name contains the filter.", "actors [filter]", ActorsCommand, InLevel,
            complete: (_, i) => i == 0 ? map.GetActors().Select(a => a.Name) : []);
        console.Register("maps", "Lists the levels that map can warp to.", "maps", MapsCommand);
        console.Register("fps", "Toggles the frame rate counter.", "fps [0|1]", FpsCommand, complete: Values("0", "1"));
        console.Register("slowmo", "Waits extra VBLs every frame (0 = off).", "slowmo [0-50]", SlowMoCommand);
        console.Register("vbls", "Adds extra VBLs per frame (0 = off).", "vbls [0-8]", VblsCommand);
        console.Register("fade", "Fades the view out to black and back in, to try a fade style.",
            "fade <palette|fizzle|melt|mosaic> [tics]", FadeCommand, InLevel,
            complete: Values(Enum.GetNames<FadeStyle>().Select(n => n.ToLowerInvariant()).ToArray()));
        console.Register("screenshot","Saves the screen, without the console, to WSHOT###.BMP in the screenshots folder.", "screenshot", _ => debugScreens.ScreenshotPending = true);
    }
    internal void OverheadCommand(string[] args)
    {
        // BasicOverhead is a blocking screen that waits for a key, so it can't run while the
        // console owns the keyboard. Close it and run the view from the play loop instead.
        console.Close();
        console.Defer(() =>
        {
            debugScreens.BasicOverhead();
            if (settings.ViewSize < 20)
                playScreen.DrawPlayBorder();
            frame.LastTimeCount = (int)GameEngineManager.GetTimeCount();
        });
    }

    internal void WhereCommand(string[] args)
    {
        int x = session.Player.TileX, y = session.Player.TileY;

        console.Print($"X: {session.Player.X} ({session.Player.X % MapConstants.TileGlobal})  Y: {session.Player.Y} ({session.Player.Y % MapConstants.TileGlobal})  Angle: {session.Player.Angle}");
        console.Print($"Tile: {x},{y}  Area: {session.Player.AreaNumber}  Map: {session.GameState.MapOn}");
        console.Print($"tilemap: {map.TileMap[x, y]}  plane 1: {map.MapSpot(x, y, 1)}  spotvis: {map.Spotvis[x, y]}");
        console.Print($"actorat: {map.ActorAt[x, y]?.GetType().Name ?? "(nothing)"}");

        var diagonals = new[] { (x + 1, y), (x, y - 1), (x - 1, y), (x, y + 1) }
            .Where(t => map.WallShapes[t.Item1, t.Item2] != WallShape.Square)
            .Select(t => $"{t.Item1},{t.Item2} {map.WallShapes[t.Item1, t.Item2]}");
        if (diagonals.Any())
            console.Print($"diagonal walls beside: {string.Join("  ", diagonals)}");

        var (fx, fy) = targets.TileArg([], 0);
        console.Print($"wall heights: level {map.LevelWallStories}  tallest {map.MaxWallStories}  "
            + $"faced tile {fx},{fy}: {map.WallStories(fx, fy)} (plane 3: {map.MapSpot(fx, fy, MapManager.HeightPlane)})");

        console.Print($"flats: floor {map.FlatName(x, y, false)}  ceiling {map.FlatName(x, y, true)}  "
            + $"(plane 2: {map.MapSpot(x, y, MapManager.FlatPlane)})");

        console.Print($"tags (plane 4): tile {map.GetTag(x, y)}  faced tile {fx},{fy}: {map.GetTag(fx, fy)}");
        console.Print($"faced tile {fx},{fy}: {targets.DescribePatch(map.GetPatch(fx, fy))}");

        int zone = map.GetZone(x, y);
        console.Print($"light zone (plane 5): {zone} ({targets.DescribeZone(zone)})");
    }

    internal void CountCommand(string[] args)
    {
        var actors = map.GetActors();
        int enemies = actors.Count(MapManager.IsEnemy);
        int alive = actors.Count(a => MapManager.IsEnemy(a) && a.RuntimeFlags.HasFlag(ObjFlags.Shootable));
        int active = actors.Count(a => a.Active != ActiveType.No);

        console.Print($"Doors: {doors.DoorNum}");
        for (int i = 0; i < doors.DoorNum; i++)
        {
            var door = doors.DoorObjList[i];
            if (!string.IsNullOrEmpty(door.Lock))
                console.Print($"  locked ({door.Lock}) at {door.TileX},{door.TileY}");
        }
        console.Print($"Actors: {actors.Count}  active: {active}");
        console.Print($"Enemies: {enemies}  alive: {alive}");
        console.Print($"Kills: {session.GameState.KillCount}/{session.GameState.KillTotal}  Secrets: {session.GameState.SecretCount}/{session.GameState.SecretTotal}  Treasure: {session.GameState.TreasureCount}/{session.GameState.TreasureTotal}");
    }

    internal void ActorsCommand(string[] args)
    {
        string filter = args.Length > 0 ? args[0] : "";
        int shown = 0;

        foreach (var actor in map.GetActors())
        {
            if (!actor.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            var line = new StringBuilder($"{actor.Name} at {actor.TileX},{actor.TileY}");
            if (actor.CurrentState != null)
                line.Append($"  state {actor.CurrentState.StateName}");
            if (MapManager.IsEnemy(actor))
                line.Append($"  hp {actor.Hitpoints}");
            if (actor.Active != ActiveType.No)
                line.Append("  active");
            if (actor.Tag != 0)
                line.Append($"  tag {actor.Tag}");
            line.Append(targets.WallSpriteDescription(actor));

            console.Print(line.ToString());
            shown++;
        }

        console.Print($"{shown} actor(s)");
    }

    internal void MapsCommand(string[] args)
    {
        // Ten to a row (an episode's worth in Wolf3D) so a whole game fits in the console.
        var names = engine.GetGameInfo().Maps.Keys
            .Select(name => name == session.GameState?.MapOn ? $"[{name}]" : name)
            .ToList();

        for (int i = 0; i < names.Count; i += 10)
            console.Print(string.Join(' ', names.Skip(i).Take(10)));

        if (map.Player != null)
            console.Print($"Current map is {session.GameState.MapOn}");
    }

    internal void FpsCommand(string[] args)
    {
        frame.FpsCounter = Toggle(args, frame.FpsCounter);
        console.Print(frame.FpsCounter ? "FPS counter ON" : "FPS counter OFF");
    }

    internal void FadeCommand(string[] args)
    {
        if (args.Length == 0 || !Enum.TryParse<FadeStyle>(args[0], ignoreCase: true, out var style))
            throw new ArgumentException("usage: fade <palette|fizzle|melt|mosaic> [tics]");

        // Without a length, as long as a screen fade
        int? tics = args.Length > 1 ? ParseInt(args[1], 1, 700) : video.FadeTics;

        // Fading blocks, so run it from the play loop with the console out of the picture.
        console.Close();
        console.Defer(() =>
        {
            frame.ThreeDRefresh();
            video.FadeOut(style, new Color { Alpha = 255 }, 30, tics);
            video.FadeIn(style, 30, tics);
            frame.LastTimeCount = (int)GameEngineManager.GetTimeCount();
        });
    }

    internal void SlowMoCommand(string[] args)
    {
        if (args.Length > 0)
            session.SingleStep = (byte)ParseInt(args[0], 0, 50);
        console.Print($"slowmo is {session.SingleStep}");
    }

    internal void VblsCommand(string[] args)
    {
        if (args.Length > 0)
            session.ExtraVbls = ParseInt(args[0], 0, 8);
        console.Print($"vbls is {session.ExtraVbls}");
    }
}
