namespace Wolf3D;

internal partial class Program
{
    internal static int DebugKeys()
    {
        bool esc;
        int level;
        int? spot;

        if (Keyboard[(int)ScanCodes.sc_B])             // B = border color
        {
            CenterWindow(20, 3);
            PrintY += 6;
            US_Print(" Border color (0-56): ");
            VW_UpdateScreen();
            string str = "";
            esc = !US_LineInput(px, py, ref str, "", true, 2, 0);
            if (!esc)
            {
                level = Convert.ToInt32(str);
                if (level >= 0 && level <= 99)
                {
                    if (level < 30) level += 31;
                    else
                    {
                        if (level > 56) level = 31;
                        else level -= 26;
                    }

                    bordercol = (byte)(level * 4 + 3);

                    if (bordercol == VIEWCOLOR)
                        DrawStatusBorder(bordercol);

                    return 1;
                }
            }
            return 1;
        }
        if (Keyboard[(int)ScanCodes.sc_C])             // C = count objects
        {
            CountObjects();
            return 1;
        }
        if (Keyboard[(int)ScanCodes.sc_D])             // D = Darkone's FPS counter
        {
            CenterWindow(22, 2);
            if (fpscounter)
                US_PrintCentered("Darkone's FPS Counter OFF");
            else
                US_PrintCentered("Darkone's FPS Counter ON");
            VW_UpdateScreen();
            IN_Ack();
            fpscounter ^= fpscounter;
            return 1;
        }
        if (Keyboard[(int)ScanCodes.sc_E])             // E = quit level
            playstate = (byte)playstatetypes.ex_completed;

        if (Keyboard[(int)ScanCodes.sc_F])             // F = facing spot
        {
            spot = actorat[player.tilex, player.tiley];

            CenterWindow(15, 9);
            US_Print($"X: {player.x} ({(player.x % TILEGLOBAL)})\n");
            US_Print($"Y: {player.y} ({(player.y % TILEGLOBAL)})\n");
            US_Print($"A: {player.angle}\n");
            US_Print($"TileX: {player.tilex}\n");
            US_Print($"TileY: {player.tiley}\n");
            US_Print($"1: {tilemap[player.tilex, player.tiley]}");
            US_Print($"2: {spot}");
            US_Print($"f 1: {player.areanumber}");
            US_Print($" 2: {MAPSPOT(player.tilex, player.tiley, 1)}");
            //snprintf(str, sizeof(str), , !ISPOINTER(spot) ? spotvis[player.tilex, player.tiley] : spot.flags);
            //US_Print($" 3: {}");

            VW_UpdateScreen();
            IN_Ack();
            return 1;
        }

        if (Keyboard[(int)ScanCodes.sc_G])             // G = god mode
        {
            CenterWindow(12, 2);
            if (godmode == 0)
                US_PrintCentered("God mode ON");
            else if (godmode == 1)
                US_PrintCentered("God (no flash)");
            else if (godmode == 2)
                US_PrintCentered("God mode OFF");

            VW_UpdateScreen();
            IN_Ack();
            if (godmode != 2)
                godmode++;
            else
                godmode = 0;
            return 1;
        }
        if (Keyboard[(int)ScanCodes.sc_H])             // H = hurt self
        {
            IN_ClearKeysDown();
            TakeDamage(16, null);
        }
        else if (Keyboard[(int)ScanCodes.sc_I])        // I = item cheat
        {
            CenterWindow(12, 3);
            US_PrintCentered("Free items!");
            VW_UpdateScreen();
            GivePoints(100000);
            HealSelf(99);
            if (gamestate.bestweapon < (short)weapontypes.wp_chaingun)
                GiveWeapon(gamestate.bestweapon + 1);
            gamestate.ammo += 50;
            if (gamestate.ammo > 99)
                gamestate.ammo = 99;
            DrawAmmo();
            IN_Ack();
            return 1;
        }
        else if (Keyboard[(int)ScanCodes.sc_K])        // K = give keys
        {
            CenterWindow(16, 3);
            PrintY += 6;
            US_Print("  Give Key (1-4): ");
            VW_UpdateScreen();
            string str = "";
            esc = !US_LineInput(px, py, ref str, "", true, 1, 0);
            if (!esc)
            {
                level = Convert.ToInt32(str);
                if (level > 0 && level < 5)
                    GiveKey(level - 1);
            }
            return 1;
        }
        else if (Keyboard[(int)ScanCodes.sc_L])        // L = level ratios
        {
            byte x, start, end = LRpack;

            if (end == 8)   // wolf3d
            {
                CenterWindow(17, 10);
                start = 0;
            }
            else            // sod
            {
                CenterWindow(17, 12);
                start = 0; end = 10;
            }

            while (true)
            {
                for (x = start; x < end; x++)
                {
                    US_Print((x + 1).ToString());
                    US_Print(" ");
                    US_Print((LevelRatios[x].time / 60).ToString());
                    US_Print(":");
                    if (LevelRatios[x].time % 60 < 10)
                        US_Print("0");
                    US_Print((LevelRatios[x].time % 60).ToString());
                    US_Print(" ");
                    US_Print((LevelRatios[x].kill).ToString());
                    US_Print("% ");
                    US_Print((LevelRatios[x].secret).ToString());
                    US_Print("% ");
                    US_Print((LevelRatios[x].treasure).ToString());
                    US_Print("%\n");
                }
                VW_UpdateScreen();
                IN_Ack();
                if (end == 10 && gamestate.mapon > 9)
                {
                    start = 10; end = 20;
                    CenterWindow(17, 12);
                }
                else
                    break;
            }

            return 1;
        }
        else if (Keyboard[(int)ScanCodes.sc_N])        // N = no clip
        {
            noclip ^= 1;
            CenterWindow(18, 3);
            if (noclip != 0)
                US_PrintCentered("No clipping ON");
            else
                US_PrintCentered("No clipping OFF");
            VW_UpdateScreen();
            IN_Ack();
            return 1;
        }
        else if (Keyboard[(int)ScanCodes.sc_O])        // O = basic overhead
        {
            BasicOverhead();
            return 1;
        }
        else if (Keyboard[(int)ScanCodes.sc_P])         // P = Ripper's picture grabber
        {
            PictureGrabber();
            return 1;
        }
        else if (Keyboard[(int)ScanCodes.sc_Q])        // Q = fast quit
            Quit("");
        else if (Keyboard[(int)ScanCodes.sc_S])        // S = slow motion
        {
            CenterWindow(30, 3);
            PrintY += 6;
            US_Print(" Slow Motion steps (default 14): ");
            VW_UpdateScreen();
            string str = "";
            esc = !US_LineInput(px, py, ref str, "", true, 2, 0);
            if (!esc)
            {
                level = Convert.ToInt32(str);
                if (level >= 0 && level <= 50)
                    singlestep = (byte)level;
            }
            return 1;
        }
        else if (Keyboard[(int)ScanCodes.sc_T])        // T = shape test
        {
            ShapeTest();
            return 1;
        }
        else if (Keyboard[(int)ScanCodes.sc_V])        // V = extra VBLs
        {
            CenterWindow(30, 3);
            PrintY += 6;
            US_Print("  Add how many extra VBLs(0-8): ");
            VW_UpdateScreen();
            string str = "";
            esc = !US_LineInput(px, py, ref str, "", true, 1, 0);
            if (!esc)
            {
                level = Convert.ToInt32(str);
                if (level >= 0 && level <= 8)
                    extravbls = level;
            }
            return 1;
        }
        else if (Keyboard[(int)ScanCodes.sc_W])        // W = warp to level
        {
            CenterWindow(26, 3);
            PrintY += 6;
            US_Print("  Warp to which level(1-10): ");
            VW_UpdateScreen();
            string str = "";
            esc = !US_LineInput(px, py, ref str, "", true, 2, 0);
            if (!esc)
            {
                level = Convert.ToInt32(str);
                if (level > 0 && level < 11)
                {
                    gamestate.mapon = (short)(level - 1);
                    playstate = (byte)playstatetypes.ex_warped;
                }
            }
            return 1;
        }
        else if (Keyboard[(int)ScanCodes.sc_X])        // X = item cheat
        {
            CenterWindow(12, 3);
            US_PrintCentered("Extra stuff!");
            VW_UpdateScreen();
            // DEBUG: put stuff here
            IN_Ack();
            return 1;
        }

        return 0;
    }

    internal static void CountObjects()
    {

    }

    internal static void PictureGrabber()
    {

    }

    internal static void BasicOverhead()
    {

    }

    internal static void ShapeTest()
    {

    }
}
