using PFWolf.Entities.Actors;

namespace PFWolf;

internal static partial class GameHost
{
    // Deathmatch's rules and state (Gameplay/Deathmatch.cs), and the scoreboard (Ui/Scoreboard.cs)
    static Deathmatch deathmatch = null!;       // made in InitServices
    static Scoreboard scoreboard = null!;

    static Deathmatch MakeDeathmatch() =>
        new(_mapManager, _inventoryManager, statusBarDrawing, session,
            new DeathmatchHooks(IsFreeFloor, GiveAllKeys));

    // Each mode's objective (Gameplay/MatchModes): the one for the mode played runs
    static void AddObjectives()
    {
        var hooks = new Gameplay.MatchModes.MatchModeHooks(
            (x, y) => playersManager.IsOpenFloor(x, y),
            (x, y) => playersManager.IsFreeFloor(x, y),
            name => _translationManager.Exists(name) ? name : null,
            name => _audioManager.Play(name));
        deathmatch.objectives.AddRange(
        [
            new Gameplay.MatchModes.CaptureTheFlag(_mapManager, session, deathmatch, _hudMessageManager, hooks),
            new Gameplay.MatchModes.Heist(_mapManager, session, deathmatch, _hudMessageManager, hooks),
            new Gameplay.MatchModes.Domination(_mapManager, session, deathmatch, _hudMessageManager, hooks),
            new Gameplay.MatchModes.KingOfTheHill(_mapManager, session, deathmatch, _hudMessageManager, hooks),
            new Gameplay.MatchModes.Horde(_mapManager, session, deathmatch, _hudMessageManager, hooks, () => _gameEngineManager.GetGameInfo().Horde),
        ]);
    }

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
    static void CountFrag(Entities.PlayerState victim, Entities.Actors.Actor? killer) => deathmatch.PlayerKilled(victim, killer);
    static bool MayRespawn(Entities.PlayerState p) => deathmatch.Objective?.MayRespawn(p) ?? true;
}
