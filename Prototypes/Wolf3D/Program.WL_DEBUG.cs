using SDL2;
namespace Wolf3D;

internal partial class Program
{
    internal static int DebugKeys()
    {
        bool esc;
        int level;

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
            uint spot = actorat[player.tilex, player.tiley];

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
            US_Print($" 3: {(!ISPOINTER(spot, out var spotObj) ? (spotvis[player.tilex, player.tiley] ? 1 : 0) : spotObj.flags)}");

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
        int i, total, count, active, inactive, doors;
        objstruct obj;

        CenterWindow(17, 7);
        active = inactive = count = doors = 0;

        US_Print("Total statics :");
        total = laststatobj;
        US_Print(total.ToString());

        US_Print($"\nlaststatobj={laststatobj}");

        US_Print("\nIn use statics:");
        for (i = 0; i < total; i++)
        {
            if (statobjlist[i].shapenum != -1)
                count++;
            else
                doors++;        //debug
        }
        US_Print(count.ToString());

        US_Print("\nDoors         :");
        US_Print(doornum.ToString());

        for (int? o = player.next; o != null; o = obj.next)
        {
            obj = objlist[o.Value];
            if (obj.active != 0)
                active++;
            else
                inactive++;
        }

        US_Print("\nTotal actors  :");
        US_Print((active + inactive).ToString());

        US_Print("\nActive actors :");
        US_Print(active.ToString());

        VW_UpdateScreen();
        IN_Ack();
    }

    internal static void PictureGrabber()
    {
        int i;
        string fname = "WSHOT000.BMP";

        for (i = 0; i < 1000; i++)
        {
            fname = $"WSHOT{i:000}.BMP";
            if (!File.Exists(fname))
                break;
        }

        // overwrites WSHOT999.BMP if all wshot files exist

        SDL.SDL_SaveBMP(screenBuffer, fname);

        CenterWindow(18, 2);
        US_PrintCentered("Screenshot taken");
        VW_UpdateScreen();
        IN_Ack();
    }

    internal static void BasicOverhead()
    {

        int x, y;
        int zoom, temp;
        int offx, offy;
        uint tile;
        int color = 0;

        zoom = 128 / MAPSIZE;
        offx = 160;
        offy = (160 - (MAPSIZE * zoom)) / 2;

        //
        // right side (raw)
        //
        for (y = 0; y < mapheight; y++)
        {
            for (x = 0; x < mapwidth; x++)
                VWB_Bar((x * zoom) + offx, (y * zoom) + offy, zoom, zoom, (byte)actorat[x, y]);
        }

        //
        // left side (filtered)
        //
        offx -= 128;

        for (y = 0; y < mapheight; y++)
        {
            for (x = 0; x < mapwidth; x++)
            {
                tile = actorat[x, y];

                if (ISPOINTER(tile, out var check) && (check.flags & (uint)objflags.FL_SHOOTABLE) != 0)
                    color = 72;
                else if (tile==0 || ISPOINTER(tile, out check))
                {
                    if (spotvis[x, y])
                        color = 111;
                    else
                        color = 0;      // nothing
                }
                else if (MAPSPOT(x, y, 1) == PUSHABLETILE)
                    color = 171;
                else if (tile == BIT_WALL)
                    color = 158;
                else if (tile < BIT_DOOR)
                    color = 154;
                else if (tile < BIT_ALLTILES)
                    color = 146;

                VWB_Bar((x * zoom) + offx, (y * zoom) + offy, zoom, zoom, color);
            }
        }

        VWB_Bar((player.tilex * zoom) + offx, (player.tiley * zoom) + offy, zoom, zoom, 15);

        VW_UpdateScreen();
        IN_Ack();
    }

    internal static void ShapeTest()
    {
        bool done;
        int scan;
        int i, j, k, x;
        int v2;
        int oldviewheight;
        uint l;
        byte v;
        byte[] addr;
        int sound;

        CenterWindow(20, 16);
        VW_UpdateScreen();

        i = 0;
        done = false;

        while (!done)
        {
            US_ClearWindow();
            sound = -1;

            US_Print(" Page #");
            US_Print(i.ToString());

            if (i < PMSpriteStart)
                US_Print(" (Wall)");
            else if (i < PMSoundStart)
                US_Print(" (Sprite)");
            else if (i == ChunksInFile - 1)
                US_Print(" (Sound Info)");
            else
                US_Print(" (Sound)");

            US_Print("\n Address: ");
            addr = PM_GetPage(i);
            //snprintf(str, sizeof(str), "0x%010X", (uintptr_t)addr);
            US_Print(i.ToString());

            if (addr != null && addr.Length > 0)
            {
                if (i < PMSpriteStart)
                {
                    //
                    // draw the wall
                    //
                    vbufPtr = VL_LockSurface(screenBuffer);

                    if (vbufPtr == IntPtr.Zero)
                        Quit("ShapeTest: Unable to create surface for walls!");

                    postx = (screenWidth / 2) - ((TEXTURESIZE / 2) * scaleFactor);
                    postsource = addr;

                    centery = (short)(screenHeight / 2);
                    oldviewheight = viewheight;
                    viewheight = 0x7fff;            // quick hack to skip clipping

                    for (x = 0, j = 0; x < TEXTURESIZE * scaleFactor; x++, j++, postx++)
                    {
                        wallheight[postx] = (short)(256 * scaleFactor);
                        ScalePost();

                        if (j == scaleFactor)
                        {
                            j = 0;
                            postsource = postsource.Skip(TEXTURESIZE).ToArray();
                        }
                    }

                    viewheight = oldviewheight;
                    centery = (short)(viewheight / 2);

                    VL_UnlockSurface(screenBuffer);
                    vbufPtr = IntPtr.Zero;
                }
                else if (i < PMSoundStart)
                {
                    //
                    // draw the sprite
                    //
                    vbufPtr = VL_LockSurface(screenBuffer);

                    if (vbufPtr == IntPtr.Zero)
                        Quit("ShapeTest: Unable to create surface for sprites!");

                    centery = (short)(screenHeight / 2);
                    oldviewheight = viewheight;
                    viewheight = 0x7fff;            // quick hack to skip clipping

                    SimpleScaleShape(screenWidth / 2, i - PMSpriteStart, 64 * scaleFactor);

                    viewheight = oldviewheight;
                    centery = (short)(viewheight / 2);

                    VL_UnlockSurface(screenBuffer);
                    vbufPtr = IntPtr.Zero;
                }
                else if (i == ChunksInFile - 1)
                {
                    //
                    // display sound info
                    //
                    US_Print("\n\n Number of sounds: ");
                    US_Print(NumDigi.ToString());

                    for (l = (uint)(j = 0); j < NumDigi; j++)
                        l += DigiList[j].length;

                    US_Print("\n Total bytes: ");
                    US_Print(l.ToString());
                    US_Print("\n Total pages: ");
                    US_Print((ChunksInFile - PMSoundStart - 1).ToString());
                }
                else
                {
                    //
                    // display sounds
                    //
                    for (j = 0; j < NumDigi; j++)
                    {
                        if (j == NumDigi - 1)
                            k = ChunksInFile - 1;    // don't let it overflow
                        else
                            k = (int)DigiList[j + 1].startpage;

                        if (i >= PMSoundStart + DigiList[j].startpage && i < PMSoundStart + k)
                            break;
                    }

                    if (j < NumDigi)
                    {
                        sound = j;

                        US_Print("\n Sound #");
                        US_Print(j.ToString());
                        US_Print("\n Segment #");
                        US_Print(((int)(i - PMSoundStart - DigiList[j].startpage)).ToString());
                    }

                    for (j = 0; j < pageLengths[i]; j += 32)
                    {
                        v = addr[j];
                        v2 = v;
                        v2 -= 128;
                        v2 /= 4;

                        if (v2 < 0)
                            VWB_Vlin(WindowY + WindowH - 32 + v2,
                                      WindowY + WindowH - 32,
                                      WindowX + 8 + (j / 32), BLACK);
                        else
                            VWB_Vlin(WindowY + WindowH - 32,
                                      WindowY + WindowH - 32 + v2,
                                      WindowX + 8 + (j / 32), BLACK);
                    }
                }
            }

            VW_UpdateScreen();

            IN_Ack();
            scan = LastScan;

            IN_ClearKey(scan);

            switch ((ScanCodes)scan)
            {
                case ScanCodes.sc_LeftArrow:
                    if (i != 0)
                        i--;
                    break;

                case ScanCodes.sc_RightArrow:
                    if (++i >= ChunksInFile)
                        i--;
                    break;

                case ScanCodes.sc_W:      // Walls
                    i = 0;
                    break;

                case ScanCodes.sc_S:      // Sprites
                    i = PMSpriteStart;
                    break;

                case ScanCodes.sc_D:      // Digitized
                    i = PMSoundStart;
                    break;

                case ScanCodes.sc_I:      // Digitized info
                    i = ChunksInFile - 1;
                    break;

                case ScanCodes.sc_P:
                    if (sound != -1)
                        SD_PlayDigitized((ushort)sound, 8, 8);
                    break;

                case ScanCodes.sc_Escape:
                    done = true;
                    break;
            }
        }

        SD_StopDigitized();
    }
}
