using static SDL2.SDL;

namespace Wolf3D;

internal struct shadedef_t
{
    public byte destRed, destGreen, destBlue; // values between 0 and 255
    public byte fogStrength;

    public shadedef_t(byte destRed, byte destGreen, byte destBlue, byte fogStrength)
    {
        this.destRed = destRed;
        this.destGreen = destGreen;
        this.destBlue = destBlue;
        this.fogStrength = fogStrength;
    }
}

internal partial class Program
{
    internal const int SHADE_COUNT = 32;
    internal const byte LSHADE_NOSHADING = 0xff;
    internal const byte LSHADE_NORMAL = 0;
    internal const byte LSHADE_FOG = 5;

    internal shadedef_t[] shadeDefs =
    {
        new shadedef_t(0,0,0, LSHADE_NOSHADING),
        new shadedef_t(0,0,0, LSHADE_NORMAL),
        new shadedef_t(0,0,0, LSHADE_FOG),
        new shadedef_t(40,40,40, LSHADE_NORMAL),
        new shadedef_t(60,60,60, LSHADE_FOG),
    };

    internal static byte[][] shadetable = new byte[SHADE_COUNT][];
    internal static int LSHADE_flag;

    internal static int GetShadeDefID()
    {
        int shadeID;

        switch (gamestate.episode * 10 + gamestate.mapon)
        {
            case 0: shadeID = 4; break;
            case 1:
            case 2:
            case 6: shadeID = 1; break;
            case 3: shadeID = 0; break;
            case 5: shadeID = 2; break;
            default: shadeID = 3; break;
        }

        //assert(shadeID >= 0 && shadeID < lengthof(shadeDefs));

        return shadeID;
    }

    //
    // Returns the palette index of the nearest matching color of the
    // given RGB color in given palette
    //
    internal static byte GetColor(byte red, byte green, byte blue, SDL_Color[] palette)
    {
        int col;
        byte mincol = 0;
        double mindist = 200000.0f, curdist, DRed, DGreen, DBlue;

        int palPtr = 0;

        for (col = 0; col < 256; col++, palPtr++)
        {
            DRed = red - palette[palPtr].r;
            DGreen = green - palette[palPtr].g;
            DBlue = blue - palette[palPtr].b;
            curdist = DRed * DRed + DGreen * DGreen + DBlue * DBlue;

            if (curdist < mindist)
            {
                mindist = curdist;
                mincol = (byte)col;
            }
        }

        return mincol;
    }


    //
    // Fade all colors in SHADE_COUNT steps down to the destination-RGB
    // (use gray for fogging, black for standard shading)
    //
    internal static void GenerateShadeTable(byte destRed, byte destGreen, byte destBlue,
                             SDL_Color[] palette, int fog)
    {
        int i, shade;
        double curRed, curGreen, curBlue, redStep, greenStep, blueStep;
        int palPtr = 0;

        LSHADE_flag = fog;

        // TODO: shadetable might need to be initialized here

        for (i = 0; i < 256; i++, palPtr++)
        {
            //
            // get original palette color
            //
            curRed = palette[palPtr].r;
            curGreen = palette[palPtr].g;
            curBlue = palette[palPtr].b;

            //
            // calculate increment per step
            //
            redStep = (destRed - curRed) / (SHADE_COUNT + 8);
            greenStep = (destGreen - curGreen) / (SHADE_COUNT + 8);
            blueStep = (destBlue - curBlue) / (SHADE_COUNT + 8);

            //
            // calculate color for each shade of the current color
            //
            for (shade = 0; shade < SHADE_COUNT; shade++)
            {
                shadetable[shade][i] = GetColor((byte)curRed, (byte)curGreen, (byte)curBlue, palette);

                curRed += redStep;
                curGreen += greenStep;
                curBlue += blueStep;
            }
        }
    }


    internal static void NoShading()
    {
        int i, shade;

        for (shade = 0; shade < SHADE_COUNT; shade++)
        {
            for (i = 0; i < 256; i++)
                shadetable[shade][i] = (byte)i;
        }
    }

    void InitLevelShadeTable()
    {
        shadedef_t shadeDef = shadeDefs[GetShadeDefID()];

        if (shadeDef.fogStrength == LSHADE_NOSHADING)
            NoShading();
        else
            GenerateShadeTable(shadeDef.destRed, shadeDef.destGreen, shadeDef.destBlue, gamepal, shadeDef.fogStrength);
    }

    byte[] GetShade(int scale, uint flags)
    {
        int shade;

        if ((flags & (uint)objflags.FL_FULLBRIGHT) != 0)
            shade = SHADE_COUNT;
        else
        {
            shade = (scale >> 1) / (((viewwidth * 3) >> 8) + 1 + LSHADE_flag);  // TODO: reconsider this...

            if (shade > SHADE_COUNT)
                shade = SHADE_COUNT;
            else if (shade < 1)
                shade = 1;
        }

        return shadetable[SHADE_COUNT - shade];
    }
}
