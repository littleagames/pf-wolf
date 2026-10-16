namespace PFWolf;

internal static partial class GameHost
{
    // Line of sight (Gameplay/LineOfSight.cs, made in InitServices); these forward to it
    static LineOfSight lineOfSight = null!;

    // What the level does on its own: outlets, warp sites, floor drops, talking (Managers/LevelAI.cs)
    internal static LevelAI levelAI = null!;      // made in InitServices

    internal static bool CHECKDIAG(int x, int y) => lineOfSight.CHECKDIAG(x, y);
    internal static bool CheckLine(Entities.Actors.Actor ob) => lineOfSight.CheckLine(ob);
    internal static bool CheckLine(Entities.Actors.Actor ob, Entities.Actors.Actor to) => lineOfSight.CheckLine(ob, to);
    internal static bool CheckLine(int fromX, int fromY, Entities.Actors.Actor to) => lineOfSight.CheckLine(fromX, fromY, to);

    internal const long MINSIGHT = LineOfSight.MINSIGHT;
}