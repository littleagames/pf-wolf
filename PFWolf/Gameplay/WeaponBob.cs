namespace PFWolf.Gameplay;

// The weapon in hand bobbing as the player walks (game-info weapon-bob), as Planet Strike's
// (bstone 3d_agent.cpp HandleWeaponBounce). It springs between two points while the player moves
// forwards or back and settles back to the middle when they stop. Only the local player's view
// has it, and it's never saved: a level or loaded game starts it at rest.
internal sealed class WeaponBob(Configuration.GameSettings? settings = null)
{
    // The Game Options weaponbob setting (its default from game-info), asked each frame; set
    // outright, it stays so (tests)
    private bool? enabled;
    internal bool Enabled { get => enabled ?? settings?.Options.WeaponBob ?? false; set => enabled = value; }

    // In bstone's 200-line pixels: where the weapon swings between, and how far it may overshoot
    private const double BobMax = 10, BobMid = 6, BobMin = 2;
    private const double BobMaxOffset = BobMax + 2, BobMinOffset = BobMin - 2;

    // SinTable[90] at bstone's full view size: its spring's pull
    private const double BobPull = 1.0;

    private double bobOffset = BobMid, bobVel, bobDest = BobMax;
    private int bobMoving;                  // tics left before a stop lets the weapon settle

    internal void Init()
    {
        bobOffset = BobMid;
        bobDest = BobMax;
        bobVel = 0;
        bobMoving = 0;
    }

    /// <summary>
    /// Moves the bob on by this frame's tics, from whether the player is walking forwards or back
    /// (controlY, the input's ControlY). bstone steps it once a frame, which at its 70 frames a
    /// second is once a tic.
    /// </summary>
    internal void Update(int controlY, uint tics)
    {
        if (!Enabled)
            return;

        for (int i = 0; i < tics; i++)
        {
            if (controlY != 0)
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
    internal int Drop(int viewheight)
        => Enabled ? (int)((BobMid * 2 - (int)bobOffset) * (viewheight + 1) / 130.0) : 0;
}
