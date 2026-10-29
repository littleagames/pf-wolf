namespace PFWolf.Gameplay;

/// <summary>
/// How a weapon bobs (weapon.bobstyle): Planet Strike's spring, or one of ZDoom's Weapon.BobStyle
/// swings. Each swing goes side to side; they differ in where it's lowest.
/// </summary>
internal enum WeaponBobStyle
{
    Spring,             // Planet Strike's: up and down only, while walking forwards or back
    Normal,             // Doom's: a round U, lowest in the middle of the swing
    Inverse,            // a round arch, lowest at the sides
    Alpha,              // a pointed arch: straight lines, lowest at the sides
    InverseAlpha,       // a V: straight lines, lowest in the middle
    Smooth,             // a gentler U
    InverseSmooth,      // a gentler arch
    None,
}

/// <summary>
/// A weapon's bob: its style (null: the player's Bob Side to Side setting picks) and how far
/// across, how far down and how fast it goes, times the usual (weapon.bobrangex, bobrangey,
/// bobspeed)
/// </summary>
internal readonly record struct WeaponBobShape(WeaponBobStyle? Style, double RangeX, double RangeY, double Speed)
{
    internal static readonly WeaponBobShape Default = new(null, 1, 1, 1);

    /// <summary>A weapon.bobstyle name (any case), or null when it's none of them (a number included)</summary>
    internal static WeaponBobStyle? ParseStyle(string? name) =>
        name?.Trim() is { Length: > 0 } trimmed && char.IsLetter(trimmed[0])
            && Enum.TryParse<WeaponBobStyle>(trimmed, ignoreCase: true, out var style) ? style : null;
}

// The weapon in hand bobbing as the player walks (game-info weapon-bob), in the weapon's style
// (weapon.bobstyle), else as the player picks (weapon-bob-sway). Planet Strike's (bstone
// 3d_agent.cpp HandleWeaponBounce) springs up and down between two points while the player moves
// forwards or back and settles back to the middle when they stop. Doom's and ZDoom's (p_pspr.c
// A_WeaponReady, gzdoom P_BobWeapon) swing side to side as wide as the player is fast, and hold
// still while the weapon fires. Only the local player's view has it, and it's never saved: a
// level or loaded game starts it at rest.
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
    private int bobMoving;                  // spring steps left before a stop lets the weapon settle
    private double springSteps;             // steps owed the spring: one a tic, times weapon.bobspeed

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

    private WeaponBobShape shape = WeaponBobShape.Default;     // the weapon in hand's, as of the last update

    /// <summary>The style the weapon in hand bobs in: its own, else the player's setting's</summary>
    internal WeaponBobStyle Style => shape.Style ?? (Sway ? WeaponBobStyle.Normal : WeaponBobStyle.Spring);

    internal void Init()
    {
        bobOffset = BobMid;
        bobDest = BobMax;
        bobVel = 0;
        bobMoving = 0;
        springSteps = 0;
        swayAngle = swayWidth = swayX = swayY = 0;
    }

    /// <summary>
    /// Moves the bob on by this frame's tics, from whether the player is walking forwards or back
    /// (controlY, the input's ControlY) for the spring, or how far they went (moved, in global
    /// units) and whether the weapon's ready rather than firing for a swing. The weapon in hand's
    /// shape (weapon.bob*) says which and how much; left out, the usual. bstone steps its spring
    /// once a frame, which at its 70 frames a second is once a tic.
    /// </summary>
    internal void Update(int controlY, uint tics, double moved = 0, bool ready = true, WeaponBobShape? weaponShape = null)
    {
        if (!Enabled)
            return;

        shape = weaponShape ?? WeaponBobShape.Default;
        switch (Style)
        {
            case WeaponBobStyle.None:
                return;
            case WeaponBobStyle.Spring:
                UpdateSpring(controlY, tics);
                return;
            default:
                UpdateSway(moved, ready, tics);
                return;
        }
    }

    private void UpdateSpring(int controlY, uint tics)
    {
        springSteps += tics * Math.Max(shape.Speed, 0);
        for (; springSteps >= 1; springSteps--)
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

    // A swing turns all the time (Doom's leveltime), and only its width follows the player
    private void UpdateSway(double moved, bool ready, uint tics)
    {
        if (tics == 0)
            return;

        double target = Math.Min(moved / tics / SwayFullSpeed, 1);
        for (int i = 0; i < tics; i++)
        {
            swayAngle = (swayAngle + SwayStep * Math.Max(shape.Speed, 0)) % (2 * Math.PI);
            swayWidth += (target - swayWidth) * SwayEase;
        }

        // Firing, the weapon stays where the swing left it (Doom only bobs it in A_WeaponReady)
        if (!ready)
            return;

        double sin = Math.Sin(swayAngle), cos = Math.Cos(swayAngle);
        (double across, double down) = Style switch
        {
            WeaponBobStyle.Normal => (cos, Math.Abs(sin)),
            WeaponBobStyle.Inverse => (cos, 1 - Math.Abs(sin)),
            WeaponBobStyle.Alpha => (sin, Math.Abs(sin)),
            WeaponBobStyle.InverseAlpha => (sin, 1 - Math.Abs(sin)),
            WeaponBobStyle.Smooth => (cos, (1 - Math.Cos(2 * swayAngle)) / 2),
            WeaponBobStyle.InverseSmooth => (cos, (1 + Math.Cos(2 * swayAngle)) / 2),
            _ => (0.0, 0.0),
        };
        swayX = swayWidth * shape.RangeX * across;
        swayY = swayWidth * shape.RangeY * down;
    }

    /// <summary>
    /// How far down the weapon is drawn for the bob, in screen pixels. The spring is bstone's
    /// 200-line pixels (6 at rest, 12 at the bottom of a swing) scaled to the view, whose 130 of
    /// them it fills; a swing dips as far as SwayMax of the view. Both times weapon.bobrangey.
    /// </summary>
    internal int Drop(int viewheight)
    {
        if (!Enabled)
            return 0;
        return Style switch
        {
            WeaponBobStyle.None => 0,
            WeaponBobStyle.Spring => (int)((BobMid * 2 - (int)bobOffset) * shape.RangeY * (viewheight + 1) / 130.0),
            _ => (int)(swayY * SwayMax * (viewheight + 1)),
        };
    }

    /// <summary>How far right the weapon is drawn for a side-to-side swing, in screen pixels (left is negative)</summary>
    internal int Across(int viewheight)
        => Enabled && Style is not (WeaponBobStyle.Spring or WeaponBobStyle.None) ? (int)(swayX * SwayMax * (viewheight + 1)) : 0;
}
