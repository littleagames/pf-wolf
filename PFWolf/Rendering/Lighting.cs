using PFWolf.Assets;
using PFWolf.Entities;

namespace PFWolf.Rendering;

/// <summary>What the level's lighting needs from the game</summary>
/// <param name="HeightNumerator">The view's height numerator (CalcHeight), to turn a height back into a distance</param>
/// <param name="ClearColorTints">Forgets the actor lights' color tints (the tints they name are gone)</param>
/// <param name="TickActorLights">Runs the actors' lights for this tic's worth of time (Rendering/ActorLights.cs)</param>
/// <param name="LightingOn">The Lighting setting (r_lighting): off, nothing is shaded; null for always on</param>
internal sealed record LightingHooks(Func<int> HeightNumerator, Action ClearColorTints, Action<uint> TickActorLights, Func<bool>? LightingOn = null);

/// <summary>
/// SHADING: the view is drawn in palette indices, so shading remaps them, Doom colormap style. A
/// light row (VideoManager.GetLightRow) holds FadeSteps remaps of 256 entries for one light (a
/// level and a tint): step 0 is only lit, and each step after it goes further toward the fade
/// color. A pixel's step comes from its distance along the view (as CalcHeight measures it), so
/// walls take one per post, flats one per row, and sprites one per sprite.
///
/// THE LIGHT GRID: where the light falls. Each tile has a base light, a level and a tint: its
/// zone's, else the level's. On top of that the level is split into light cells, LIGHTCELLS a
/// tile each way, and each cell has light added to it (CellAdd, in light levels) by lights
/// moving about. Light is looked up at a point: the base light of a tile (the open tile a wall
/// is seen from, the tile a floor pixel or a sprite is on) plus the cell the point is in.
///
/// ZONE LIGHTS: light zones change as the level is played: switches and actor states set their
/// light (at once or fading over some tics) and effects make it flicker, pulse or strobe. The
/// zones tick with the actors, and each frame a zone whose light level or tint has moved gives
/// its tiles the new one in the light grid.
/// </summary>
internal sealed class Lighting(
    GameEngineManager engine,
    VideoManager video,
    MapManager map,
    LightingHooks hooks)
{
    // The numbers are PFWolf.Common's (Shading/ShadeMath.cs), shared with the editor's previews
    internal const int LightLevels = ShadeMath.LightLevels;
    internal const int FadeSteps = ShadeMath.FadeSteps;

    internal static readonly byte[] NoShade = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

    // What the level fades with when it has zones but no shading of its own: nothing
    internal static readonly ShadingSettings NoFade = ShadingResolver.NoFade;

    internal ShadingSettings? LevelShading;  // null: the level isn't shaded
    internal Dictionary<int, ZoneState> LevelZones = [];     // RegisterZoneLightActions
    internal bool Shading;                // anything to do: something is dimmed, tinted or faded
    internal byte[] LightRow = NoShade;   // the level's light's row
    internal FadeParams Fade;             // the level's fade, as RebuildShading last made it
    internal byte FadeR => Fade.R;
    internal byte FadeG => Fade.G;
    internal byte FadeB => Fade.B;
    internal long FadeStart => Fade.Start;    // along the view, in global units (TileGlobal a tile)
    internal long FadeEnd => Fade.End;
    internal int MaxFadeStep => Fade.MaxFadeStep;
    internal bool InverseFade => Fade.Inverse;    // fade-curve inverse: MaxFadeStep × (1 − FadeStart / nx)

    /// <inheritdoc cref="ShadingResolver.ResolveShading"/>
    internal static ShadingSettings? ResolveShading(MapInfo? mapInfo, DefaultMapInfo defaultMap) => ShadingResolver.ResolveShading(mapInfo, defaultMap);

    internal static bool IsInverseCurve(string? curve) => ShadingResolver.IsInverseCurve(curve);

    /// <inheritdoc cref="ShadingResolver.ResolveZones"/>
    internal static Dictionary<int, ZoneState> ResolveZones(MapInfo? mapInfo, DefaultMapInfo defaultMap) => ShadingResolver.ResolveZones(mapInfo, defaultMap);

    /// <summary>Shades the level with settings (null for none) until it's left or reloaded.</summary>
    internal void SetShading(ShadingSettings? settings)
    {
        LevelShading = settings;
        RebuildShading();
    }

    // Everything shading draws from, again: after the settings change or zones come or go
    internal void RebuildShading()
    {
        var s = LevelShading ?? NoFade;
        // any zone at all, since its light can change as the level is played; nothing with the
        // Lighting setting off
        lightingOn = hooks.LightingOn?.Invoke() ?? true;
        Shading = lightingOn && (s.MaxFade > 0 || s.Light < 255 || LevelZones.Count > 0 || ActorShading);
        LightRow = NoShade;
        foreach (var zone in LevelZones.Values)
            zone.RowLevel = -1;
        GridDirty = true;
        if (!Shading)
            return;

        Fade = FadeParams.From(s);

        ResetLightRows();
        AmbientLevel = LightLevelOf(s.Light);
        LightRow = RowOf(0, AmbientLevel);
        foreach (var zone in LevelZones.Values)
            if (zone.Effect != ZoneEffect.None)
                PrewarmZoneRows(zone, zone.Low, zone.BaseLight);
        UpdateZoneLevels();
    }

    /// <summary>Gives each zone the light level and tint for the light it shows now, where that's moved.</summary>
    internal void UpdateZoneLevels()
    {
        if (!Shading)
            return;
        foreach (var zone in LevelZones.Values)
        {
            int level = LightLevelOf(zone.Current);
            if (zone.RowLevel >= 0 && level == zone.RowLevel && zone.Color == zone.RowColor)
                continue;

            zone.RowLevel = level;
            zone.RowColor = zone.Color;
            zone.RowTint = TintIndex(ParseRgb(zone.Color, NoTint, "zone color"));
            GridDirty = true;
        }
    }

    internal static HashSet<string> BadColors => ShadingResolver.BadColors;

    // A #RRGGBB color, or fallback when there's none or it isn't one (warned about once)
    internal static (byte R, byte G, byte B) ParseRgb(string? color, (byte, byte, byte) fallback, string what) =>
        ShadingResolver.ParseRgb(color, fallback, what);

    // The light level (0 to LightLevels - 1) for a light (0 to 255)
    internal static int LightLevelOf(int light) => ShadeMath.LightLevelOf(light);

    // The light row for a light level with a tint
    internal byte[] LevelRow(int level, (byte R, byte G, byte B) tint) =>
        Shading ? video.GetLightRow(level, LightLevels, FadeSteps, FadeR, FadeG, FadeB, tint.R, tint.G, tint.B) : NoShade;

    /// <summary>Where the remap for something nx away along the view (global units) starts in a light row.</summary>
    internal int ShadeOffset(long nx) => !Shading ? 0 : ShadeMath.ShadeOffset(nx, Fade);

    /// <summary>ShadeOffset for something drawn height high (as WallHeight: a story is height >> 2 pixels).</summary>
    internal int ShadeOffsetForHeight(int height) =>
        !Shading ? 0 : ShadeMath.ShadeOffsetForHeight(height, hooks.HeightNumerator(), Fade);

    /// <summary>
    /// Whether an actor's frame is drawn unshaded: its state has the bright modifier (or
    /// lighten(bright)), or the actor the BRIGHT flag (or `lighten: bright`)
    /// </summary>
    internal static bool IsBright(Entities.Actors.Actor actor)
    {
        bool hasOwn = actor.Properties.TryGetValue("lighten", out var level);
        return ActorShadingRules.IsBright(actor.CurrentState?.Bright == true, actor.HasFlag("bright"), hasOwn, level?.ToString());
    }

    /// <summary>
    /// How many fade steps (of <see cref="FadeSteps"/>) less an actor darkens with distance
    /// (<see cref="ActorShadingRules.Lighten"/>), with the game's lighten levels
    /// </summary>
    internal static int Lighten(Entities.Actors.Actor actor)
    {
        bool hasOwn = actor.Properties.TryGetValue("lighten", out var level);
        return ActorShadingRules.Lighten(hasOwn, level?.ToString(), actor.CurrentState?.LightenLevel, Levels);
    }

    /// <summary>A sprite's shade offset: its distance's, less its lighten's steps, within the level's fade</summary>
    internal int LightenedShadeOffset(int height, int lighten) => ShadeMath.Lightened(ShadeOffsetForHeight(height), lighten, Fade);

    /// <summary>The name that draws something unshaded, as the bright flag does</summary>
    internal const string BrightLevel = LightenLevelTable.BrightLevel;

    internal static bool IsBrightLevel(string? level) => LightenLevelTable.IsBrightLevel(level);

    // The named lighten levels in use: game-info lighten-levels, with the `lighten` command's
    // changes over them for the rest of the session
    internal static readonly LightenLevelTable Levels = new();

    /// <summary>The named lighten levels as they are now (game-info's, with any the `lighten` command changed)</summary>
    internal static IReadOnlyDictionary<string, int> LightenLevels => Levels.Levels;

    /// <summary>Takes the game's named lighten levels (game-info lighten-levels), keeping the `lighten` command's changes</summary>
    internal static void SetLightenLevels(IReadOnlyDictionary<string, int> fromGameInfo) => Levels.Set(fromGameInfo);

    /// <summary>Changes a named lighten level for the rest of the session (the `lighten` command)</summary>
    internal static void ChangeLightenLevel(string name, int steps) => Levels.Change(name, steps);

    /// <summary>A lighten level's fade steps: a number, a name's (warned of once if there's no such name), else 0</summary>
    internal static int LevelSteps(string? level) => Levels.Steps(level);

    internal void InitLevelShading(MapInfo? mapInfo)
    {
        SetLightenLevels(engine.GetGameInfo().LightenLevels);
        var defaultMap = engine.GetGameInfo().DefaultMap;
        _levelMapInfo = mapInfo;
        ActorShading = false;
        LevelZones = ResolveZones(mapInfo, defaultMap);
        SetShading(ResolveShading(mapInfo, defaultMap));
    }

    // The level's map, for A_SetShading("on") to put its own shading back
    MapInfo? _levelMapInfo;

    // The grid's numbers are PFWolf.Common's (Shading/LightGrid.cs), shared with the editor
    internal const int LightCellShift = LightGrid.LightCellShift;
    internal const int LightCells = LightGrid.LightCells;                    // cells a tile, each way
    internal const int CellGlobalShift = LightGrid.CellGlobalShift;         // global units to cells
    internal const int MaxLightLevel = ShadeMath.MaxLightLevel;

    /// <summary>THE LIGHT GRID (PFWolf.Common's LightGrid): each tile's base light, the light cells, the tints</summary>
    internal readonly LightGrid Grid = new();

    // Cells across and down the level: LightCells a tile each way (SizeToLevel)
    internal int GridWidth => Grid.GridWidth;
    internal int GridHeight => Grid.GridHeight;

    // Each tile's base light, indexed MapManager.TileIndex like the flats: a light level, and a
    // tint (an index in tints)
    internal byte[] TileLevel => Grid.TileLevel;
    internal byte[] TileTint => Grid.TileTint;

    // Light levels added to each cell, indexed cy * GridWidth + cx; the most of them any one light
    // added, and that light's tint (0: none, the tile's own)
    internal byte[] CellAdd => Grid.CellAdd;
    internal byte[] CellStrong => Grid.CellStrong;
    internal byte[] CellTint => Grid.CellTint;

    // What a set light (light.mode: set) moves each cell's base light by, up or down, toward its
    // own; and how much of the way (0-255) it went, so where set lights overlap the nearest wins
    internal sbyte[] CellSet => Grid.CellSet;
    internal byte[] CellSetWeight => Grid.CellSetWeight;

    // A set light needs the level shaded, on a level that isn't otherwise (ActorLights)
    internal bool ActorShading;

    /// <summary>The cell (x, y in cells) in CellAdd, CellStrong and CellTint</summary>
    internal int CellIndex(int cx, int cy) => Grid.CellIndex(cx, cy);

    /// <summary>Sizes the tiles' and cells' lights to the level loaded, if it's another size (cleared).</summary>
    internal void SizeToLevel()
    {
        if (Grid.SizeTo(map.MapWidth, map.MapHeight))
            GridDirty = true;
    }

    // The tints in use (0 is none: the grid's), and each one's light rows by level, made as they're needed
    internal List<(byte R, byte G, byte B)> Tints => Grid.Tints;
    internal readonly List<byte[]?[]> TintRows = [];

    internal int AmbientLevel;            // the level's own light level (tint 0)
    internal bool GridDirty = true;       // the tiles' base lights need building again
    internal int GridVersion;             // the MapManager.ZonesVersion they were built for
    internal bool HasZones;               // some tile's base light isn't the level's

    internal static readonly (byte, byte, byte) NoTint = ShadingResolver.NoTint;

    /// <summary>A tint's index in tints, added if it's new (with room for its light rows).</summary>
    internal byte TintIndex((byte R, byte G, byte B) tint)
    {
        byte index = Grid.TintIndex(tint);
        while (TintRows.Count < Tints.Count)
            TintRows.Add(new byte[LightLevels][]);
        return index;
    }

    /// <summary>Forgets the light rows (they're for the old fade), leaving the tints with only none.</summary>
    internal void ResetLightRows()
    {
        Tints.Clear();
        TintRows.Clear();
        hooks.ClearColorTints();
        // cells tinted from the old tints mustn't name new ones
        Array.Clear(CellTint);
        TintIndex(NoTint);
    }

    // A tint's light row for a light level
    internal byte[] RowOf(int tint, int level) => TintRows[tint][level] ??= LevelRow(level, Tints[tint]);

    /// <summary>The index of a tile in TileLevel and TileTint, or -1 off the map.</summary>
    internal int TileIndex(int x, int y) => map.TileIndexOrNone(x, y);

    /// <summary>
    /// The light row at a point (global units): the base light of a tile (a TileIndex; off the
    /// map, the level's) plus the light added to the point's cell (LightGrid.LightAt).
    /// </summary>
    internal byte[] LightAt(int tile, long gx, long gy)
    {
        if (!Shading)
            return NoShade;
        if (tile < 0)
            return LightRow;

        var (level, tint) = Grid.LightAt(tile, gx, gy);
        return RowOf(tint, level);
    }

    /// <summary>The middle of a tile, in global units: where something placed on the tile stands.</summary>
    internal static (int X, int Y) TileCenter(int x, int y) =>
        ((x << MapConstants.TileShift) + (int)MapConstants.TileGlobal / 2, (y << MapConstants.TileShift) + (int)MapConstants.TileGlobal / 2);

    /// <summary>Brings the tiles' base lights up to date with the zone plane and the zones; called before each frame.</summary>
    internal void UpdateLightGrid()
    {
        SizeToLevel();
        if (!GridDirty && GridVersion == map.ZonesVersion)
            return;
        GridDirty = false;
        GridVersion = map.ZonesVersion;

        HasZones = false;
        for (int y = 0; y < map.MapHeight; y++)
            for (int x = 0; x < map.MapWidth; x++)
            {
                int i = map.TileIndex(x, y);
                TileLevel[i] = (byte)AmbientLevel;
                TileTint[i] = 0;
                if (map.GetZone(x, y) is var id and not 0
                    && LevelZones.TryGetValue(id, out var zone) && zone.RowLevel >= 0)
                {
                    TileLevel[i] = (byte)zone.RowLevel;
                    TileTint[i] = zone.RowTint;
                    HasZones |= zone.RowLevel != AmbientLevel || zone.RowTint != 0;
                }
            }
    }

    // Flickering is only for show, so it doesn't draw on the game's random numbers (demos)
    internal static Random ZoneRandom = new();

    /// <summary>
    /// A hash of the light grid as it stands (the console's `lightgrid`): each tile's base light
    /// and tint, every cell's added light, strongest light, tint and set light, and the tints
    /// </summary>
    internal string GridHash()
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        hash.AppendData(BitConverter.GetBytes(GridWidth));
        hash.AppendData(BitConverter.GetBytes(GridHeight));
        hash.AppendData(TileLevel);
        hash.AppendData(TileTint);
        hash.AppendData(CellAdd);
        hash.AppendData(CellStrong);
        hash.AppendData(CellTint);
        hash.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(CellSet.AsSpan()));
        hash.AppendData(CellSetWeight);
        foreach (var (r, g, b) in Tints)
            hash.AppendData([r, g, b]);
        return Convert.ToHexString(hash.GetHashAndReset())[..16];
    }

    internal static bool TryParseZoneEffect(string name, out ZoneEffect effect) => ShadingResolver.TryParseZoneEffect(name, out effect);

    /// <summary>Runs the zones' fades and effects for this tic's worth of time.</summary>
    // The Lighting setting as RebuildShading last found it
    private bool lightingOn = true;

    internal void TickZoneLights(uint tics)
    {
        // The Lighting setting switched (the Game Options menu, r_lighting): shade, or stop, now
        if ((hooks.LightingOn?.Invoke() ?? true) != lightingOn)
            RebuildShading();

        foreach (var zone in LevelZones.Values)
            zone.Tick((int)tics, ZoneRandom);
        UpdateZoneLevels();
        hooks.TickActorLights(tics);      // and actors' (Rendering/ActorLights.cs)
    }

    /// <summary>The zone with this id, made (at full light) if the level has none yet.</summary>
    internal ZoneState ZoneFor(int id)
    {
        if (!LevelZones.TryGetValue(id, out var zone))
        {
            LevelZones[id] = zone = new ZoneState();
            RebuildShading();           // a new zone may turn shading on
        }
        return zone;
    }

    /// <summary>Sets a zone's light, fading to it over tics (0: at once).</summary>
    internal void FadeZoneLight(int id, int light, int tics)
    {
        var zone = ZoneFor(id);
        zone.FadeFrom = zone.BaseLight;
        zone.Light = Math.Clamp(light, 0, 255);
        zone.FadeTotal = zone.FadeLeft = Math.Max(tics, 0);
        PrewarmZoneRows(zone, zone.FadeFrom, zone.Light);
        UpdateZoneLevels();
    }

    /// <summary>Sets a zone's tint (null for none).</summary>
    internal void SetZoneColor(int id, string? color)
    {
        ZoneFor(id).Color = color;
        RebuildShading();               // a tint may turn shading on
    }

    internal void SetZoneEffect(int id, ZoneEffect effect)
    {
        var zone = ZoneFor(id);
        zone.SetEffect(effect);
        PrewarmZoneRows(zone, zone.Low, zone.BaseLight);
        UpdateZoneLevels();
    }

    /// <summary>Takes a zone out: its tiles get the level's light.</summary>
    internal void RemoveZone(int id)
    {
        LevelZones.Remove(id);
        RebuildShading();
    }

    /// <summary>Builds the light rows a zone going between two lights will need, so it doesn't stall mid-level.</summary>
    internal void PrewarmZoneRows(ZoneState zone, int from, int to)
    {
        if (!Shading)
            return;
        var tint = ParseRgb(zone.Color, (255, 255, 255), "zone color");
        for (int level = LightLevelOf(Math.Min(from, to)); level <= LightLevelOf(Math.Max(from, to)); level++)
            LevelRow(level, tint);
    }

    /*
    =============================================================================

                                    ACTIONS

    For switches (walls.yaml `switch: actions`) and actor states alike. They name the zone
    they change, whatever the switch's tag:

      A_SetZoneLight(zone, light, [tics], [color])  fades the zone to light (0-255) over tics
                                                    (left out or 0: at once); color #RRGGBB
                                                    tints it, "none" takes the tint off, left
                                                    out keeps it
      A_ToggleZoneLight(zone, light1, light2, [tics])  to light2 if it's at (or going to)
                                                    light1, else to light1
      A_SetZoneEffect(zone, effect)                 none, flicker, pulse or strobe, timed as
                                                    the zone's game-info low, tics, bright-tics

    And for the whole level (End of Destiny's light switches, wl_shade.cpp lightson):

      A_SetShading(off | on | toggle)               off takes the level's shading (its fade
                                                    and light) away, its zones' lights staying;
                                                    on puts back the map's own. Not kept in a
                                                    saved game: a script can set it again

    =============================================================================
    */

    internal void RegisterZoneLightActions()
    {
        void Both(string name, Func<string, string[], bool> action)
        {
            MapTriggerRegistry.Register(name, (_, args) => action(name, args));
            Entities.Actors.ActorActionRegistry.Register(name, (Entities.Actors.Actor _, string[] args) => action(name, args));
        }

        Both("A_SetZoneLight", SetZoneLightAction);
        Both("A_ToggleZoneLight", ToggleZoneLightAction);
        Both("A_SetZoneEffect", SetZoneEffectAction);
        Both("A_SetShading", SetShadingAction);
    }

    internal bool SetShadingAction(string name, string[] args)
    {
        var mode = args.ElementAtOrDefault(0)?.Trim().ToLowerInvariant();
        bool on = mode switch
        {
            "on" => true,
            "off" => false,
            "toggle" => LevelShading == null,
            _ => Warn(),
        };
        if (mode is not ("on" or "off" or "toggle"))
            return false;

        var shading = on ? ResolveShading(_levelMapInfo, engine.GetGameInfo().DefaultMap) : null;
        if (shading == LevelShading)
            return false;
        SetShading(shading);
        return true;

        bool Warn()
        {
            Console.WriteLine($"{name}: say off, on or toggle, as {name}(\"off\").");
            return false;
        }
    }

    internal bool SetZoneLightAction(string name, string[] args)
    {
        if (!ZoneArg(name, args, 0, out var zone) || !LightArg(name, args, 1, out var light) || !TicsArg(name, args, 2, out var tics))
            return false;

        switch (args.ElementAtOrDefault(3))
        {
            case null or "":
                break;
            case var none when none.Equals("none", StringComparison.OrdinalIgnoreCase):
                SetZoneColor(zone, null);
                break;
            case var color:
                if (!IsRgb(color))
                {
                    Console.WriteLine($"{name}: '{color}' isn't a #RRGGBB color.");
                    return false;
                }
                SetZoneColor(zone, color);
                break;
        }

        FadeZoneLight(zone, light, tics);
        return true;
    }

    internal bool ToggleZoneLightAction(string name, string[] args)
    {
        if (!ZoneArg(name, args, 0, out var zone) || !LightArg(name, args, 1, out var light1)
            || !LightArg(name, args, 2, out var light2) || !TicsArg(name, args, 3, out var tics))
            return false;

        FadeZoneLight(zone, ZoneFor(zone).Light == light1 ? light2 : light1, tics);
        return true;
    }

    internal bool SetZoneEffectAction(string name, string[] args)
    {
        if (!ZoneArg(name, args, 0, out var zone))
            return false;
        if (!TryParseZoneEffect(args.ElementAtOrDefault(1) ?? "", out var effect))
        {
            Console.WriteLine($"{name}: '{args.ElementAtOrDefault(1)}' isn't none, flicker, pulse or strobe.");
            return false;
        }

        SetZoneEffect(zone, effect);
        return true;
    }

    internal bool ZoneArg(string name, string[] args, int index, out int zone)
    {
        if (int.TryParse(args.ElementAtOrDefault(index), out zone) && zone is > 0 and <= ushort.MaxValue)
            return true;
        Console.WriteLine($"{name}: '{args.ElementAtOrDefault(index)}' isn't a zone (1-65535).");
        return false;
    }

    internal bool LightArg(string name, string[] args, int index, out int light)
    {
        if (int.TryParse(args.ElementAtOrDefault(index), out light) && light is >= 0 and <= 255)
            return true;
        Console.WriteLine($"{name}: '{args.ElementAtOrDefault(index)}' isn't a light (0-255).");
        return false;
    }

    // Left out (or "") is 0: at once
    internal bool TicsArg(string name, string[] args, int index, out int tics)
    {
        tics = 0;
        var arg = args.ElementAtOrDefault(index);
        if (string.IsNullOrEmpty(arg) || int.TryParse(arg, out tics) && tics >= 0)
            return true;
        Console.WriteLine($"{name}: '{arg}' isn't a number of tics.");
        return false;
    }

    internal static bool IsRgb(string color) => ShadingResolver.IsRgb(color);

    /*
    =============================================================================

                                    SAVES

    =============================================================================
    */

    internal void WriteZoneLights(BinaryWriter bw)
    {
        bw.Write(LevelZones.Count);
        foreach (var (id, zone) in LevelZones)
        {
            bw.Write(id);
            zone.Write(bw);
        }
    }

    internal Dictionary<int, ZoneState> ReadZoneLights(BinaryReader br)
    {
        var zones = new Dictionary<int, ZoneState>();
        for (int i = br.ReadCount(); i > 0; i--)
        {
            int id = br.ReadInt32();
            zones[id] = ZoneState.Read(br);
        }
        return zones;
    }

    /// <summary>Puts the level's zones back as a save had them (after SetupGameLevel set them from game-info).</summary>
    internal void RestoreZoneLights(Dictionary<int, ZoneState> zones)
    {
        LevelZones = zones;
        RebuildShading();
    }
}
