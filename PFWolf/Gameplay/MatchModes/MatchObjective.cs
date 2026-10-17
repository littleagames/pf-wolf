using PFWolf.Entities.Actors;
using PFWolf.Managers;

namespace PFWolf.Gameplay.MatchModes;

/// <summary>What the modes' MatchObjectives need from the game</summary>
/// <param name="IsOpenFloor">Floor a player can walk on (no WallId, DoorNumber or solid thing, in an area)</param>
/// <param name="IsFreeFloor">Open floor nobody stands on</param>
/// <param name="Translation">A translation by name if this game has it (translations.yaml), else null</param>
/// <param name="PlaySound">A sound everyone hears, as the game's own (an announcement)</param>
internal sealed record MatchModeHooks(
    Func<int, int, bool> IsOpenFloor,
    Func<int, int, bool> IsFreeFloor,
    Func<string, string?> Translation,
    Action<string> PlaySound);

/// <summary>
/// What every mode's objective shares: the players in it, where they stand, announcing what's
/// happened, the flags it shows on the map, and finding spots for its things on a map that
/// places none. Its state is plain numbers and tiles, never actors: a player joining mid-game
/// gets the level's actors from the host and the objective's state alongside (Write/Read), and
/// the flags shown are found again each frame by class and tile (ShowFlag).
/// </summary>
internal abstract class MatchObjective(MapManager map, GameSession session, Deathmatch match, HudMessageManager hud, MatchModeHooks hooks)
    : IMatchObjective
{
    protected readonly MapManager Map = map;
    protected readonly GameSession Session = session;
    protected readonly Deathmatch Match = match;
    protected readonly HudMessageManager Hud = hud;
    protected readonly MatchModeHooks Hooks = hooks;

    public abstract GameMode Mode { get; }
    public abstract void LevelSetup();
    public abstract void Tick();
    public abstract void Write(BinaryWriter bw);
    public abstract void Read(BinaryReader br);

    protected const int TileGlobal = (int)MapConstants.TileGlobal;

    /// <summary>The players alive on the level, in player order</summary>
    protected IEnumerable<PlayerPawn> LivingPawns =>
        Map.Players.Where(pawn => !pawn.State.Gone && pawn.State.Health > 0 && pawn.Active == ActiveType.Yes);

    /// <summary>Whether a player stands on (or right at) a tile: within <paramref name="reach"/> of its middle either way</summary>
    protected static bool Touches(PlayerPawn pawn, int tilex, int tiley, int reach = TileGlobal * 5 / 8) =>
        Math.Abs((long)pawn.X - (tilex * (long)TileGlobal + TileGlobal / 2)) < reach
        && Math.Abs((long)pawn.Y - (tiley * (long)TileGlobal + TileGlobal / 2)) < reach;

    /// <summary>Whether a player is within <paramref name="radius"/> tiles of one, either way (a zone of 2r+1 square)</summary>
    protected static bool InZone(PlayerPawn pawn, int tilex, int tiley, int radius) =>
        Math.Abs(pawn.TileX - tilex) <= radius && Math.Abs(pawn.TileY - tiley) <= radius;

    /// <summary>What messages call a player</summary>
    protected static string NameOf(Entities.PlayerState p) => p.Name ?? $"Player {p.Number + 1}";

    /// <summary>Tells everyone (over the view, and the console) what's happened, with a sound</summary>
    protected void Announce(string text, string? sound = null)
    {
        Hud.Show(HudMessageKind.Other, text);
        if (sound != null)
            Hooks.PlaySound(sound);
    }

    /// <summary>The translation a team's flag is drawn through: flag-red, flag-blue, or flag-neutral for nobody's</summary>
    protected string? FlagTranslation(int team) =>
        Hooks.Translation(team switch { 0 => "flag-red", 1 => "flag-blue", _ => "flag-neutral" });

    /// <summary>The flag of a class shown on a tile, if there is one</summary>
    protected Entities.Actors.Actor? FlagAt(string className, int tilex, int tiley) =>
        Map.GetActors().FirstOrDefault(a => !a.IsRemoved && a.Name == className && a.TileX == tilex && a.TileY == tiley);

    /// <summary>Shows a flag of a class on a tile (one already there is kept), through a translation</summary>
    protected void ShowFlag(string className, int tilex, int tiley, string? translation = null)
    {
        var flag = FlagAt(className, tilex, tiley) ?? Map.SpawnThing(tilex, tiley, new Assets.MapActorTranslation { Class = className }, countKill: false);
        if (flag != null && translation != null)
            flag.Translation = translation;
    }

    /// <summary>Takes away every flag of a class but those on the tiles given</summary>
    protected void HideFlags(string className, params (int x, int y)[] keep)
    {
        foreach (var flag in Map.GetActors().Where(a => !a.IsRemoved && a.Name == className).ToList())
            if (!keep.Contains((flag.TileX, flag.TileY)))
                Map.MarkForRemoval(flag);
    }

    /// <summary>Where the map's markers of a class stand, in the order they were placed</summary>
    protected List<(int x, int y)> Markers(string className) =>
        Map.GetActors().Where(a => !a.IsRemoved && a.Name == className).Select(a => ((int)a.TileX, (int)a.TileY)).ToList();

    /// <summary>Every open floor tile of the map, row by row</summary>
    protected List<(int x, int y)> OpenFloor()
    {
        var tiles = new List<(int x, int y)>();
        for (int y = 1; y < Map.mapheight - 1; y++)
            for (int x = 1; x < Map.mapwidth - 1; x++)
                if (Hooks.IsOpenFloor(x, y))
                    tiles.Add((x, y));
        return tiles;
    }

    static int Distance((int x, int y) a, (int x, int y) b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

    /// <summary>The two tiles farthest apart (the first in row order of those that are), for two bases; null with fewer than two</summary>
    protected static ((int x, int y) a, (int x, int y) b)? FarthestPair(List<(int x, int y)> tiles)
    {
        if (tiles.Count < 2)
            return null;
        // From any tile, the farthest from it; then the farthest from that (near enough on a map)
        var a = tiles.MaxBy(t => Distance(t, tiles[tiles.Count / 2]));
        var b = tiles.MaxBy(t => Distance(t, a));
        a = tiles.MaxBy(t => Distance(t, b));
        return (a, b);
    }

    /// <summary>
    /// Spots spread out over tiles: after <paramref name="first"/> ones, each the tile farthest
    /// from all chosen so far, up to <paramref name="count"/>
    /// </summary>
    protected static List<(int x, int y)> SpreadOut(List<(int x, int y)> tiles, int count, IEnumerable<(int x, int y)> first)
    {
        var chosen = first.ToList();
        while (chosen.Count < count && tiles.Count > chosen.Count)
        {
            var next = chosen.Count == 0
                ? tiles.MinBy(t => Distance(t, Middle(tiles)))
                : tiles.MaxBy(t => chosen.Min(c => Distance(t, c)));
            if (chosen.Contains(next))
                break;
            chosen.Add(next);
        }
        return chosen;
    }

    /// <summary>The middle of some tiles (their average)</summary>
    protected static (int x, int y) Middle(List<(int x, int y)> tiles) =>
        tiles.Count == 0 ? (0, 0) : ((int)tiles.Average(t => t.x), (int)tiles.Average(t => t.y));

    /// <summary>The open floor tile nearest a spot</summary>
    protected static (int x, int y) NearestOf(List<(int x, int y)> tiles, (int x, int y) to) =>
        tiles.Count == 0 ? to : tiles.MinBy(t => Distance(t, to));

    protected static void WriteTile(BinaryWriter bw, (int x, int y) tile)
    {
        bw.Write(tile.x);
        bw.Write(tile.y);
    }

    protected static (int x, int y) ReadTile(BinaryReader br) => (br.ReadInt32(), br.ReadInt32());
}
