namespace PFWolf.Gameplay;

/// <summary>
/// The game's constants from id's WL_DEF.H (and WL_PLAY's and WL_STATE's): tics, tile bits, sizes,
/// angles, texture sizes, directions and speeds. GameHost has same-named forwards.
/// </summary>
internal static class GameConstants
{
    /*
    =============================================================================

                                MACROS

    =============================================================================
    */

    internal const string YESBUTTONNAME = "Y";
    internal const string NOBUTTONNAME = "N";

    /*
    =============================================================================

                                GLOBAL CONSTANTS

    =============================================================================
    */
    internal const int MAXTICS = 10;
    internal const int DEMOTICS = 4;

    // tilemap holds a wall's id below BIT_WALL: 1 to 255, all a plane-0 code can be (its high
    // byte is the wall patch). Wolf3D's WALLSHIFT was 6, so 63 walls.
    internal const int WALLSHIFT = 8;
    internal const int BIT_WALL = (1 << WALLSHIFT);
    internal const int BIT_DOOR = (1 << (WALLSHIFT + 1));
    internal const int BIT_ALLTILES = (1 << (WALLSHIFT + 2));

    internal const int MAXACTORS = 150;
    internal const int MAXSTATS = 400;
    // A door tile in tilemap is BIT_DOOR | its number: 128 doors (Wolf3D allowed 64; Midnight
    // Assault's Wolf4SDL raised it to 128). A door test must still come before the door-side
    // (BIT_WALL) test, should MAXDOORS ever pass BIT_WALL.
    internal const int MAXDOORS = 128;
    internal const int MAXVISABLE = 250;

    internal const int NUMBERCHARS = 9;

    //----------------

    internal const int RUNSPEED = 6000;

    internal const float HEIGHTRATIO = 0.50f;

    internal const long PLAYERSIZE = MINDIST;// player radius
    internal const long MINACTORDIST = 0x10000L;// minimum dist from player center
                                                // to any actor center

    internal const float PI = 3.141592657f;
    internal const float M_PI = PI;
    internal const int UNSIGNEDSHIFT = 8;

    internal const int ANGLES = 360;
    internal const int ANGLEQUAD = (ANGLES / 4);
    internal const int FINEANGLES = 3600;
    internal const int ANG90 = (FINEANGLES / 4);
    internal const int ANG180 = ANG90 * 2;
    internal const int ANG270 = ANG90 * 3;
    internal const int ANG360 = ANG90 * 4;
    internal const int VANG90 = (ANGLES / 4);
    internal const int VANG180 = VANG90 * 2;
    internal const int VANG270 = VANG90 * 3;
    internal const int VANG360 = VANG90 * 4;

    internal const long MINDIST = 0x5800L;

    internal const int TEXTURESHIFT = 6;
    internal const int FIXED2TEXSHIFT = (TEXTURESHIFT == 8) ? 0 : (TEXTURESHIFT == 7 ? 2 : 4);
    internal const int TEXTURESIZE = (1 << TEXTURESHIFT);
    internal const int TEXTUREMASK = (TEXTURESIZE * (TEXTURESIZE - 1));

    internal const int NORTH = 0;
    internal const int EAST = 1;
    internal const int SOUTH = 2;
    internal const int WEST = 3;

    /*
    =============================================================================

                                WL_PLAY DEFINITIONS

    =============================================================================
    */

    internal const int BASEMOVE  = 35;
    internal const int RUNMOVE   = 70;
    internal const int BASETURN  = 35;
    internal const int RUNTURN = 70;

    internal const int JOYSCALE = 2;

    /*
    =============================================================================

                                 WL_STATE DEFINITIONS

    =============================================================================
    */
    internal const int TURNTICS = 10;
    internal const int SPDPATROL = 512;
    internal const int SPDDOG = 1500;

    /*
    =============================================================================

                    FROM WL_DRAW, WL_AGENT, WL_ACT2, WL_PLAY AND WL_MAIN

    =============================================================================
    */

    internal const int ACTORSIZE = 0x4000;

    // tall walls: every wall is wallstories 64 unit stories tall, built up from the floor
    internal const int MAXWALLSTORIES = 8;

    // How high above the floor the view is, in texels (64 a story); see GameHost.WL_DRAW's Eye height
    internal const int EYEDEFAULT = TEXTURESIZE / 2, MINEYE = 4, MAXEYE = TEXTURESIZE - 4;

    internal const long MOVESCALE = 150L;
    internal const long BACKMOVESCALE = 100L;
    internal const int ANGLESCALE = 20;

    internal const long PROJECTILESIZE = 0xc000L;
    internal const int PROJSIZE = 0x2000;        // a projectile's box, either way (ProjectileTryMove)

    internal const int BJRUNSPEED = 2048;
    internal const int BJJUMPSPEED = 680;

    internal const int MAXX = 320;
    internal const int MAXY = 160;

    internal const int MaxGameName = 32;    // a save's name, as typed in the save menu, is one less

    internal const long FOCALLENGTH = (0x5700L);               // in global coordinates
    internal const int VIEWGLOBAL = 0x10000;               // globals visable flush to wall

    internal static readonly int[] dirangle ={0,ANGLES/8,2*ANGLES/8,3*ANGLES/8,4*ANGLES/8,
                       5*ANGLES/8,6*ANGLES/8,7*ANGLES/8,ANGLES};
}
