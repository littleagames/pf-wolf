namespace PFWolf;

// The weapon in hand bobbing as the player walks (game-info weapon-bob), as Planet Strike's
// (bstone 3d_agent.cpp HandleWeaponBounce). It springs between two points while the player moves
// forwards or back and settles back to the middle when they stop. Only the local player's view
// has it, and it's never saved: a level or loaded game starts it at rest.
internal partial class Program
{
    internal static bool weaponBob;         // game-info weapon-bob

    // In bstone's 200-line pixels: where the weapon swings between, and how far it may overshoot
    private const double BobMax = 10, BobMid = 6, BobMin = 2;
    private const double BobMaxOffset = BobMax + 2, BobMinOffset = BobMin - 2;

    // sintable[90] at bstone's full view size: its spring's pull
    private const double BobPull = 1.0;

    private static double bobOffset = BobMid, bobVel, bobDest = BobMax;
    private static int bobMoving;           // tics left before a stop lets the weapon settle

    internal static void InitWeaponBob()
    {
        bobOffset = BobMid;
        bobDest = BobMax;
        bobVel = 0;
        bobMoving = 0;
    }

    /// <summary>
    /// Moves the bob on by this frame's tics, from whether the acting player is walking forwards
    /// or back. bstone steps it once a frame, which at its 70 frames a second is once a tic.
    /// </summary>
    internal static void UpdateWeaponBob()
    {
        if (!weaponBob || !ActingIsLocal)
            return;

        for (int i = 0; i < tics; i++)
        {
            if (playerinput.ControlY != 0)
                bobMoving = 8;
            else if (bobMoving != 0)
                bobMoving--;

            if (bobMoving != 0)
            {
                if (bobOffset < bobDest)
                {
                    bobVel += BobPull / 2;
                    bobOffset += bobVel;
                    if (bobOffset > bobDest)
                    {
                        bobDest = BobMin;
                        bobVel /= 4;
                    }
                }
                else if (bobOffset > bobDest)
                {
                    bobVel -= BobPull / 4;
                    bobOffset += bobVel;
                    if (bobOffset < bobDest)
                    {
                        bobDest = BobMax;
                        bobVel /= 4;
                    }
                }
            }
            else
            {
                if (bobOffset > BobMid)
                    bobOffset = Math.Max(bobOffset - 2, BobMid);
                else if (bobOffset < BobMid)
                    bobOffset = Math.Min(bobOffset + 2, BobMid);
                bobDest = BobMax;
                bobVel = 0;
            }

            bobOffset = Math.Clamp(bobOffset, BobMinOffset, BobMaxOffset);
        }
    }

    /// <summary>
    /// How far down the weapon is drawn for the bob, in screen pixels: bstone's 200-line pixels
    /// (6 at rest, 12 at the bottom of a swing) scaled to the view, whose 130 of them it fills
    /// </summary>
    internal static int WeaponBobDrop()
        => weaponBob ? (int)((BobMid * 2 - (int)bobOffset) * (viewheight + 1) / 130.0) : 0;
}
