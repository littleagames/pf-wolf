using PFWolf.Constants;

namespace PFWolf.Gameplay;

/// <summary>
/// Sounds placed where things are, in tile units (JAB): from a tile or an actor; the listener
/// follows the camera (UpdateSoundListener).
/// </summary>
internal sealed class PositionalSounds(AudioManager audio, GameSession session)
{
    // JAB
    // Positional sounds are placed in tile units; the listener follows the camera (see UpdateSoundListener).
    internal void PlaySoundLocTile(string s, int tx, int ty) => audio.PlayAt(s, tx + 0.5f, ty + 0.5f);
    internal void PlaySoundLocActor(string s, Entities.Actors.Actor ob) =>
        audio.PlayAt(s, FixedToTiles(ob.X), FixedToTiles(ob.Y));

    internal void UpdateSoundListener() =>
        audio.SetListener(FixedToTiles(session.camera.X), FixedToTiles(session.camera.Y), session.camera.Angle);

    internal static float FixedToTiles(int value) => value / (float)MapConstants.TILEGLOBAL;
}