namespace Wolf3D;

internal partial class Program
{
    static UInt16 ChunksInFile;
    static UInt16 PMSpriteStart;
    static UInt16 PMSoundStart;

    static bool PMSoundInfoPagePadded = false;

    static UInt16[] pageLengths;
    static byte[] PMPageData;
    static byte[][] PMPages;

    internal static void PM_Startup()
    {
        string fname = $"vswap.{extension}";

        if (!File.Exists(fname))
            CA_CannotOpen(fname);

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
                Quit($"PM_Startup: The page file \"{fname}\" is too large!");

            // terminator offset
            pageOffsets[ChunksInFile] = (uint)filesize;

            // validate offsets
            for (int i = 0; i < ChunksInFile; i++)
            {
                if (pageOffsets[i] == 0)
                    continue; // sparse page

                if (pageOffsets[i] < pageOffsets[0] || pageOffsets[i] >= (uint)filesize)
                    Quit($"PM_Startup: Illegal page offset for page {i}: {pageOffsets[i]} (filesize: {filesize})");
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
                    Quit($"PM_Startup: Failed to read full page {i} (expected {toRead}, got {buffer.Length})");

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

    internal static void PM_Shutdown()
    {
        // Do nothing, GC does it
    }

    internal static byte[] PM_GetSpritePage(spritenums v) => PM_GetPage(PMSpriteStart + (int)v);
    internal static byte[] PM_GetSoundPage(int v, int size)
    {
        List<byte> data = new List<byte>();
        var v1 = 0;
        while (data.Count < size)
        {
            data.AddRange(PM_GetPage(PMSoundStart + v+v1));
            v1++;
        }

        return data.ToArray();
    }

    internal static byte[] PM_GetPage(int page)
    {
        if (page < 0 || page >= ChunksInFile)
            Quit($"PM_GetPage: Invalid page request: {page}");

        return PMPages[page];
    }

    internal static uint PM_GetPageSize(int page)
    {
        if (page < 0 || page >= ChunksInFile)
            Quit($"PM_GetPageSize: Invalid page request: {page}");

        return (uint)(PMPages[page].Length); // (uint32_t)(PMPages[page + 1] - PMPages[page]); // pointer addresses
    }

    internal static int PM_GetPageEnd()
    {
        return PMPages.Sum(arr => arr.Length);
    }

    static void CA_CannotOpen(string text)
    {
        Quit($"Can't open {text}!");
    }
}
