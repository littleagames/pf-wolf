namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                  SPRITE DRAWING ROUTINES

    =============================================================================
    */


    /*
    ===================
    =
    = ScaleLine
    =
    = Reconstruct a sprite and draw it
    =
    = each vertical line of the shape has a pointer to segment data:
    = 	end of segment pixel*2 (0 terminates line)
    = 	top of virtual line with segment in proper place
    =	start of segment pixel*2
    =	<repeat>
    =
    ===================
    */

    internal static void ScaleLine(short x, short toppix, int fracstep, byte[] linesrc, byte[] linecmds)
    {
        int src;
        int color;
        int start, end, top;
        int startpix, endpix;
        int frac;
        int linecmdsIndex = 0;

        for (end = BitConverter.ToInt16(linecmds.Skip(linecmdsIndex).ToArray()) >> 1; end != 0; end = BitConverter.ToInt16(linecmds.Skip(linecmdsIndex).ToArray()) >> 1)
        {
            top = BitConverter.ToInt16(linecmds.Skip(linecmdsIndex+2).ToArray());
            start = BitConverter.ToInt16(linecmds.Skip(linecmdsIndex+4).ToArray()) >> 1;

            frac = start* fracstep;

            endpix = (frac >> FRACBITS) + toppix;

            for (src = top + start; start != end; start++, src++)
            {
                startpix = endpix;

                if (startpix >= viewheight)
                    break;                          // off the bottom of the view area

                frac += fracstep;
                endpix = (frac >> FRACBITS) + toppix;

                if (endpix< 0)
                    continue;                       // not into the view area

                if (startpix< 0)
                    startpix = 0;                   // clip upper boundary

                if (endpix > viewheight)
                    endpix = viewheight;            // clip lower boundary

                color = linesrc[src];

                var destIndex = ylookup[startpix] + x;
                unsafe
                {
                    byte* dest = (byte*)vbufPtr + screenofs;// + destIndex;
                    while (startpix < endpix)
                    {
                        dest[destIndex] = (byte)color;
                        destIndex += bufferPitch;
                        startpix++;
                    }
                }
            }

            linecmdsIndex += 6;                          // next segment list
        }
    }

    /*
    ===================
    =
    = ScaleShape
    =
    = Draws a compiled shape at [height] pixels high
    =
    ===================
    */
    internal static void ScaleShape(visobj_t sprite)
    {
        int i;
        compshape_t shape;
        byte[] linesrc, linecmds;
        int height, toppix;
        int x1, x2, xcenter;
        int       frac, fracstep;

        height = sprite.viewheight >> 3;        // low three bits are fractional

        if (height == 0)
            return;                 // too close or far away

        linesrc = PM_GetSpritePage(sprite.shapenum);
        shape = new compshape_t(linesrc);// (compshape_t*)linesrc; // this needs to build the struct from the byte[], and get the table data afterwards
        fracstep = FixedDiv(height, TEXTURESIZE / 2);
        frac = shape.leftpix * fracstep;

        xcenter = sprite.viewx - height;
        toppix = centery - height;

        x2 = (frac >> FRACBITS) + xcenter;

        for (i = shape.leftpix; i <= shape.rightpix; i++)
        {
            //
            // calculate edges of the shape
            //
            x1 = x2;

            if (x1 >= viewwidth)
                break;                // off the right side of the view area

            frac += fracstep;
            x2 = (frac >> FRACBITS) + xcenter;

            if (x2 < 0)
                continue;             // not into the view area

            if (x1 < 0)
                x1 = 0;               // clip left boundary

            if (x2 > viewwidth)
                x2 = viewwidth;       // clip right boundary

            while (x1 < x2)
            {
                if (wallheight[x1] < sprite.viewheight)
                {
                    linecmds = linesrc.Skip(shape.dataofs[i - shape.leftpix]).ToArray(); // this needs to get a subset of data

                    ScaleLine((short)x1, (short)toppix, fracstep, linesrc, linecmds);
                }

                x1++;
            }
        }
    }

    /*
    ===================
    =
    = SimpleScaleShape
    =
    = NO CLIPPING, height in pixels
    =
    = Draws a compiled shape at [height] pixels high
    =
    ===================
    */
    internal static void SimpleScaleShape (int dispx, int shapenum, int dispheight)
    {
        int i;
        compshape_t shape;
        byte[] linesrc, linecmds;
        int height, toppix;
        int x1, x2, xcenter;
        int frac, fracstep;

        height = dispheight >> 1;

        linesrc = PM_GetSpritePage(shapenum);
        shape = new compshape_t(linesrc);
        fracstep = FixedDiv(height, TEXTURESIZE / 2);
        frac = shape.leftpix * fracstep;

        xcenter = dispx - height;
        toppix = centery - height;

        x2 = (frac >> FRACBITS) + xcenter;

        for (i = shape.leftpix; i <= shape.rightpix; i++)
        {
            //
            // calculate edges of the shape
            //
            x1 = x2;

            frac += fracstep;
            x2 = (frac >> FRACBITS) + xcenter;

            while (x1 < x2)
            {
                linecmds = linesrc.Skip(shape.dataofs[i - shape.leftpix]).ToArray(); // this needs to get a subset of data

                ScaleLine((short)x1, (short)toppix, fracstep, linesrc, linecmds);

                x1++;
            }
        }
    }
}
