namespace PFWolf;

internal static partial class GameHost
{
    // Line of sight (Gameplay/LineOfSight.cs, made in InitServices); these forward to it
    static LineOfSight lineOfSight = null!;

    // What the level does on its own: outlets, warp sites, floor drops, talking (Managers/LevelAI.cs)
    internal static LevelAI levelAI = null!;      // made in InitServices
}
