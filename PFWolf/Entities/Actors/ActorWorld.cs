using PFWolf.Managers;
using PFWolf.Ui;

namespace PFWolf.Entities.Actors;

/// <summary>
/// What the actors (<see cref="Monster"/> and its kin) use of the game: the managers, the
/// session, the level's doors and AI, line of sight, sounds, and the players' stats, weapons and
/// views. GameHost builds it once; MapManager hands it to each actor as the actor joins the level
/// (<see cref="Actor.World"/>).
/// </summary>
internal sealed class ActorWorld(
    GameEngineManager engine,
    AssetManager assets,
    AudioManager audio,
    InventoryManager inventory,
    HudMessageManager hudMessages,
    MapManager map,
    FactionManager factions,
    LineOfSight lineOfSight,
    Doors doors,
    PositionalSounds positionalSounds,
    ActorSounds actorSounds,
    HudMessageDrawing hudMessageDrawing,
    PlayerWeapons playerWeapons,
    PlayerAttacks playerAttacks,
    PlayerStats playerStats,
    Players players,
    PlayerView playerView,
    SmartSwitches smartSwitches,
    LevelAI levelAI,
    GameSession session)
{
    internal GameEngineManager Engine => engine;
    internal AssetManager Assets => assets;
    internal AudioManager Audio => audio;
    internal InventoryManager Inventory => inventory;
    internal HudMessageManager HudMessages => hudMessages;
    internal MapManager Map => map;
    internal FactionManager Factions => factions;
    internal LineOfSight LineOfSight => lineOfSight;
    internal Doors Doors => doors;
    internal PositionalSounds PositionalSounds => positionalSounds;
    internal ActorSounds ActorSounds => actorSounds;
    internal HudMessageDrawing HudMessageDrawing => hudMessageDrawing;
    internal PlayerWeapons PlayerWeapons => playerWeapons;
    internal PlayerAttacks PlayerAttacks => playerAttacks;
    internal PlayerStats PlayerStats => playerStats;
    internal Players Players => players;
    internal PlayerView PlayerView => playerView;
    internal SmartSwitches SmartSwitches => smartSwitches;
    internal LevelAI LevelAI => levelAI;
    internal GameSession Session => session;
}
