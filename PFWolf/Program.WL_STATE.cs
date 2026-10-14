namespace PFWolf;

internal partial class Program
{
    // Line of sight (Gameplay/LineOfSight.cs, made in InitServices); these forward to it
    static LineOfSight lineOfSight = null!;

    internal static bool CHECKDIAG(int x, int y) => lineOfSight.CHECKDIAG(x, y);
    internal static bool CheckLine(Entities.Actors.Actor ob) => lineOfSight.CheckLine(ob);
    internal static bool CheckLine(Entities.Actors.Actor ob, Entities.Actors.Actor to) => lineOfSight.CheckLine(ob, to);
    internal static bool CheckLine(int fromX, int fromY, Entities.Actors.Actor to) => lineOfSight.CheckLine(fromX, fromY, to);

    internal const long MINSIGHT = LineOfSight.MINSIGHT;
}