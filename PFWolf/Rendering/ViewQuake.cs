using PFWolf.Constants;

namespace PFWolf.Rendering;

/*
=============================================================================

                                 VIEW QUAKE

Midnight Assault's quake (wl_play.c StartQuakeEffect, UpdateZoomEffects): an intensity that a
blast adds to (A_Quake) and that dies away, pulsing the view's focal length in and out while it
lasts, so the view shudders. A level with a game-info `shake:` rumbles on its own as well, as the
original's vibration flag did (CAMP04's train): a quake of up to that power every 20 to 140
tics. It's only how the view looks, so its rolls are its own, not the game's.

=============================================================================
*/

internal sealed class ViewQuake
{
    // An intensity of 1 is a power of 100; it's held to half that, and loses a 200th a tic
    internal const double MaxIntensity = 0.5;
    internal const double DecayPerTic = 1 / 200.0;
    // The pulse turns 30 degrees a tic, out to a fifth of the focal length at an intensity of 1
    internal const int PhaseDegreesPerTic = 30;
    internal const long UnitExcursion = GameConstants.FocalLength / 5;

    readonly Random random;
    int phase;                  // degrees
    int rumbleTics, nextRumble;

    internal ViewQuake(Random? random = null)
    {
        this.random = random ?? new Random();
        nextRumble = NextRumble();
    }

    /// <summary>How hard the view's shaking now: 0 for still</summary>
    internal double Intensity { get; private set; }

    /// <summary>Starts a quake of power (0-200), on top of any still going; the pulse starts afresh</summary>
    internal void Start(int power)
    {
        if (power <= 0)
            return;
        Intensity = Math.Min(Intensity + power / 100.0, MaxIntensity);
        phase = 0;
    }

    /// <summary>A level ending or starting: no quake going on</summary>
    internal void Reset()
    {
        Intensity = 0;
        phase = 0;
        rumbleTics = 0;
    }

    /// <summary>
    /// Moves the quake on by tics, rumbling (up to rumble power, 0 for none) as the level does,
    /// and gives how far the focal length is pushed out (or in) now: 0 once it's still
    /// </summary>
    internal long Update(int tics, int rumble)
    {
        if (rumble > 0)
        {
            rumbleTics += tics;
            if (rumbleTics > nextRumble)
            {
                rumbleTics = 0;
                nextRumble = NextRumble();
                Start(random.Next(rumble));
            }
        }

        if (Intensity <= 0)
            return 0;
        Intensity -= tics * DecayPerTic;
        if (Intensity <= 0)
        {
            Intensity = 0;
            return 0;
        }

        phase = (phase + tics * PhaseDegreesPerTic) % 360;
        return (long)(Intensity * UnitExcursion * Math.Sin(phase * Math.PI / 180));
    }

    int NextRumble() => 20 + random.Next(120);
}
