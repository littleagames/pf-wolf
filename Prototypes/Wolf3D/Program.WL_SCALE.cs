using Wolf3D.Assets;

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
    = ScaleColumn
    =
    = Draws one column of a sprite's indexed bitmap, scaling its rows onto
    = the screen column at x. Rows whose OpacityMask entry is 0 are skipped.
    =
    ===================
    */

    internal static void ScaleColumn(short x, short toppix, int fracstep, SpriteAsset sprite, int column)
    {
        int height = sprite.Height;
        int width = sprite.Width;
        byte[] indices = sprite.RawData;
        byte[] mask = sprite.OpacityMask;

        int frac = 0;
        int startpix;
        int endpix = toppix;

        for (int row = 0; row < height; row++)
        {
            startpix = endpix;

            if (startpix >= viewheight)
                break;                          // off the bottom of the view area

            frac += fracstep;
            endpix = (frac >> MathUtils.FRACBITS) + toppix;

            if (endpix < 0)
                continue;                       // not into the view area

            if (startpix < 0)
                startpix = 0;                   // clip upper boundary

            if (endpix > viewheight)
                endpix = viewheight;            // clip lower boundary

            int srcIndex = row * width + column;
            if (mask[srcIndex] == 0)
                continue;                       // transparent pixel

            byte color = indices[srcIndex];

            var destIndex = _videoManager.ylookup[startpix] + x;
            unsafe
            {
                byte* dest = (byte*)vbufPtr + screenofs;
                while (startpix < endpix)
                {
                    dest[destIndex] = color;
                    destIndex += _videoManager.bufferPitch;
                    startpix++;
                }
            }
        }
    }

    /*
    ===================
    =
    = ScaleShape
    =
    = Draws a sprite's indexed bitmap at [height] pixels high
    =
    ===================
    */
    internal static void ScaleShape(visobj_t sprite)
    {
        int height, toppix;
        int x1, x2, xcenter;
        int frac, fracstep;

        height = sprite.viewheight >> 3;        // low three bits are fractional

        if (height == 0)
            return;                 // too close or far away

        var spriteAsset = _assetManager.Find<SpriteAsset>(sprite.shapenum);
        if (spriteAsset == null)
            return;

        fracstep = MathUtils.FixedDiv(height, TEXTURESIZE / 2);
        frac = 0;

        xcenter = sprite.viewx - height;
        toppix = centery - height;

        x2 = xcenter;

        for (int i = 0; i < spriteAsset.Width; i++)
        {
            //
            // calculate edges of the shape
            //
            x1 = x2;

            if (x1 >= viewwidth)
                break;                // off the right side of the view area

            frac += fracstep;
            x2 = (frac >> MathUtils.FRACBITS) + xcenter;

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
                    ScaleColumn((short)x1, (short)toppix, fracstep, spriteAsset, i);
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
    = Draws a sprite's indexed bitmap at [height] pixels high
    =
    ===================
    */
    internal static void SimpleScaleShape (int dispx, string shapenum, int dispheight)
    {
        int height, toppix;
        int x1, x2, xcenter;
        int frac, fracstep;

        height = dispheight >> 1;

        var spriteAsset = _assetManager.Find<SpriteAsset>(shapenum);
        if (spriteAsset == null)
            return;

        fracstep = MathUtils.FixedDiv(height, TEXTURESIZE / 2);
        frac = 0;

        xcenter = dispx - height;
        toppix = centery - height;

        x2 = xcenter;

        for (int i = 0; i < spriteAsset.Width; i++)
        {
            //
            // calculate edges of the shape
            //
            x1 = x2;

            frac += fracstep;
            x2 = (frac >> MathUtils.FRACBITS) + xcenter;

            while (x1 < x2)
            {
                ScaleColumn((short)x1, (short)toppix, fracstep, spriteAsset, i);

                x1++;
            }
        }
    }
}
