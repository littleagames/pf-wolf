namespace PFWolf;

internal static partial class GameHost
{
    // The game's constants (Gameplay/GameConstants.cs)
    internal const int MAXTICS = GameConstants.MAXTICS;
    internal const int BIT_DOOR = GameConstants.BIT_DOOR;
    internal const int ANGLES = GameConstants.ANGLES;
    internal const int TEXTURESIZE = GameConstants.TEXTURESIZE;

    internal static void ClearMemory() => _audioManager.StopAll();

    // Sounds placed where things are, heard from the camera (Gameplay/PositionalSounds.cs)
    static PositionalSounds positionalSounds = null!;       // made in InitServices

    internal static void PlaySoundLocTile(string s, int tx, int ty) => positionalSounds.PlaySoundLocTile(s, tx, ty);
    internal static void PlaySoundLocActor(string s, Entities.Actors.Actor ob) => positionalSounds.PlaySoundLocActor(s, ob);
    internal static void UpdateSoundListener() => positionalSounds.UpdateSoundListener();
}
