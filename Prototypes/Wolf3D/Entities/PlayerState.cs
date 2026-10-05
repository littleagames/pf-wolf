namespace Wolf3D.Entities;

/// <summary>
/// One player in the game: who they are and what they carry, from level to level. Their pawn
/// (the actor on the map) is made again each level; this lasts the whole game.
/// </summary>
/// <remarks>
/// The game is written against "the player": Program's player, playerstate, playerinput and
/// the player globals below them (anglefrac, playerpitch, LastAttacker...) are whichever
/// player is acting right now (Program.ActAs). Outside a player's own think that's the local
/// player, the one this machine's screen, status bar and sounds belong to.
/// </remarks>
internal sealed class PlayerState
{
    /// <summary>Their place in Program.players, 0 for the first</summary>
    public int Number;

    /// <summary>They've left a game played over the network: no pawn, no turn (Program.NetPlay.cs)</summary>
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

    // What was gametype's (Program.WL_DEF.cs), and is still saved in its place
    public string playerclass = "Player";   // the actordefs class played as (game-info player-classes)
    public int oldscore, score, nextextra;
    public short lives;
    public short health;
    public short armor, armorpercent;       // armor points, and how much of each hit (0-100%) they absorb
    public string? weapon, chosenweapon;    // the weapon in hand and the one picked (see gametype)
    public short faceframe;
    public int killx, killy;                // where what killed them stood

    // Playing with others: other players killed in a deathmatch (less their own deaths by
    // their own hand), and enemies killed (for the scoreboard)
    public int Frags, Kills;

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
    public double Pitch;                    // looking up (positive) or down, in degrees
    public int EyeZ = Program.EYEDEFAULT;   // eye height, in texels above the floor

    // What others see of them (Program.Camera.cs): their class's sprites, drawn where they are
    public Actors.Actor? Body;
    public string? BodyClass;               // the class Body was made for
    public int BodyLastX, BodyLastY;        // where they were last tic, to tell if they're walking
    public int PainTics;                    // tics left showing them hurt
    public bool PainAlt;                    // Pain1 rather than Pain (even health left, as enemies)

    // Dead in a game with others, until they come back (Program.Players.cs)
    public int DeadTics;                    // how long they've been dead
    public short DeathAngle;                // the way they turn to face, toward whatever killed them

    /// <summary>What messages call them: their name, else "Player n"</summary>
    public string? Name;

    /// <summary>
    /// Copies what a gametype read from a save holds for the player into this one: the game
    /// keeps a single player's stats in a save, in among the game's own.
    /// </summary>
    public void CopyStatsFrom(PlayerState other)
    {
        playerclass = other.playerclass;
        (oldscore, score, nextextra) = (other.oldscore, other.score, other.nextextra);
        (lives, health, armor, armorpercent) = (other.lives, other.health, other.armor, other.armorpercent);
        (weapon, chosenweapon, faceframe) = (other.weapon, other.chosenweapon, other.faceframe);
        (killx, killy) = (other.killx, other.killy);
    }
}
