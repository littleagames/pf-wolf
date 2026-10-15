namespace PFWolf;

internal partial class Program
{
    // The game's constants (Gameplay/GameConstants.cs)
    internal const string YESBUTTONNAME = GameConstants.YESBUTTONNAME;
    internal const string NOBUTTONNAME = GameConstants.NOBUTTONNAME;
    internal const int MAXTICS = GameConstants.MAXTICS;
    internal const int DEMOTICS = GameConstants.DEMOTICS;
    internal const int BIT_WALL = GameConstants.BIT_WALL;
    internal const int BIT_DOOR = GameConstants.BIT_DOOR;
    internal const int BIT_ALLTILES = GameConstants.BIT_ALLTILES;
    internal const int MAXDOORS = GameConstants.MAXDOORS;
    internal const int MAXVISABLE = GameConstants.MAXVISABLE;
    internal const int RUNSPEED = GameConstants.RUNSPEED;
    internal const float HEIGHTRATIO = GameConstants.HEIGHTRATIO;
    internal const long PLAYERSIZE = GameConstants.PLAYERSIZE;
    internal const long MINACTORDIST = GameConstants.MINACTORDIST;
    internal const float PI = GameConstants.PI;
    internal const float M_PI = GameConstants.M_PI;
    internal const int UNSIGNEDSHIFT = GameConstants.UNSIGNEDSHIFT;
    internal const int ANGLES = GameConstants.ANGLES;
    internal const int ANGLEQUAD = GameConstants.ANGLEQUAD;
    internal const int FINEANGLES = GameConstants.FINEANGLES;
    internal const int ANG90 = GameConstants.ANG90;
    internal const int ANG180 = GameConstants.ANG180;
    internal const int ANG270 = GameConstants.ANG270;
    internal const int ANG360 = GameConstants.ANG360;
    internal const long MINDIST = GameConstants.MINDIST;
    internal const int TEXTURESHIFT = GameConstants.TEXTURESHIFT;
    internal const int FIXED2TEXSHIFT = GameConstants.FIXED2TEXSHIFT;
    internal const int TEXTURESIZE = GameConstants.TEXTURESIZE;
    internal const int TEXTUREMASK = GameConstants.TEXTUREMASK;
    internal const int BASEMOVE = GameConstants.BASEMOVE;
    internal const int RUNMOVE = GameConstants.RUNMOVE;
    internal const int SPDPATROL = GameConstants.SPDPATROL;
    internal const int SPDDOG = GameConstants.SPDDOG;

    // The status bar's height in 320x200 lines (statusbar.yaml background), 0 with none
    internal static int STATUSLINES => StatusBar.Get("background")?.Height ?? 0;
    // Lines a status bar across the top of the screen takes (statusbar.yaml top; Blake Stone's)
    internal static int TOPLINES => StatusBar.Get("top")?.Height ?? 0;

    internal static void ClearMemory() => _audioManager.StopAll();

    // Sounds placed where things are, heard from the camera (Gameplay/PositionalSounds.cs)
    static PositionalSounds positionalSounds = null!;       // made in InitServices

    internal static void PlaySoundLocTile(string s, int tx, int ty) => positionalSounds.PlaySoundLocTile(s, tx, ty);
    internal static void PlaySoundLocActor(string s, Entities.Actors.Actor ob) => positionalSounds.PlaySoundLocActor(s, ob);
    internal static void UpdateSoundListener() => positionalSounds.UpdateSoundListener();
}
