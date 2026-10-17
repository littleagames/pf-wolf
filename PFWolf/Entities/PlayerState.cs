namespace PFWolf.Entities;

/// <summary>
/// One player in the game: who they are and what they carry, from level to level. Their pawn
/// (the actor on the map) is made again each level; this lasts the whole game.
/// </summary>
/// <remarks>
/// The game is written against "the player": GameHost's player, PlayerState, PlayerInput and
/// the player globals below them (anglefrac, playerpitch, LastAttacker...) are whichever
/// player is acting right now (GameHost.ActAs). Outside a player's own think that's the local
/// player, the one this machine's screen, status bar and sounds belong to.
/// </remarks>
internal sealed class PlayerState
{
    /// <summary>Their place in GameHost.players, 0 for the first</summary>
    public int Number;

    /// <summary>They've left a game played over the network: no pawn, no turn (Networking/NetPlay.cs)</summary>
    public bool Gone;

    /// <summary>Their actor on the map; null between levels</summary>
    public Actors.PlayerPawn? Pawn;

    /// <summary>Their controls each frame</summary>
    public readonly PlayerInput Input = new();

    /// <summary>
    /// The controls for their next frame, when they aren't this machine's player (whose come
    /// from its own keys): nothing pressed, until the network fills it in
    /// </summary>
    public TicCmd PendingCmd;

    // What was GameState's (GameTypes.cs), and is still saved in its place
    public string PlayerClass = "Player";   // the actordefs class played as (game-info player-classes)
    public int OldScore, Score, NextExtra;
    public short Lives;
    public short Health;
    public short Armor, ArmorPercent;       // armor points, and how much of each hit (0-100%) they absorb
    public string? Weapon, ChosenWeapon;    // the weapon in hand and the one picked (see GameState)
    public short FaceFrame;
    public int KillX, KillY;                // where what killed them stood

    // Playing with others: other players killed in a deathmatch (less their own deaths by
    // their own hand), and enemies killed (for the scoreboard)
    public int Frags, Kills;

    /// <summary>
    /// Their team in a team mode (Gameplay/GameModes.cs: 0 red, 1 blue); -1 for none. What
    /// they've done for it this match (captures, loot, points held) is <see cref="Objective"/>.
    /// </summary>
    public int Team = -1;
    public int Objective;

    // Carrying something others should see (Capture the Flag's flag): the sprite their body's
    // Spawn frames are drawn with instead (PLFG), and the translation it's drawn through; null
    // for nothing. Set every frame by the mode, so it isn't saved or sent.
    public string? CarrySprite, CarryTranslation;

    /// <summary>Held item counts, keyed by item type (InventoryManager reads the acting player's)</summary>
    public readonly Dictionary<string, int> Items = new(StringComparer.OrdinalIgnoreCase);

    // The rest isn't saved
    public int ThrustSpeed;                 // how far they moved this frame (Monster aims worse at a runner)
    public ushort PlUX, PlUY;               // their position scaled to unsigned, for CheckLine
    public short AngleFrac;                 // turning left over from the last frame, under a degree
    public Actors.Actor? LastAttacker;      // whoever last hurt them: the death turn and the dead face
    public string? GrinSound;               // the pickup sound the face grins through
    public int FaceCount, FaceTimes;
    public Actors.Actor? WeaponSprite;      // the weapon in hand, run through its states
    public int WeaponCharge;                // tics until a weapon.chargetics weapon can fire again
    public int ZoomLevel;                   // a scope's zoom, 0 to 100 (Gameplay/WeaponZoom.cs)
    public Gameplay.ZoomMode ZoomMode;      // zooming in, zoomed, zooming out or not
    public int TurnSlowdown = 1;            // turning is this many times slower (a zoomed scope's)
    public int WeaponPower;                 // 0 to 100: how long fire's been held to cook a throw (A_HoldCharge)

    // Manning a mounted gun (Gameplay/MountedWeapons.cs): what they're on, the gun it gives them,
    // the weapon they had picked before, and the map it's on
    public Actors.Actor? Mount;
    public string? MountGun, MountFrom, MountMap;

    // Something latched on (Gameplay/Latching.cs): who, the struggle it puts in their hands, the
    // weapon they had picked before, the map, how many times they've struck at it, and how many
    // times slower they move
    public Actors.Actor? Latch;
    public string? LatchGun, LatchFrom, LatchMap;
    public int LatchStrikes;
    public int MoveSlowdown = 1;

    // Going unseen (Gameplay/Stealth.cs): no enemy has noticed them yet this level, how many
    // they've killed unseen, and the suspicion meter (0-100: how long the most suspicious enemy
    // still waits for papers), with what the enemies tell it this tic
    public bool Undetected = true;
    public int StealthKills;
    public int SuspicionMeter, SuspicionPending;
    public double Pitch;                    // looking up (positive) or down, in degrees
    public int EyeZ = GameConstants.EyeDefault;   // eye height, in texels above the floor

    // What others see of them (Gameplay/PlayerBodies.cs): their class's sprites, drawn where they are
    public Actors.Actor? Body;
    public string? BodyClass;               // the class Body was made for
    public bool BodyHoldsWeapon;            // Body is drawn with Player's sprites, so the weapon's weapon.bodysprite stands in for them
    public string? Color;                   // the translation (translations.yaml) Body is drawn through; null for the class's own
    public int BodyLastX, BodyLastY;        // where they were last tic, to tell if they're walking
    public int PainTics;                    // tics left showing them hurt
    public bool PainAlt;                    // Pain1 rather than Pain (even health left, as enemies)

    // Dead in a game with others, until they come back (Gameplay/Players.cs)
    public int DeadTics;                    // how long they've been dead
    public short DeathAngle;                // the way they turn to face, toward whatever killed them

    /// <summary>What messages call them: their name, else "Player n"</summary>
    public string? Name;

    /// <summary>
    /// Copies what a GameState read from a save holds for the player into this one: the game
    /// keeps a single player's stats in a save, in among the game's own.
    /// </summary>
    public void CopyStatsFrom(PlayerState other)
    {
        PlayerClass = other.PlayerClass;
        (OldScore, Score, NextExtra) = (other.OldScore, other.Score, other.NextExtra);
        (Lives, Health, Armor, ArmorPercent) = (other.Lives, other.Health, other.Armor, other.ArmorPercent);
        (Weapon, ChosenWeapon, FaceFrame) = (other.Weapon, other.ChosenWeapon, other.FaceFrame);
        (KillX, KillY) = (other.KillX, other.KillY);
    }
}
