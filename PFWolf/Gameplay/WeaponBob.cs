namespace PFWolf.Gameplay;

// The weapon in hand bobbing as the player walks (game-info weapon-bob), in one of two ways
// (weapon-bob-sway). As Planet Strike's (bstone 3d_agent.cpp HandleWeaponBounce), it springs
// up and down between two points while the player moves forwards or back and settles back to the
// middle when they stop. As Doom's (p_pspr.c A_WeaponReady, p_user.c P_CalcHeight), it swings side
// to side in an arc that dips in the middle, as wide as the player is fast, and holds still while
// the weapon fires. Only the local player's view has it, and it's never saved: a level or loaded
// game starts it at rest.
internal sealed class WeaponBob(Configuration.GameSettings? settings = null)
{
    // The Game Options weaponbob and weaponbob_sway settings (their defaults from game-info),
    // asked each frame; set outright, they stay so (tests)
    private bool? enabled, sway;
    internal bool Enabled { get => enabled ?? settings?.Options.WeaponBob ?? false; set => enabled = value; }
    internal bool Sway { get => sway ?? settings?.Options.WeaponBobSway ?? false; set => sway = value; }

    // In bstone's 200-line pixels: where the weapon swings between, and how far it may overshoot
    private const double BobMax = 10, BobMid = 6, BobMin = 2;
    private const double BobMaxOffset = BobMax + 2, BobMinOffset = BobMin - 2;

    // SinTable[90] at bstone's full view size: its spring's pull
    private const double BobPull = 1.0;

    private double bobOffset = BobMid, bobVel, bobDest = BobMax;
    private int bobMoving;                  // tics left before a stop lets the weapon settle

    // Doom's MAXBOB, 16 pixels of its 168-line view, as a share of the view's height: how far
    // the swing goes each way, and how far it dips
    private const double SwayMax = 16.0 / 168;

    // Doom turns its swing 128 of 8192 fine angles a 35 Hz tic: once round in 128 of ours
    private const double SwayStep = 2 * Math.PI / 128;

    // Walking pace, a tic's BaseMove: as fast as the swing goes full width. Doom's swing is at
    // its widest at a walk too.
    private const double SwayFullSpeed = GameConstants.BaseMove * GameConstants.MoveScale;

    // How much of the way to its new width the swing goes in a tic, as Doom's momentum builds
    // and falls away
    private const double SwayEase = 0.15;

    private double swayAngle, swayWidth;    // swayWidth: 0 to 1 of SwayMax
    private double swayX, swayY;            // where it is now, in shares of SwayMax

    internal void Init()
    {
        bobOffset = BobMid;
        bobDest = BobMax;
        bobVel = 0;
        bobMoving = 0;
        swayAngle = swayWidth = swayX = swayY = 0;
    }

    /// <summary>
    /// Moves the bob on by this frame's tics, from whether the player is walking forwards or back
    /// (controlY, the input's ControlY) for Planet Strike's, or how far they went (moved, in
    /// global units) and whether the weapon's ready rather than firing for Doom's. bstone steps
    /// it once a frame, which at its 70 frames a second is once a tic.
    /// </summary>
    internal void Update(int controlY, uint tics, double moved = 0, bool ready = true)
    {
        if (!Enabled)
            return;

        if (Sway)
        {
            UpdateSway(moved, ready, tics);
            return;
        }

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

    // Doom's: the swing turns all the time (its leveltime), and only its width follows the player
    private void UpdateSway(double moved, bool ready, uint tics)
    {
        if (tics == 0)
            return;

        double target = Math.Min(moved / tics / SwayFullSpeed, 1);
        for (int i = 0; i < tics; i++)
        {
            swayAngle = (swayAngle + SwayStep) % (2 * Math.PI);
            swayWidth += (target - swayWidth) * SwayEase;
        }

        // Firing, the weapon stays where the swing left it (Doom only bobs it in A_WeaponReady)
        if (ready)
        {
            swayX = swayWidth * Math.Cos(swayAngle);
            swayY = swayWidth * Math.Abs(Math.Sin(swayAngle));
        }
    }

    /// <summary>
    /// How far down the weapon is drawn for the bob, in screen pixels. Planet Strike's is
    /// bstone's 200-line pixels (6 at rest, 12 at the bottom of a swing) scaled to the view, whose
    /// 130 of them it fills; Doom's dips as far as SwayMax of the view in the middle of its swing.
    /// </summary>
    internal int Drop(int viewheight)
    {
        if (!Enabled)
            return 0;
        if (Sway)
            return (int)(swayY * SwayMax * (viewheight + 1));
        return (int)((BobMid * 2 - (int)bobOffset) * (viewheight + 1) / 130.0);
    }

    /// <summary>How far right the weapon is drawn for Doom's side-to-side bob, in screen pixels (left is negative)</summary>
    internal int Across(int viewheight)
        => Enabled && Sway ? (int)(swayX * SwayMax * (viewheight + 1)) : 0;
}
