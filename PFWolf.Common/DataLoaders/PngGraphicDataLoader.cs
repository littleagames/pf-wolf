using PFWolf.Common.Assets;
using System;

namespace PFWolf.Common.DataLoaders;

public class PngGraphicDataLoader
{
    public static Graphic Load(Stream stream)
    {
        using MemoryStream ms = new MemoryStream();
        stream.CopyTo(ms);
        var rawData = ms.ToArray();

        var pngPalette = new PaletteColor[256];
        var offsetTopLeft = new Vector2(0, 0);

        bool isAlphaTexture = false;
        bool isMasked = false;

        if (rawData.Length < 8)
        {
            throw new ArgumentException("Invalid PNG data: too short.");
        }

        var sigCheck = new byte[] { 137, (byte)'P', (byte)'N', (byte)'G' };
        int sigIndex = FindChunk(rawData, sigCheck);

        var sig2Check = new byte[] {13,10,26,10};
        int sig2Index = FindChunk(rawData, sig2Check);

        if (sigIndex != 0 || sig2Index != 4)
        {
            throw new ArgumentException("Invalid PNG data: incorrect signature.");
        }

        var pngHeader = rawData.Skip(8).Take(8).ToArray();

        if (pngHeader.Length < 8)
        {
            throw new ArgumentException("Invalid PNG data: missing IDHR.");
        }

        var ihdrCheck = new byte[] {(byte)'I', (byte)'H', (byte)'D', (byte)'R' };
        int hdrIndex = FindChunk(rawData, ihdrCheck);

        if (hdrIndex == -1)
        {
            throw new ArgumentException("Invalid PNG data: incorrect IDHR.");
        }

        var headerSizeRaw = rawData.Skip(hdrIndex - 4).Take(4).ToArray();
        var headerChunkSize = BitConverter.ToInt32(headerSizeRaw.Reverse().ToArray());
        // I assume its the 13 bytes after
        //
        var headerChunk = rawData.Skip(hdrIndex + 4).Take(headerChunkSize).ToArray();

        var width = BitConverter.ToInt32(headerChunk.Take(4).Reverse().ToArray());
        var height = BitConverter.ToInt32(headerChunk.Skip(4).Take(4).Reverse().ToArray());
        var bitDepth = headerChunk[8];
        var colorType = headerChunk[9];
        var compressionMethod = headerChunk[10];
        var filterMethod = headerChunk[11];
        var interlaceMethod = headerChunk[12];

        if (compressionMethod != 0 || filterMethod != 0 || interlaceMethod > 1)
        {
            throw new ArgumentException("Not a valid PNG");
        }
        if ((1 << colorType & 0x5D) == 0)
        {
            throw new ArgumentException("Not a valid PNG");
        }
        if ((1 << bitDepth & 0x116) == 0)
        {
            throw new ArgumentException("Not a valid PNG");
        }

        var iDatCheck = new byte[] { (byte)'I', (byte)'D', (byte)'A', (byte)'T' };
        var iEndCheck = new byte[] { (byte)'I', (byte)'E', (byte)'N', (byte)'D' };

        int idatIndex = FindChunk(rawData, iDatCheck);
        int iendIndex = FindChunk(rawData, iEndCheck);

        if (idatIndex == -1 || iendIndex == -1 || iendIndex <= idatIndex)
        {
            throw new ArgumentException("Invalid PNG data: missing or misordered IDAT/IEND chunks.");
        }

        var dataLength = BitConverter.ToInt32(rawData.Skip(33).Take(4).Reverse().ToArray());
        var dataChunkSize = iendIndex - idatIndex;
        var data = rawData.Skip(idatIndex).Take(dataChunkSize).ToArray();

        var grabCheck = new byte[] { (byte)'g', (byte)'r', (byte)'A', (byte)'B' };
        int grabIndex = FindChunk(rawData, grabCheck);
        if (grabIndex != -1)
        {
            var grabData = rawData.Skip(grabIndex + 4).Take(8).ToArray();
            if (grabData.Length == 8)
            {
                var x = BitConverter.ToInt32(grabData.Take(4).Reverse().ToArray());
                var y = BitConverter.ToInt32(grabData.Skip(4).Take(4).Reverse().ToArray());
                if (x < -32768 || x > 32767)
                {
                    // log.Warning("X-Offset for PNG texture {assetName} is bad: {x}", assetName, x);
                    x = 0;
                }

                if (y < -32768 || y > 32767)
                {
                    // log.Warning("Y-Offset for PNG texture {assetName} is bad: {y}", assetName, y);
                    y = 0;
                }

                offsetTopLeft = new Vector2(x, y);
            }
        }

        var plteCheck = new byte[] { (byte)'P', (byte)'L', (byte)'T', (byte)'E' };
        int plteIndex = FindChunk(rawData, plteCheck);
        if (plteIndex != -1)
        {
            var plteLength = BitConverter.ToInt32(rawData.Skip(plteIndex - 4).Take(4).Reverse().ToArray());
            var paletteSize = Math.Min(plteLength / 3, 256);
            var paletteData = rawData.Skip(plteIndex).Take(paletteSize*3).ToArray();

            for (var i = paletteSize - 1; i >= 0; i--)
            {
                // get rgb
                var r = paletteData[i * 3];
                var g = paletteData[i * 3 + 1];
                var b = paletteData[i * 3 + 2];
                pngPalette[i] = new PaletteColor(r, g, b, 255);
            }
        }

        var trnsCheck = new byte[] { (byte)'t', (byte)'R', (byte)'N', (byte)'S' };
        int transIndex = FindChunk(rawData, trnsCheck);

        var alphaCheck = new byte[] { (byte)'a', (byte)'l', (byte)'P', (byte)'h' };
        int alphaIndex = FindChunk(rawData, alphaCheck);
        if (alphaIndex != 0)
        {
            isAlphaTexture = true;
            isMasked = true;
        }

        // TODO: Get tEXt chunk
        var textCheck = new byte[] { (byte)'t', (byte)'E', (byte)'X', (byte)'t' };
        int textIndex = FindChunk(rawData, textCheck);

        // get text chunk, if exists
        if (textIndex != -1)
        {
            // parse text chunk
        }

        

        // PNG to graphic data
        return new Graphic
        {
            // Offset
            Dimensions = new Dimension
            {
                Width = width,
                Height = height
            },
            Offset = offsetTopLeft,
            //Data = indexedData,
            Data = Enumerable.Repeat((byte)0x20, width*height).ToArray()// new byte[width*height] // TODO: Translate to index-based palette
        };
    }
    // Search for IDAT and IEND chunks in the byte array, ensuring IEND is after IDAT
    private static int FindChunk(byte[] data, byte[] chunk)
    {
        for (int i = 0; i <= data.Length - chunk.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < chunk.Length; j++)
            {
                if (data[i + j] != chunk[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return i;
        }
        return -1;
    }

    public static void MapPalette()
    {
        // TODO: Implement palette mapping logic
    }
}
