using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities;
using PFWolf.Entities.Actors;
using PFWolf.Extensions;

namespace PFWolf.Rendering;

/// <summary>A scroller as the level has it now: its speeds, which surfaces, whether it's running, and how far it's gone.</summary>
internal sealed class ScrollerState
{
    internal double X, Y;               // texels a tic: right (east) and down (south)
    internal bool Walls = true, Floor = true, Ceiling = true;
    internal bool Active = true;
    internal double OffsetX, OffsetY;   // texels moved, OffsetX within a tile, OffsetY within Scrollers.RowWrap

    internal static ScrollerState From(ScrollerInfo info) => new()
    {
        X = info.X, Y = info.Y, Walls = info.Walls, Floor = info.Floor, Ceiling = info.Ceiling, Active = info.Active,
    };

    internal ScrollerState Clone() => (ScrollerState)MemberwiseClone();

    internal void Write(BinaryWriter bw)
    {
        bw.Write(X);
        bw.Write(Y);
        bw.Write(Walls);
        bw.Write(Floor);
        bw.Write(Ceiling);
        bw.Write(Active);
        bw.Write(OffsetX);
        bw.Write(OffsetY);
    }

    internal static ScrollerState Read(BinaryReader br) => new()
    {
        X = br.ReadDouble(), Y = br.ReadDouble(),
        Walls = br.ReadBoolean(), Floor = br.ReadBoolean(), Ceiling = br.ReadBoolean(), Active = br.ReadBoolean(),
        OffsetX = br.ReadDouble(), OffsetY = br.ReadDouble(),
    };
}

/// <summary>
/// Scrolling walls and flats (Midnight Assault's): the tiles with a scroller's tag (the tag plane,
/// plane 4) have their textures slide along by it. The map's game-info scrollers say each one's
/// speed and surfaces; switches and actor states start, stop and re-speed them. Only for show:
/// nothing in play depends on them, so demos and net games don't care where they are.
/// </summary>
internal sealed class Scrollers(MapManager map)
{
    const int TextureSize = GameConstants.TextureSize;

    /// <summary>
    /// What OffsetY wraps at, in texels: a whole number of times any texture 1 to 16 stories tall,
    /// so a tall texture never jumps as it wraps (720720 is the least multiple of 1 to 16)
    /// </summary>
    internal const int RowWrap = TextureSize * 720720;

    /// <summary>The level's scrollers by tag</summary>
    internal Dictionary<int, ScrollerState> Level = [];

    // Each tile's offsets, indexed (y << MapShift) + x: a wall's column (0-63, texels to the right
    // the face has moved) and row (texels down), and a floor's and ceiling's east and south (global
    // units within a tile). All 0 on a tile no scroller moves.
    internal readonly int[] WallColumn = new int[MapConstants.MapArea], WallRow = new int[MapConstants.MapArea];
    internal readonly int[] FloorX = new int[MapConstants.MapArea], FloorY = new int[MapConstants.MapArea];
    internal readonly int[] CeilingX = new int[MapConstants.MapArea], CeilingY = new int[MapConstants.MapArea];

    /// <summary>Some tile's walls, floor or ceiling are moved: only then do the renderer's look-ups matter</summary>
    internal bool AnyWalls, AnyFloors, AnyCeilings;

    /// <summary>Sets the level's scrollers from its game-info map (none without one).</summary>
    internal void InitLevel(MapInfo? mapInfo)
    {
        Level = (mapInfo?.Scrollers ?? []).ToDictionary(s => s.Key, s => ScrollerState.From(s.Value));
        Rebuild();
    }

    /// <summary>Moves the running scrollers on by tics, and the tiles with them.</summary>
    internal void Tick(uint tics)
    {
        if (Level.Count == 0)
            return;
        foreach (var scroller in Level.Values)
        {
            if (!scroller.Active)
                continue;
            scroller.OffsetX = Wrap(scroller.OffsetX + scroller.X * tics, TextureSize);
            scroller.OffsetY = Wrap(scroller.OffsetY + scroller.Y * tics, RowWrap);
        }
        Rebuild();      // every tic: tags move with pushwalls
    }

    static double Wrap(double value, double size)
    {
        value %= size;
        return value < 0 ? value + size : value;
    }

    /// <summary>Works out each tile's offsets from its tag's scroller.</summary>
    internal void Rebuild()
    {
        bool had = AnyWalls || AnyFloors || AnyCeilings;
        AnyWalls = AnyFloors = AnyCeilings = false;
        if (Level.Count == 0)
        {
            if (had)
                ClearTiles();
            return;
        }

        const int GlobalPerTexel = (int)MapConstants.TileGlobal / TextureSize;
        for (int y = 0; y < MapConstants.MapSize; y++)
            for (int x = 0; x < MapConstants.MapSize; x++)
            {
                int i = (y << MapConstants.MapShift) + x;
                ScrollerState? scroller = null;
                if (x < map.MapWidth && y < map.MapHeight && map.GetTag(x, y) is var tag and not 0)
                    Level.TryGetValue(tag, out scroller);

                int column = 0, row = 0, east = 0, south = 0;
                if (scroller != null)
                {
                    column = (int)scroller.OffsetX & (TextureSize - 1);
                    row = (int)scroller.OffsetY;
                    east = (int)(scroller.OffsetX * GlobalPerTexel) & ((int)MapConstants.TileGlobal - 1);
                    south = (int)(Wrap(scroller.OffsetY, TextureSize) * GlobalPerTexel) & ((int)MapConstants.TileGlobal - 1);
                }

                bool walls = scroller?.Walls == true, floor = scroller?.Floor == true, ceiling = scroller?.Ceiling == true;
                WallColumn[i] = walls ? column : 0;
                WallRow[i] = walls ? row : 0;
                FloorX[i] = floor ? east : 0;
                FloorY[i] = floor ? south : 0;
                CeilingX[i] = ceiling ? east : 0;
                CeilingY[i] = ceiling ? south : 0;
                AnyWalls |= walls;
                AnyFloors |= floor;
                AnyCeilings |= ceiling;
            }
    }

    void ClearTiles()
    {
        Array.Clear(WallColumn);
        Array.Clear(WallRow);
        Array.Clear(FloorX);
        Array.Clear(FloorY);
        Array.Clear(CeilingX);
        Array.Clear(CeilingY);
    }

    /// <summary>
    /// A wall face's column (0 to 63, times 64, as the trace finds it left to right across the
    /// face, before any flip) moved along by the tile's scroller: the texture slides right.
    /// </summary>
    internal int Column(int tilex, int tiley, int texture)
    {
        if (!AnyWalls || Lighting.TileIndex(tilex, tiley) is not (>= 0 and var i) || WallColumn[i] == 0)
            return texture;
        int column = ((texture >> GameConstants.TextureShift) - WallColumn[i]) & (TextureSize - 1);
        return column << GameConstants.TextureShift;
    }

    /// <summary>How many texels down a wall tile's faces have moved (0 if no scroller moves them)</summary>
    internal int Row(int tilex, int tiley) =>
        AnyWalls && Lighting.TileIndex(tilex, tiley) is >= 0 and var i ? WallRow[i] : 0;

    /*
    =============================================================================

                                    ACTIONS

    For switches (walls.yaml `switch: actions`) and actor states alike. The tag left out (or "")
    is the switch's own tag, or the actor's:

      A_StartScroll([tag])          the tag's scroller runs
      A_StopScroll([tag])           it stops where it is
      A_ToggleScroll([tag])         stops it if it's running, else starts it
      A_SetScroll(tag, x, y)        new speeds, in texels a tic (and running); a tag the map has
                                    no scroller for gets one, on its walls, floors and ceilings

    =============================================================================
    */

    /// <summary>Registers the actions, on whatever level's scrollers <paramref name="level"/> gives when they run.</summary>
    internal static void RegisterActions(Func<Scrollers> level)
    {
        void Both(string name, Func<Scrollers, string, ushort, string[], bool> action)
        {
            MapTriggerRegistry.Register(name, (trigger, args) => action(level(), name, trigger.Tag, args));
            ActorActionRegistry.Register(name, (ActorActionRegistry.ActorAction)((actor, args) => action(level(), name, actor.Tag, args)));
        }

        Both("A_StartScroll", (s, name, own, args) => s.SetActive(name, own, args, _ => true));
        Both("A_StopScroll", (s, name, own, args) => s.SetActive(name, own, args, _ => false));
        Both("A_ToggleScroll", (s, name, own, args) => s.SetActive(name, own, args, active => !active));
        Both("A_SetScroll", (s, name, own, args) => s.SetScrollAction(name, own, args));
    }

    bool SetActive(string name, ushort own, string[] args, Func<bool, bool> active)
    {
        if (!TagArg(name, args, 0, own, out var tag))
            return false;
        if (!Level.TryGetValue(tag, out var scroller))
        {
            Console.WriteLine($"{name}: the map has no scroller for tag {tag} (game-info maps scrollers, or A_SetScroll).");
            return false;
        }
        scroller.Active = active(scroller.Active);
        return true;
    }

    internal bool SetScrollAction(string name, ushort own, string[] args)
    {
        if (!TagArg(name, args, 0, own, out var tag) || !SpeedArg(name, args, 1, out var x) || !SpeedArg(name, args, 2, out var y))
            return false;

        if (!Level.TryGetValue(tag, out var scroller))
            Level[tag] = scroller = new ScrollerState();
        scroller.X = x;
        scroller.Y = y;
        scroller.Active = true;
        Rebuild();
        return true;
    }

    static bool TagArg(string name, string[] args, int index, ushort own, out int tag)
    {
        var arg = args.ElementAtOrDefault(index);
        if (string.IsNullOrEmpty(arg))
            tag = own;
        else if (!int.TryParse(arg, out tag))
            tag = 0;
        if (tag is > 0 and <= ushort.MaxValue)
            return true;
        Console.WriteLine(string.IsNullOrEmpty(arg)
            ? $"{name}: no tag given, and the switch or actor has none."
            : $"{name}: '{arg}' isn't a tag (1-65535).");
        return false;
    }

    static bool SpeedArg(string name, string[] args, int index, out double speed)
    {
        var arg = args.ElementAtOrDefault(index);
        if (string.IsNullOrEmpty(arg))
        {
            speed = 0;
            return true;
        }
        if (double.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out speed))
            return true;
        Console.WriteLine($"{name}: '{arg}' isn't a speed (texels a tic).");
        return false;
    }

    /*
    =============================================================================

                                    SAVES

    =============================================================================
    */

    internal void Write(BinaryWriter bw)
    {
        bw.Write(Level.Count);
        foreach (var (tag, scroller) in Level)
        {
            bw.Write(tag);
            scroller.Write(bw);
        }
    }

    internal static Dictionary<int, ScrollerState> Read(BinaryReader br)
    {
        var scrollers = new Dictionary<int, ScrollerState>();
        for (int i = br.ReadCount(); i > 0; i--)
        {
            int tag = br.ReadInt32();
            scrollers[tag] = ScrollerState.Read(br);
        }
        return scrollers;
    }

    /// <summary>Puts the level's scrollers back as a save had them (after SetupGameLevel set them from game-info).</summary>
    internal void Restore(Dictionary<int, ScrollerState> scrollers)
    {
        Level = scrollers;
        Rebuild();
    }
}
