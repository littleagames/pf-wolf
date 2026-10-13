using PFWolf.Assets;

namespace PFWolf;

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
    = Each color is remapped through shade from shadeofs (Program.WL_SHADE.cs).
    =
    ===================
    */

    // translation, if any, recolors each index first (Managers/TranslationManager.cs)
    internal static void ScaleColumn(short x, short toppix, int fracstep, SpriteAsset sprite, int column, byte[] shade, int shadeofs,
        bool cloaked = false, byte[]? translation = null)
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

            byte index = indices[srcIndex];
            byte color = shade[shadeofs + (translation == null ? index : translation[index])];

            var destIndex = _videoManager.ylookup[startpix] + x;
            unsafe
            {
                byte* dest = (byte*)vbufPtr + screenofs;
                while (startpix < endpix)
                {
                    // cloaked: what's behind, darkened (shade is then the light table, shadeofs its level)
                    dest[destIndex] = cloaked ? shade[shadeofs + dest[destIndex]] : color;
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

        // Its origin pixel on the actor's spot on the floor (a Wolf3D sprite: the middle of its
        // bottom row, so a story tall)
        var (originx, originy) = spriteAsset.Origin;
        xcenter = sprite.viewx - TexelsToPixels(originx, height);
        toppix = FloorRow(height) - TexelsToPixels(originy, height);

        byte[] shade = sprite.bright ? noshade : LightAt(TileIndex(sprite.tilex, sprite.tiley), sprite.worldx, sprite.worldy);
        int shadeofs = sprite.bright ? 0 : ShadeOffsetForHeight(sprite.viewheight);

        // A cloaked sprite (Planet Strike's) darkens what's behind it, through shade level 16 as
        // bstone's shading[0x1000 | pixel] does
        if (sprite.cloaked)
        {
            shade = LightAt(TileIndex(sprite.tilex, sprite.tiley), sprite.worldx, sprite.worldy);
            shadeofs = Math.Min(16 * 256, shade.Length - 256);
        }

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
                    ScaleColumn((short)x1, (short)toppix, fracstep, spriteAsset, i, shade, shadeofs, sprite.cloaked, sprite.translation);
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
    = Draws a sprite's indexed bitmap at [height] pixels high, through shade
    = (unshaded when left out), [dropy] pixels below the view's centre
    =
    ===================
    */
    internal static void SimpleScaleShape (int dispx, string shapenum, int dispheight, byte[]? shade = null, int dropy = 0)
    {
        shade ??= noshade;
        int height, toppix;
        int x1, x2, xcenter;
        int frac, fracstep;

        height = dispheight >> 1;

        var spriteAsset = _assetManager.Find<SpriteAsset>(shapenum);
        if (spriteAsset == null)
            return;

        fracstep = MathUtils.FixedDiv(height, TEXTURESIZE / 2);
        frac = 0;

        // Its origin pixel on the view's bottom edge, in the middle (a Wolf3D sprite: the middle of
        // its bottom row); the weapon in hand doesn't move with the view's pitch
        var (originx, originy) = spriteAsset.Origin;
        xcenter = dispx - TexelsToPixels(originx, height);
        toppix = basecentery + height + dropy - TexelsToPixels(originy, height);

        x2 = xcenter;

        for (int i = 0; i < spriteAsset.Width; i++)
        {
            //
            // calculate edges of the shape
            //
            x1 = x2;

            if (x1 >= viewwidth)
                break;                // off the right side (an offset can put it there)

            frac += fracstep;
            x2 = (frac >> MathUtils.FRACBITS) + xcenter;

            for (x1 = Math.Max(x1, 0); x1 < Math.Min(x2, viewwidth); x1++)
                ScaleColumn((short)x1, (short)toppix, fracstep, spriteAsset, i, shade, 0);
        }
    }

    /// <summary>
    /// A distance in a sprite's texels as screen pixels, for a sprite drawn <paramref name="half"/>
    /// pixels to 32 texels (half a story)
    /// </summary>
    internal static int TexelsToPixels(int texels, int half) => (int)Math.Floor(texels * (double)half / (TEXTURESIZE / 2));
}
