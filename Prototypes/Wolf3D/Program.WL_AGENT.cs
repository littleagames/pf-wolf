namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                    GLOBAL VARIABLES

    =============================================================================
    */

    //
    // player state info
    //
    static int thrustspeed;

    static ushort plux, pluy;          // player coordinates scaled to unsigned

    static short anglefrac;

    static objstruct? LastAttacker;


    static void StatusDrawPic (uint x, uint y, uint picnum)
    {
        VWB_DrawPicScaledCoord((int)(((screenWidth - scaleFactor * 320) / 16 + scaleFactor * x) * 8),
            (int)(screenHeight - scaleFactor * (STATUSLINES - y)), (int)picnum);
    }

    static void StatusDrawFace(uint picnum)
    {
        StatusDrawPic(17, 4, picnum);
    }

    static void LatchNumber(int x, int y, uint width, int number)
    {
        uint length, c;
        string str;

        str = number.ToString();

        length = (uint)str.Length;
        while (length < width)
        {
            StatusDrawPic((uint)x, (uint)y, (int)graphicnums.N_BLANKPIC);
            x++;
            width--;
        }

        c = length <= width ? 0 : length - width;

        while (c < length)
        {
            StatusDrawPic((uint)x, (uint)y, (uint)(str[(int)c] - '0' + (int)graphicnums.N_0PIC));
            x++;
            c++;
        }
    }

    static void DrawAmmo()
    {
        if (viewsize == 21 && ingame) return;
        LatchNumber(27, 16, 2, gamestate.ammo);
    }

    static void DrawFace()
    {
        if (viewsize == 21 && ingame) return;
        if (SD_SoundPlaying() == (int)soundnames.GETGATLINGSND)
            StatusDrawFace((uint)graphicnums.GOTGATLINGPIC);
        else if (gamestate.health != 0)
        {
            StatusDrawFace((uint)((uint)graphicnums.FACE1APIC + 3 * ((100 - gamestate.health) / 16) + gamestate.faceframe));
        }
        else
        {
            //if (LastAttacker && LastAttacker->obclass == needleobj)
            //    StatusDrawFace(MUTANTBJPIC);
            //else
                StatusDrawFace((uint)graphicnums.FACE8APIC);
        }
    }

    /*
    ===============
    =
    = UpdateFace
    =
    = Calls draw face if time to change
    =
    ===============
    */

    static int facecount = 0;
    static int facetimes = 0;

    static void DrawHealth()
    {
        if (viewsize == 21 && ingame) return;
        LatchNumber(21, 16, 3, gamestate.health);
    }

    static void DrawKeys()
    {
        if (viewsize == 21 && ingame) return;
        if ((gamestate.keys & 1) != 0)
            StatusDrawPic(30, 4, (int)graphicnums.GOLDKEYPIC);
        else
            StatusDrawPic(30, 4, (int)graphicnums.NOKEYPIC);

        if ((gamestate.keys & 2) != 0)
            StatusDrawPic(30, 20, (int)graphicnums.SILVERKEYPIC);
        else
            StatusDrawPic(30, 20, (int)graphicnums.NOKEYPIC);
    }

    static void DrawLevel()
    {
        if (viewsize == 21 && ingame) return;
        LatchNumber(2, 16, 2, gamestate.mapon + 1);
    }

    static void DrawLives()
    {
        if (viewsize == 21 && ingame) return;
        LatchNumber(14, 16, 1, gamestate.lives);
    }

    static void DrawScore()
    {
        if (viewsize == 21 && ingame) return;
        LatchNumber(6, 16, 6, gamestate.score);
    }

    static void DrawWeapon()
    {
        if (viewsize == 21 && ingame) return;
        StatusDrawPic(32, 8, (uint)(graphicnums.KNIFEPIC + gamestate.weapon));
    }
}
