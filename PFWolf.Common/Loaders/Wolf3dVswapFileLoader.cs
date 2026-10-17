using PFWolf.Assets;
using PFWolf.Assets.Sounds;

namespace PFWolf.Loaders;

public class Wolf3dVswapFileLoader
{
    private struct DigiInfo
    {
        public uint StartPage;
        public uint Length;
    }

    private UInt16 ChunksInFile;
    private UInt16 PMSpriteStart;
    private UInt16 PMSoundStart;

    private bool PMSoundInfoPagePadded = false;

    private UInt16[] pageLengths;
    private byte[] PMPageData;
    private byte[][] PMPages;

    public Wolf3dVswapFileLoader(string fileName)
    {
        string fname = fileName;

        if (!File.Exists(fname))
            throw new FileNotFoundException("File not found", fname);

        using (FileStream fs = File.OpenRead(fname))
        using (BinaryReader br = new BinaryReader(fs))
        {
            // Read header words (16-bit little-endian)
            ChunksInFile = br.ReadUInt16();
            PMSpriteStart = br.ReadUInt16();
            PMSoundStart = br.ReadUInt16();

            // Read chunk offsets (32-bit each)
            uint[] pageOffsets = new uint[ChunksInFile + 1];
            for (int i = 0; i < ChunksInFile; i++)
            {
                pageOffsets[i] = br.ReadUInt32();
            }

            // Read chunk lengths (16-bit each)
            pageLengths = new ushort[ChunksInFile];
            for (int i = 0; i < ChunksInFile; i++)
            {
                pageLengths[i] = br.ReadUInt16();
            }

            long filesize = fs.Length;
            long datasize = filesize - (long)pageOffsets[0];

            if (datasize < 0)
                throw new PfWolfFileLoaderException("Vswap page data is corrupted.");

            // terminator offset
            pageOffsets[ChunksInFile] = (uint)filesize;

            // validate offsets
            for (int i = 0; i < ChunksInFile; i++)
            {
                if (pageOffsets[i] == 0)
                    continue; // sparse page

                if (pageOffsets[i] < pageOffsets[0] || pageOffsets[i] >= (uint)filesize)
                    throw new PfWolfFileLoaderException("Vswap page offset is illegal for page {0}: {1} (filesize: {2})", i, pageOffsets[i], filesize);
            }

            // calculate padding for alignment between sprite and sound pages
            int padding = 0;
            for (int i = PMSpriteStart; i < PMSoundStart; i++)
            {
                if (pageOffsets[i] == 0)
                    continue; // sparse page

                long relative = (long)pageOffsets[i] - pageOffsets[0];
                if (((relative + padding) & 1L) != 0L)
                    padding++;
            }

            long lastRelative = (long)pageOffsets[ChunksInFile - 1] - pageOffsets[0];
            if (((lastRelative + padding) & 1L) != 0L)
                padding++;

            // allocate contiguous page buffer
            PMPageData = new byte[datasize + padding];

            // allocate page pointers (copies per-page)
            PMPages = new byte[ChunksInFile + 1][];

            //
            // load pages and initialize PMPages pointers
            //
            long pagePos = 0;

            for (int i = 0; i < ChunksInFile; i++)
            {
                if ((i >= PMSpriteStart && i < PMSoundStart) || i == ChunksInFile - 1)
                {
                    //
                    // pad with zeros to make it 2-byte aligned
                    //
                    if ((pagePos & 1L) != 0L)
                    {
                        PMPageData[pagePos++] = 0;
                        if (i == ChunksInFile - 1)
                            PMSoundInfoPagePadded = true;
                    }
                }

                PMPages[i] = Array.Empty<byte>();

                if (pageOffsets[i] == 0)
                {
                    continue; // sparse page
                }

                //
                // use specified page length when next page is sparse
                // otherwise, calculate size from the offset difference between this and the next page
                //
                uint pagesize;
                if (pageOffsets[i + 1] == 0)
                {
                    pagesize = pageLengths[i];
                }
                else
                {
                    pagesize = pageOffsets[i + 1] - pageOffsets[i];
                }

                // read the page data
                fs.Position = pageOffsets[i];
                int toRead = (int)pagesize;
                byte[] buffer = br.ReadBytes(toRead);
                if (buffer.Length != toRead)
                    throw new PfWolfFileLoaderException("Failed to read full page {0} (expected {1}, got {2})", i, toRead, buffer.Length);

                // copy into contiguous buffer and per-page array
                Buffer.BlockCopy(buffer, 0, PMPageData, (int)pagePos, toRead);
                PMPages[i] = new byte[toRead];
                Buffer.BlockCopy(buffer, 0, PMPages[i], 0, toRead);

                pagePos += toRead;
            }

            // last page points after page buffer - represent as empty array
            PMPages[ChunksInFile] = Array.Empty<byte>();
        }
    }

    /// <param name="warn">Told when the file has a different number of walls, sprites or sounds than the names</param>
    public Dictionary<string, Asset> GetAssets(List<string> textures, List<string> sprites, List<string> digitizedAudio,
        Action<string>? warn = null)
    {
        var assets = new Dictionary<string, Asset>();
        // 1) Get wall texture mapping
        // 2) Get sprite mapping
        // 3) Get digi sound mapping

        var wallNames = new DataMapNames(textures, "walls", "WALL");
        var spriteNames = new DataMapNames(sprites, "sprites", "SPR");
        var digiNames = new DataMapNames(digitizedAudio, "digitized sounds", "DIGI");

        // A sparse page (empty) is one the release leaves out, as shareware does the registered
        // episodes' walls and sprites: it gets no asset
        for (int i = 0; i < PMSpriteStart; i++)
        {
            byte[] textureData = GetPage(i);
            if (textureData.Length == 0)
                continue;
            assets[wallNames.NameAt(i).ToLowerInvariant()] = new TextureAsset { RawData = textureData };
            // Use SDL_SetupSprite to get sprite data
        }

        for (int i = PMSpriteStart; i < PMSoundStart; i++)
        {
            byte[] spriteData = GetPage(i);
            if (spriteData.Length == 0)
                continue;
            assets[spriteNames.NameAt(i - PMSpriteStart).ToLowerInvariant()] = Wolf3dCompiledSpriteConverter.Convert(spriteData);
            // Use SDL_SetupSprite to get sprite data
        }
        // Use SDL_SetupDigi to get sound data

        byte[] soundInfoData = GetPage(ChunksInFile - 1);
        ushort[] soundInfoPage = new ushort[soundInfoData.Length / 2];
        Buffer.BlockCopy(soundInfoData, 0, soundInfoPage, 0, soundInfoData.Length);

        var numDigi = (ushort)(GetPageSize(ChunksInFile - 1) / 4);

        var digiList = new DigiInfo[numDigi];

        for (int i = 0; i < numDigi; i++)
        {
            // Calculate the size of the digi from the sizes of the pages between
            // the start page and the start page of the next sound

            digiList[i].StartPage = soundInfoPage[i * 2];
            if ((int)digiList[i].StartPage >= ChunksInFile - 1)
            {
                numDigi = (ushort)i;
                break;
            }

            int lastPage;
            if (i < numDigi - 1)
            {
                lastPage = soundInfoPage[i * 2 + 2];
                if (lastPage == 0 || lastPage + PMSoundStart > ChunksInFile - 1) lastPage = ChunksInFile - 1;
                else lastPage += PMSoundStart;
            }
            else lastPage = ChunksInFile - 1;

            int size = 0;
            for (int page = (int)(PMSoundStart + digiList[i].StartPage); page < lastPage; page++)
                size += (int)GetPageSize(page);

            // Don't include padding of sound info page, if padding was added
            if (lastPage == ChunksInFile - 1 && PMSoundInfoPagePadded) size--;

            // Patch lower 16-bit of size with size from sound info page.
            // The original VSWAP contains padding which is included in the page size,
            // but not included in the 16-bit size. So we use the more precise value.
            if ((size & 0xffff0000) != 0 && (size & 0xffff) < soundInfoPage[i * 2 + 1])
                size -= 0x10000;
            size = (int)((size & 0xffff0000) | soundInfoPage[i * 2 + 1]);

            digiList[i].Length = (uint)size;
        }

        for (int i = 0; i < numDigi; i++)
        {
            // The sound info page's size can take in padding past the last entry (shareware's is
            // a whole page of them): past the map's names, an entry that doesn't start after the
            // one before it is that padding, not a sound
            if (i >= digitizedAudio.Count && i > 0 && digiList[i].StartPage <= digiList[i - 1].StartPage)
                break;

            // Left out of the release (shareware's): its pages are sparse
            if (GetPage((int)(PMSoundStart + digiList[i].StartPage)).Length == 0)
                continue;

            byte[] soundData;
            try
            {
                soundData = GetSoundPage(digiList[i].StartPage, digiList[i].Length);
            }
            catch (PfWolfFileLoaderException) when (i >= digitizedAudio.Count)
            {
                break; // padding whose made-up length runs past the sound pages
            }
            assets[digiNames.NameAt(i).ToLowerInvariant()] = new Wolf3dDigitizedAudio { RawData = soundData };
        }

        wallNames.Warn(warn);
        spriteNames.Warn(warn);
        digiNames.Warn(warn);
        return assets;
    }

    private byte[] GetPage(int page)
    {
        if (page < 0 || page >= ChunksInFile)
            throw new PfWolfFileLoaderException($"GetPage: Invalid page request: {page}");

        return PMPages[page];
    }

    private byte[] GetSoundPage(uint v, uint size)
    {
        List<byte> data = new List<byte>();
        var v1 = 0;
        while (data.Count < size)
        {
            data.AddRange(GetPage((int)(PMSoundStart + v + v1)));
            v1++;
        }

        return data.Take((int)size).ToArray();
    }
    private uint GetPageSize(int page)
    {
        if (page < 0 || page >= ChunksInFile)
            throw new PfWolfFileLoaderException($"GetPageSize: Invalid page request: {page}", page);

        return (uint)(PMPages[page].Length); // (uint32_t)(PMPages[page + 1] - PMPages[page]); // pointer addresses
    }

    private int GetPageEnd()
    {
        return PMPages.Sum(arr => arr.Length);
    }
}
