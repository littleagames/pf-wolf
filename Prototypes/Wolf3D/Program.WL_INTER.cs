namespace Wolf3D;

internal partial class Program
{
    internal static void NonShareware()
    {
        VW_FadeOut();

        ClearMScreen();
        DrawStripes(10);

        fontnumber = 1;

        SETFONTCOLOR(READHCOLOR, BKGDCOLOR);
        PrintX = 110;
        PrintY = 15;

        US_Print("Attention");

        SETFONTCOLOR(HIGHLIGHT, BKGDCOLOR);
        WindowX = PrintX = 40;
        PrintY = 60;

        US_Print("This game is NOT shareware.\n");
        US_Print("Please do not distribute it.\n");
        US_Print("Thanks.\n\n");

        US_Print("        Id Software\n");

        VW_UpdateScreen();
        VW_FadeIn();
        IN_Ack();
    }

    internal static void PG13()
    {
        VW_FadeOut();
        VWB_Bar(0, 0, 320, 200, 0x82);     // background

        VWB_DrawPic(216, 110, (int)graphicnums.PG13PIC);
        VW_UpdateScreen();

        VW_FadeIn();
        IN_UserInput(TickBase * 7);

        VW_FadeOut();
    }

    internal static void DrawHighScores()
    {
        ushort i, w, h;

        ClearMScreen();
        DrawStripes(10);

        VWB_DrawPic(48, 0, (int)graphicnums.HIGHSCORESPIC);

        VWB_DrawPic(4 * 8, 68, (int)graphicnums.C_NAMEPIC);
        VWB_DrawPic(20 * 8, 68, (int)graphicnums.C_LEVELPIC);
        VWB_DrawPic(28 * 8, 68, (int)graphicnums.C_SCOREPIC);

        fontnumber = 0;
        SETFONTCOLOR(15, 0x29);


        for (i = 0; i < MaxScores; i++)
        {
            HighScore s = Scores[i];
            PrintY = (ushort)(76 + (16 * i));

            //
            // name
            //
            PrintX = 4 * 8;
            US_Print(new string(s.name));

            //
            // level
            //
            var buffer = s.completed.ToString();
            USL_MeasureString(buffer, out w, out h);
            PrintX = (ushort)((22 * 8) - w);

            PrintX -= 6;
            var buffer1 = (s.episode + 1).ToString();
            US_Print("E");
            US_Print(buffer1);
            US_Print("/L");

            US_Print(buffer);

            //
            // score
            //

            buffer = s.score.ToString();
            USL_MeasureString(buffer, out w, out h);
            PrintX = (ushort)((34 * 8) - 8 - w);
            US_Print(buffer);
        }

        VW_UpdateScreen();
    }
}
