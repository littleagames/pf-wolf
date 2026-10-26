using PFWolf.Assets;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>
/// Changing the level as it's played, from the console (not saved): its wall height, sky,
/// weather, fog and light, the light zones, and a tile's height, tag, patch and flats
/// </summary>
internal sealed class LevelEditCommands(
    ConsoleManager console,
    AssetManager assets,
    MapManager map,
    Lighting lighting,
    ActorLights actorLights,
    Weather weather,
    SkyAndFlats skyAndFlats,
    RenderState render,
    ConsoleTargets targets,
    GameSession session)
{
    internal void Register()
    {
        console.Register("wallheight", "How many stories tall the level's walls are, until the level is left or reloaded.",
            $"wallheight [1-{GameConstants.MaxWallStories}]", WallHeightCommand, Cheat | InLevel);
        console.Register("sky", "Draws a graphic (or wall texture) as the level's sky, until the level is left or reloaded; none for the ceiling color.",
            "sky [name|none]", SkyCommand, InLevel);
        console.Register("weather", "Makes it rain or snow (a game-info weather-types name, or rain, heavy-rain, snow or blizzard) until the level is left or reloaded; none for clear weather.",
            "weather [name|none]", WeatherCommand, InLevel,
            complete: (_, i) => i == 0 ? weather.WeatherTypes().Keys.Append("none") : []);
        console.Register("fog", "Fades the view toward a color with distance, from start to end tiles away, up to max percent, until the level is left or reloaded; none for no shading.",
            "fog [#RRGGBB|none] [start] [end] [max 0-100]", FogCommand, Cheat | InLevel,
            complete: (_, i) => i == 0 ? ["none", "#000000", "#707070"] : []);
        console.Register("lighten", "Lists the named lighten levels (game-info lighten-levels: how many fade steps less things with that level darken with distance), or sets one for the rest of the session.",
            $"lighten [name [0-{Lighting.FadeSteps - 1}]]", LightenCommand, Cheat | InLevel,
            complete: (_, i) => i == 0 ? Lighting.LightenLevels.Keys : []);
        console.Register("light", "Sets the level's light, 0 (black) to 255 (full), until the level is left or reloaded.",
            "light [0-255]", LightCommand, Cheat | InLevel);
        console.Register("lights", "Lists the actors giving off light (actordefs light.*, A_SetLight), and their light now.",
            "lights", LightsCommand, InLevel);
        console.Register("zone", "Shows or sets the light zone (plane 5) of the tile you stand on, a tile, or a rectangle of tiles; 0 takes them out of any zone.",
            "zone [0-65535 [tilex tiley [tilex2 tiley2]]]", ZoneCommand, Cheat | InLevel);
        console.Register("zonelight", "Lists the level's light zones, or shows or sets how one is lit (light 0-255, optional #RRGGBB tint or none, optional fade tics; none as the light removes the zone).",
            "zonelight [zone [0-255|none] [#RRGGBB|none] [tics]]", ZoneLightCommand, Cheat | InLevel);
        console.Register("zoneeffect", "Makes a light zone flicker, pulse or strobe (none stops it), down to low, timed in tics (70 a second).",
            "zoneeffect <zone> <none|flicker|pulse|strobe> [low 0-255] [tics] [bright-tics]", ZoneEffectCommand, Cheat | InLevel,
            complete: (_, i) => i == 1 ? ["none", "flicker", "pulse", "strobe"] : []);
        console.Register("height","Sets how many stories tall a tile's wall is (default: the one you face); 0 uses the level's height. On open floor, 2 or more makes an arch.",
            $"height <0-{GameConstants.MaxWallStories}> [tilex tiley]", HeightCommand, Cheat | InLevel);
        console.Register("tag", "Shows or sets a tile's tag on the tag plane (default: the one you face), and the tag of any actors on it; 0 clears it.",
            "tag [0-65535 [tilex tiley]]", TagCommand, Cheat | InLevel);
        console.Register("scroll", "Lists the level's scrollers (scrolling walls and flats, by tag), or sets one's speeds in texels a tic (x right/east, y down/south), starts, stops or removes it.",
            "scroll [tag [x [y]|start|stop|none]]", ScrollCommand, Cheat | InLevel,
            complete: (_, i) => i == 1 ? ["start", "stop", "none"] : []);
        console.Register("patch", "Shows or sets the patch (mapdefs patches) on a wall tile (default: the one you face); 0 takes it off.",
            "patch [0-255 [tilex tiley]]", PatchCommand, Cheat | InLevel,
            complete: (_, i) => i == 0 ? map.GetMapData().Patches.Keys.Order().Select(id => id.ToString()) : []);
        console.Register("flat", "Sets a tile's floor and ceiling flat indices (mapdefs flats) on the flat plane (default: the tile you stand on).",
            "flat <floor 0-255> <ceiling 0-255> [tilex tiley]", FlatCommand, Cheat | InLevel);
        console.Register("flats", "Sets the level's default floor and ceiling textures, for tiles the flat plane doesn't give one (not saved).",
            "flats [floor|none] [ceiling|none]", FlatsCommand, InLevel);
    }
    internal void HeightCommand(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException($"usage: height <0-{GameConstants.MaxWallStories}> [tilex tiley]");

        int stories = ParseInt(args[0], 0, GameConstants.MaxWallStories);
        var (x, y) = targets.TileArg(args, 1);

        map.SetWallStories(x, y, stories);
        console.Print(stories == 0
            ? $"Tile {x},{y} is back to the level's height ({map.LevelWallStories})"
            : $"Tile {x},{y} is now {stories} {(stories == 1 ? "story" : "stories")} tall");
    }

    internal void TagCommand(string[] args)
    {
        if (args.Length is 2 or > 3)
            throw new ArgumentException("usage: tag [0-65535 [tilex tiley]]");

        var (x, y) = targets.TileArg(args, 1);
        // The actors standing on the tile (not the player), which keep the tag they spawned with
        var actors = map.GetActors()
            .Where(a => a is not Entities.Actors.PlayerPawn && !a.IsRemoved && a.TileX == x && a.TileY == y)
            .ToList();

        if (args.Length > 0)
        {
            var tag = (ushort)ParseInt(args[0], 0, ushort.MaxValue);
            map.SetTag(x, y, tag);
            foreach (var actor in actors)
                actor.Tag = tag;
        }

        var tagged = actors.Count == 0 ? ""
            : "  actors: " + string.Join(", ", actors.Select(a => $"{a.Name} {a.Tag}"));
        console.Print($"Tile {x},{y}: tag {map.GetTag(x, y)}{tagged}");
    }

    internal void ScrollCommand(string[] args)
    {
        var scrollers = map.Scrollers;
        if (args.Length > 3)
            throw new ArgumentException("usage: scroll [tag [x [y]|start|stop|none]]");

        if (args.Length >= 2)
        {
            int tag = ParseInt(args[0], 1, ushort.MaxValue);
            switch (args[1].ToLowerInvariant())
            {
                case "none":
                    scrollers.Level.Remove(tag);
                    scrollers.Rebuild();
                    break;
                case "start" or "stop" when !scrollers.Level.ContainsKey(tag):
                    throw new ArgumentException($"The level has no scroller for tag {tag}");
                case "start" or "stop":
                    scrollers.Level[tag].Active = args[1].Equals("start", StringComparison.OrdinalIgnoreCase);
                    break;
                default:
                    if (!scrollers.SetScrollAction("scroll", 0, args))
                        return;
                    break;
            }
        }

        IEnumerable<int> shown = args.Length == 0 ? scrollers.Level.Keys.Order().ToList() : [ParseInt(args[0], 1, ushort.MaxValue)];
        bool any = false;
        foreach (var tag in shown)
        {
            if (!scrollers.Level.TryGetValue(tag, out var s))
            {
                console.Print($"Tag {tag}: no scroller");
                continue;
            }
            var surfaces = string.Join(", ", new[] { ("walls", s.Walls), ("floor", s.Floor), ("ceiling", s.Ceiling) }.Where(p => p.Item2).Select(p => p.Item1));
            int tiles = map.TaggedTiles((ushort)tag).Count();
            console.Print($"Tag {tag}: x {s.X:0.###} y {s.Y:0.###} on {(surfaces.Length > 0 ? surfaces : "nothing")} of {tiles} tiles, {(s.Active ? "running" : "stopped")}");
            any = true;
        }
        if (args.Length == 0 && !any)
            console.Print("The level has no scrollers");
    }

    internal void PatchCommand(string[] args)
    {
        if (args.Length is 2 or > 3)
            throw new ArgumentException("usage: patch [0-255 [tilex tiley]]");

        var (x, y) = targets.TileArg(args, 1);
        if (args.Length > 0)
        {
            var patch = (byte)ParseInt(args[0], 0, 255);
            int tile = map.TileMap[x, y];
            if (patch != 0 && ((tile & GameConstants.BitDoor) != 0 || (tile & ~GameConstants.BitWall) == 0))
                throw new ArgumentException($"Tile {x},{y} isn't a wall: patches go on walls only");
            map.SetPatch(x, y, patch);
        }

        console.Print($"Tile {x},{y}: {targets.DescribePatch(map.GetPatch(x, y))}");
    }

    internal void FlatCommand(string[] args)
    {
        if (args.Length < 2)
            throw new ArgumentException("usage: flat <floor 0-255> <ceiling 0-255> [tilex tiley]");

        int floor = ParseInt(args[0], 0, 255), ceiling = ParseInt(args[1], 0, 255);
        var (x, y) = args.Length >= 4 ? targets.TileArg(args, 2) : (session.Player.TileX, session.Player.TileY);

        map.SetFlats(x, y, (ushort)(floor | ceiling << 8));
        console.Print($"Tile {x},{y}: floor {map.FlatName(x, y, false)}, ceiling {map.FlatName(x, y, true)}");
    }

    internal void FlatsCommand(string[] args)
    {
        string? Texture(int i)
        {
            if (args[i].Equals("none", StringComparison.OrdinalIgnoreCase))
                return null;
            if (assets.Find<TextureAsset>(args[i]) == null)
                throw new ArgumentException($"no texture named {args[i]}");
            return args[i];
        }

        if (args.Length > 0)
            map.BuildFlats(Texture(0), args.Length > 1 ? Texture(1) : map.DefaultCeiling);
        console.Print($"Default floor: {map.DefaultFloor ?? "(color)"}  ceiling: {map.DefaultCeiling ?? "(color)"}");
    }

    internal void ZoneCommand(string[] args)
    {
        if (args.Length is 2 or 4 or > 5)
            throw new ArgumentException("usage: zone [0-65535 [tilex tiley [tilex2 tiley2]]]");

        // the tile stood on, one given, or a rectangle between two
        var (x1, y1) = args.Length >= 3 ? targets.TileArg(args, 1) : (session.Player.TileX, session.Player.TileY);
        var (x2, y2) = args.Length == 5 ? targets.TileArg(args, 3) : (x1, y1);
        if (args.Length > 0)
        {
            var zone = (ushort)ParseInt(args[0], 0, ushort.MaxValue);
            for (int y = Math.Min(y1, y2); y <= Math.Max(y1, y2); y++)
                for (int x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
                    map.SetZone(x, y, zone);
        }

        int shown = map.GetZone(x1, y1);
        var tiles = (x1, y1) == (x2, y2) ? $"Tile {x1},{y1}" : $"Tiles {x1},{y1} to {x2},{y2}";
        console.Print($"{tiles}: zone {shown} ({targets.DescribeZone(shown)})");
    }

    internal void ZoneLightCommand(string[] args)
    {
        if (args.Length == 0)
        {
            if (lighting.LevelZones.Count == 0)
                console.Print("The level has no light zones");
            foreach (var id in lighting.LevelZones.Keys.Order())
                console.Print($"Zone {id}: {targets.DescribeZone(id)}");
            return;
        }

        int zone = ParseInt(args[0], 1, ushort.MaxValue);
        if (args.Length > 1)
        {
            if (args[1].Equals("none", StringComparison.OrdinalIgnoreCase))
                lighting.RemoveZone(zone);
            else
            {
                int light = ParseInt(args[1], 0, 255);
                if (args.Length > 2)
                {
                    if (args[2].Equals("none", StringComparison.OrdinalIgnoreCase))
                        lighting.SetZoneColor(zone, null);
                    else if (Lighting.IsRgb(args[2]))
                        lighting.SetZoneColor(zone, args[2]);
                    else
                        throw new ArgumentException($"expected a #RRGGBB color or none, got \"{args[2]}\"");
                }
                lighting.FadeZoneLight(zone, light, args.Length > 3 ? ParseInt(args[3], 0, 7000) : 0);
            }
        }
        console.Print($"Zone {zone}: {targets.DescribeZone(zone)}");
    }

    internal void LightsCommand(string[] args)
    {
        var lights = actorLights.DescribeActorLights().ToList();
        if (lights.Count == 0)
            console.Print("Nothing on the level gives off light");
        foreach (var line in lights)
            console.Print(line);
        if (lights.Count > 0 && !lighting.Shading)
            console.Print("(the level is at full light, so they don't show)");
    }

    internal void ZoneEffectCommand(string[] args)
    {
        if (args.Length < 2 || !Lighting.TryParseZoneEffect(args[1], out var effect))
            throw new ArgumentException("usage: zoneeffect <zone> <none|flicker|pulse|strobe> [low 0-255] [tics] [bright-tics]");

        int zone = ParseInt(args[0], 1, ushort.MaxValue);
        var state = lighting.ZoneFor(zone);
        if (args.Length > 2) state.LowSetting = ParseInt(args[2], 0, 255);
        if (args.Length > 3) state.TicsSetting = ParseInt(args[3], 1, 7000);
        if (args.Length > 4) state.BrightTics = ParseInt(args[4], 0, 7000);
        lighting.SetZoneEffect(zone, effect);
        console.Print($"Zone {zone}: {targets.DescribeZone(zone)}");
    }

    internal void WallHeightCommand(string[] args)
    {
        if (args.Length > 0)
            map.LevelWallStories = ParseInt(args[0], 1, GameConstants.MaxWallStories);
        console.Print($"Walls are {map.LevelWallStories} {(map.LevelWallStories == 1 ? "story" : "stories")} tall");
    }

    internal void SkyCommand(string[] args)
    {
        if (args.Length > 0)
        {
            var name = args[0].Equals("none", StringComparison.OrdinalIgnoreCase) ? null : args[0];
            if (name != null && assets.Find<GraphicAsset>(name) == null && assets.Find<TextureAsset>(name) == null)
                throw new ArgumentException($"no graphic or texture named {name}");
            skyAndFlats.LevelSky = name;
        }
        console.Print(skyAndFlats.LevelSky == null ? "No sky: the ceiling is a color" : $"Sky: {skyAndFlats.LevelSky}");
    }

    internal void WeatherCommand(string[] args)
    {
        if (args.Length > 0)
        {
            if (!args[0].Equals("none", StringComparison.OrdinalIgnoreCase) && !weather.WeatherTypes().ContainsKey(args[0]))
                throw new ArgumentException($"no weather named {args[0]}");
            weather.SetWeather(args[0]);
        }
        console.Print(weather.LevelWeather == null ? "No weather" : $"Weather: {weather.LevelWeatherName}");
    }

    // The level's shading, or what shading starts from when it has none
    internal ShadingSettings CurrentShading => lighting.LevelShading ?? new ShadingSettings("#000000", 0, 16, 0, 255);

    internal void FogCommand(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("none", StringComparison.OrdinalIgnoreCase))
            lighting.SetShading(null);
        else if (args.Length > 0)
        {
            var color = args[0];
            try { Entities.Color.FromHexRGBA(color); }
            catch (Exception) { throw new ArgumentException($"expected a #RRGGBB color, got \"{color}\""); }

            var s = CurrentShading with { FadeColor = color, MaxFade = 100 };
            if (args.Length > 1) s = s with { FadeStart = ParseTiles(args[1]) };
            if (args.Length > 2) s = s with { FadeEnd = ParseTiles(args[2]) };
            if (args.Length > 3) s = s with { MaxFade = ParseInt(args[3], 0, 100) };
            lighting.SetShading(s);
        }

        console.Print(lighting.LevelShading is { MaxFade: > 0 } f
            ? $"Fog: {f.FadeColor} from {f.FadeStart} to {f.FadeEnd} tiles, up to {f.MaxFade}%"
            : "No fog");
    }

    internal void LightenCommand(string[] args)
    {
        if (args.Length > 1)
        {
            if (Lighting.IsBrightLevel(args[0]))
                throw new ArgumentException($"{Lighting.BrightLevel} is always unshaded");
            Lighting.ChangeLightenLevel(args[0], ParseInt(args[1], 0, Lighting.FadeSteps - 1));
        }

        var shown = Lighting.LightenLevels
            .Where(level => args.Length == 0 || level.Key.Equals(args[0], StringComparison.OrdinalIgnoreCase))
            .OrderBy(level => level.Key).ToList();
        if (shown.Count == 0)
            console.Print(args.Length > 0 ? $"No lighten level {args[0]}" : "No lighten levels");
        foreach (var (name, steps) in shown)
            console.Print($"{name}: {steps} of {Lighting.FadeSteps - 1} fade steps");
    }

    internal void LightCommand(string[] args)
    {
        if (args.Length > 0)
            lighting.SetShading(CurrentShading with { Light = ParseInt(args[0], 0, 255) });
        console.Print($"Light: {lighting.LevelShading?.Light ?? 255}");
    }
}
