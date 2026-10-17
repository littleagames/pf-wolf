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

    internal const string YesButtonName = "Y";
    internal const string NoButtonName = "N";

    /*
    =============================================================================

                                GLOBAL CONSTANTS

    =============================================================================
    */
    internal const int MaxTics = 10;
    internal const int DemoTics = 4;

    // TileMap holds a wall's id below BitWall: 1 to 255, all a plane-0 code can be (its high
    // byte is the wall patch). Wolf3D's WallShift was 6, so 63 walls.
    internal const int WallShift = 8;
    internal const int BitWall = (1 << WallShift);
    internal const int BitDoor = (1 << (WallShift + 1));
    internal const int BitAllTiles = (1 << (WallShift + 2));

    internal const int MaxActors = 150;
    internal const int MaxStats = 400;
    // A door tile in TileMap is BitDoor | its number: 128 doors (Wolf3D allowed 64; Midnight
    // Assault's Wolf4SDL raised it to 128). A door test must still come before the door-side
    // (BitWall) test, should MaxDoors ever pass BitWall.
    internal const int MaxDoors = 128;
    internal const int MaxVisible = 250;

    internal const int NumberChars = 9;

    //----------------

    internal const int RunSpeed = 6000;

    internal const float HeightRatio = 0.50f;

    internal const long PlayerSize = MinDist;// player radius
    internal const long MinActorDist = 0x10000L;// minimum dist from player center
                                                // to any actor center

    internal const float Pi = 3.141592657f;
    internal const float MathPi = Pi;
    internal const int UnsignedShift = 8;

    internal const int Angles = 360;
    internal const int AngleQuad = (Angles / 4);
    internal const int FineAngles = 3600;
    internal const int Ang90 = (FineAngles / 4);
    internal const int Ang180 = Ang90 * 2;
    internal const int Ang270 = Ang90 * 3;
    internal const int Ang360 = Ang90 * 4;
    internal const int Vang90 = (Angles / 4);
    internal const int Vang180 = Vang90 * 2;
    internal const int Vang270 = Vang90 * 3;
    internal const int Vang360 = Vang90 * 4;

    internal const long MinDist = 0x5800L;

    internal const int TextureShift = 6;
    internal const int Fixed2TexShift = (TextureShift == 8) ? 0 : (TextureShift == 7 ? 2 : 4);
    internal const int TextureSize = (1 << TextureShift);
    internal const int TextureMask = (TextureSize * (TextureSize - 1));

    internal const int North = 0;
    internal const int East = 1;
    internal const int South = 2;
    internal const int West = 3;

    /*
    =============================================================================

                                WL_PLAY DEFINITIONS

    =============================================================================
    */

    internal const int BaseMove  = 35;
    internal const int RunMove   = 70;
    internal const int BaseTurn  = 35;
    internal const int RunTurn = 70;

    internal const int JoyScale = 2;

    /*
    =============================================================================

                                 WL_STATE DEFINITIONS

    =============================================================================
    */
    internal const int TurnTics = 10;
    internal const int SpdPatrol = 512;
    internal const int SpdDog = 1500;

    /*
    =============================================================================

                    FROM WL_DRAW, WL_AGENT, WL_ACT2, WL_PLAY AND WL_MAIN

    =============================================================================
    */

    internal const int ActorSize = 0x4000;

    // tall walls: every wall is wallstories 64 unit stories tall, built up from the floor
    internal const int MaxWallStories = 8;

    // How high above the floor the view is, in texels (64 a story); see GameHost.WL_DRAW's Eye height
    internal const int EyeDefault = TextureSize / 2, MinEye = 4, MaxEye = TextureSize - 4;

    internal const long MoveScale = 150L;
    internal const long BackMoveScale = 100L;
    internal const int AngleScale = 20;

    internal const long ProjectileSize = 0xc000L;
    internal const int ProjSize = 0x2000;        // a projectile's box, either way (ProjectileTryMove)

    internal const int BjRunSpeed = 2048;
    internal const int BjJumpSpeed = 680;

    internal const int MaxX = 320;
    internal const int MaxY = 160;

    internal const int MaxGameName = 32;    // a save's name, as typed in the save menu, is one less

    internal const long FocalLength = (0x5700L);               // in global coordinates
    internal const int ViewGlobal = 0x10000;               // globals visable flush to wall

    internal static readonly int[] DirAngle ={0,Angles/8,2*Angles/8,3*Angles/8,4*Angles/8,
                       5*Angles/8,6*Angles/8,7*Angles/8,Angles};
}
