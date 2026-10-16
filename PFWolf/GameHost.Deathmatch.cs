using PFWolf.Entities.Actors;

namespace PFWolf;

internal static partial class GameHost
{
    // Deathmatch's rules and state (Gameplay/Deathmatch.cs), and the scoreboard (Ui/Scoreboard.cs)
    static Deathmatch deathmatch = null!;       // made in InitServices
    static Scoreboard scoreboard = null!;

    static Deathmatch MakeDeathmatch() =>
        new(_mapManager, _inventoryManager, statusBarDrawing, session,
            new DeathmatchHooks(IsFreeFloor, GiveAllKeys, state => playstate = state));

    static Scoreboard MakeScoreboard() =>
        new(_videoManager, _inputManager, menuDrawing, ui, session, ClearMemory);

    internal static readonly NetRules DefaultRules = Deathmatch.DefaultRules;

    /// <summary>The rules of the deathmatch being played</summary>
    internal static NetRules netrules { get => deathmatch.netrules; set => deathmatch.netrules = value; }

    /// <summary>The match has been won (the frag limit) or run out of time: the level ends, for the scoreboard</summary>

    static (int x, int y, int angle) DeathmatchSpot(PlayerPawn self) => deathmatch.DeathmatchSpot(self);
    static void GiveDeathmatchKeys() => deathmatch.GiveDeathmatchKeys();
    static bool HasWeaponFrom(Weapon pickup) => deathmatch.HasWeaponFrom(pickup);
    static void QueueItemRespawn(Inventory item) => deathmatch.QueueItemRespawn(item);
    static void CountFrag(Entities.PlayerState victim, Entities.Actors.Actor? killer) => deathmatch.CountFrag(victim, killer);
}
