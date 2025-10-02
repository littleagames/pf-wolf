using PFWolf.Common.Assets;

namespace PFWolf.Common.Loaders;

public class Wolf3DVgaFileLoader : BaseFileLoader
{
    private readonly byte[] _dictionaryData;
    private readonly byte[] _headerData;
    private readonly string _vgaGraphFilePath;
    private const string AssetMarker = "!ID!";

    //private readonly byte[] _graphicData;

    public Wolf3DVgaFileLoader(string directory, string vgaDict, string vgaHead, string vgaGraph)
        : base(directory)
    {
        var vgaDictFilePath = Path.Combine(Directory, vgaDict);
        _dictionaryData = File.ReadAllBytes(vgaDictFilePath);
        var vgaHeadFilePath = Path.Combine(Directory, vgaHead);
        _headerData = File.ReadAllBytes(vgaHeadFilePath);
        _vgaGraphFilePath = Path.Combine(Directory, vgaGraph);
        //_graphicData = File.ReadAllBytes(vgaGraphFilePath);
    }
    
    public List<int> GetHeaderList()
    {
        var graphicStartIndexes = new List<int>((_headerData.Length / 3));

        for (var i = 0; i < (_headerData.Length / 3); i++)
        {
            // TODO: ReadLittleInt24(_headerData.Skip(i*sizeof(Int24).Take(sizeof(Int24).ToArray());
            // TODO: position += sizeof(Int24);
            var d0 = _headerData[i * 3];
            var d1 = _headerData[i * 3 + 1];
            var d2 = _headerData[i * 3 + 2];
            int val = d0 | d1 << 8 | d2 << 16;
            var grStart = val == 0x00FFFFFF ? -1 : val;
            graphicStartIndexes.Add(grStart);
        }

        return graphicStartIndexes;
    }
    
    public override List<KeyValuePair<string, Asset>> Load(GamePackAssetReference assetNameReferences)
    {
        var headerList = GetHeaderList();

        var vgaAssets = new List<KeyValuePair<string, Asset>>();
        var numChunks = headerList.Count - 1;
        
        // assumed vga structure
        // 0 structpic (pic table data)
        // FONTS
        // PICS
        // EXTERNS
            // TILE8s
            // ENDSCREENs
            // ENDARTs
            // PALETTEs
            // DEMOs
            // More ENDARTs
            
        var segmentAssetType = new[] { typeof(PicData), typeof(Font), typeof(Graphic), typeof(VgaExtern) };
        uint[] segmentStarts = [0,0,0,0];
        uint currentSegment = 0;

        var lumps = new List<VgaLump>();
        var vgaFileStream = File.OpenRead(_vgaGraphFilePath);

        for (int index = 0; index < numChunks; index++)
        {
            var position = headerList[index];
            var compressedSize = headerList[index + 1] - position;
            var assetType = segmentAssetType[currentSegment];
            //Try to find!ID! tags
            if (currentSegment < 3 && compressedSize >= 4)
            {
                var tagBytes = new byte[4];
                vgaFileStream.Seek(position + compressedSize - sizeof(int), System.IO.SeekOrigin.Begin);
                var bytesRead = vgaFileStream.Read(tagBytes);
                if (bytesRead < 4)
                    continue;
                var tag = System.Text.Encoding.UTF8.GetString(tagBytes);
                if (tag.Equals(AssetMarker))
                {
                    segmentStarts[++currentSegment] = (uint)index + 1;
                    // Lump end contains a !ID! tag, remove it from the lump
                    compressedSize -= 4;

                    // TODO: Add "marker" to assets
                }
            }

            var assetName = GetReferenceName(assetNameReferences.Graphics, index) ?? $"VGA{index:D5}";
            switch (assetType.Name)
            {
                case nameof(PicData):
                    vgaAssets.Add(new KeyValuePair<string, Asset>(assetName, new AssetReference<PicData>(() => LoadVgaAsset<PicData>(assetName))));
                    break;
                case nameof(Font):
                    vgaAssets.Add(new KeyValuePair<string, Asset>(assetName, new AssetReference<Font>(() => LoadVgaAsset<Font>(assetName))));
                    break;
                case nameof(Graphic):
                    vgaAssets.Add(new KeyValuePair<string, Asset>(assetName, new AssetReference<Graphic>(() => LoadVgaAsset<Graphic>(assetName))));
                    break;
                case nameof(VgaExtern):
                    // TODO: Determine the extern type (palette, endscreen, demo, text, tile8)
                    vgaAssets.Add(new KeyValuePair<string, Asset>(assetName, new AssetReference<VgaExtern>(() => LoadVgaAsset<VgaExtern>(assetName))));
                    break;
            }

        }

        //var numFonts = segmentStarts[2] - segmentStarts[1];
        //var numPics = segmentStarts[3] - segmentStarts[2];

        //var huffman = new HuffmanCompression(_dictionaryData);
        //const int StructPicIndex = 0;
        //for (var i = 0; i < lumps.Count; i++)
        //{
        //    if (i == StructPicIndex)
        //    {
        //        var structData = lumps[i].CompressedData;
        //        // First 4 bytes is the struct data expanded length
        //        var expandedStructLength = BitConverter.ToInt32(structData.Take(sizeof(int)).ToArray());
        //        var expandedStructData = huffman.Expand(structData.Skip(sizeof(int)).Take(expandedStructLength).ToArray());
        //        expandedStructData = expandedStructData.Take(expandedStructLength).ToArray();
        //        var expectedPictableDataLength = numPics * 2 * sizeof(ushort);
        //        if (expandedStructLength != expectedPictableDataLength)
        //        {
        //            throw new Exception(
        //                $"Picture dimension data size mismatch, is {expandedStructLength}, expected {expectedPictableDataLength}");
        //        }
        
        //        var picTableData = Converters.ByteArrayToUInt16Array(expandedStructData);
        //        vgaAssets.Add(new StructPicAsset
        //        {
        //            NumFonts = (int)numFonts,
        //            NumPics = (int)numPics,
        //            RawData = expandedStructData,
        //            Dimensions = ToDimensions(picTableData, (int)numPics)
        //        });
        //    }
            
        //    // Fonts
        //    if (i >= segmentStarts[1] && i < segmentStarts[2])
        //    {
        //        var size = BitConverter.ToInt32(lumps[i].CompressedData.Take(sizeof(int)).ToArray());
        //        var compressedData = lumps[i].CompressedData.Skip(sizeof(int)).ToArray();
        //        var expandedData = huffman.Expand(compressedData);
        //        expandedData = expandedData.Take(size).ToArray();
        //        vgaAssets.Add(new FontAsset(
        //            name: GetReferenceName(i) ?? $"FONT{i:D5}",
        //            rawData: expandedData));
        //    }
            
        //    // Graphics
        //    if (i >= segmentStarts[2] && i < segmentStarts[3])
        //    {
        //        var structData = ((StructPicAsset)vgaAssets[StructPicIndex]);
        //        var picNum = i - (int)(segmentStarts[2]);
        //        if (picNum < 0 || picNum >= structData.NumPics)
        //        {
        //            throw new IndexOutOfRangeException($"Pic number {picNum} is out of range.");
        //        }
                
        //        var dimensions = structData.Dimensions[picNum];
                
        //        var size = BitConverter.ToInt32(lumps[i].CompressedData.Take(sizeof(int)).ToArray());
        //        var compressedData = lumps[i].CompressedData.Skip(sizeof(int)).ToArray();
        //        var expandedData = huffman.Expand(compressedData); // TODO: 63999 for a 64000 image?
        //        if (expandedData.Length < size)
        //        {
        //            throw new Exception(
        //                $"Huffman expand didn't fill the entire array: {expandedData.Length} (expanded) < {size} (size)");
        //        }
        //        expandedData = expandedData.Take(size).ToArray();
        //        expandedData = DeplaneData(expandedData, dimensions);
                
        //        vgaAssets.Add(new GraphicAsset
        //        {
        //            Name = GetReferenceName(i) ?? $"PIC{i:D5}",
        //            RawData = expandedData, // TODO: DeplaneVGA? (4 byte blocks?)
        //            Dimensions = dimensions
        //        });
        //    }
            
        //    // Externs
        //    if (i >= segmentStarts[3])
        //    {
        //        var paletteSize = 256 * 3 * sizeof(byte);
                
        //        // Check if ENDSCREEN (4000 bytes?)
        //        // 80 x 25
        //        // ASCII, foreground, and background color (2 + 1 + 1 byte)
        //        var endscreenSize = 4008;
        //        var size = BitConverter.ToInt32(lumps[i].CompressedData.Take(sizeof(int)).ToArray());
        //        var compressedData = lumps[i].CompressedData.Skip(sizeof(int)).ToArray();
        //        var expandedData = huffman.Expand(compressedData);

        //        if (size == paletteSize)
        //        {
        //            vgaAssets.Add(new PaletteAsset
        //            {
        //                Name = GetReferenceName(i) ??  $"PALETTE{i:D5}",
        //                RawData = expandedData
        //            });
        //            continue;
        //        }

        //        if (size == endscreenSize)
        //        {
        //            vgaAssets.Add(new EndScreenAsset
        //            {
        //                Name = GetReferenceName(i) ?? $"SCREEN{i:D5}",
        //                RawData = expandedData
        //            });
        //            continue;
        //        }

        //        var gameMapNumber = expandedData[0]; // Cannot validate this number on number of maps, since that information may not be known
        //        var demoLength = BitConverter.ToInt16(expandedData.Skip(sizeof(byte)).Take(sizeof(short)).ToArray());
        //        if (demoLength == expandedData.Length || demoLength == size)
        //        {
        //            // demo length in file 2077, expanded data is 2076
        //            // should the expanded data be 2077 + 2 + 1 (demo + demolength + map)??
        //            if (expandedData.Length < demoLength)
        //                Array.Resize(ref expandedData, demoLength);
        //            vgaAssets.Add(new DemoAsset
        //            {
        //                MapNumber = gameMapNumber,
        //                Name = GetReferenceName(i) ?? $"DEMO{i:D5}",
        //                RawData = expandedData
        //            });
        //            continue;
        //        }
                
        //        var text = System.Text.Encoding.ASCII.GetString(expandedData);
        //        if (text.StartsWith("^P", StringComparison.CurrentCultureIgnoreCase)
        //            || text.EndsWith("^E", StringComparison.CurrentCultureIgnoreCase))
        //            // TODO: Check to see if all values are between 32 and 127 (used for ASCII)
        //        // TODO: Spear text is missing the page tags (its unused but still there
        //        {
        //            vgaAssets.Add(new TextAsset
        //            {
        //                Name = GetReferenceName(i) ?? $"TEXT{i:D5}",
        //                RawData = expandedData,
        //                Text = text
        //            });
        //            continue;
        //        }
                
        //        // TODO: Tile8s
        //    }
        //}
        
        return vgaAssets;
    }

    public T LoadVgaAsset<T>(string name) where T : Asset
    {
        throw new NotImplementedException();
    }

    private string? GetReferenceName(List<string> assetNameReferences, int i)
    {
        if (i < 0 || i > assetNameReferences.Count)
            return null;
        
        return assetNameReferences[i];
    }
    
    private List<Vector2> ToDimensions(ushort[] picTableData, int numPics)
    {
        var dimensions = new List<Vector2>(numPics);
        for (var i = 0; i < numPics; i++)
        {
            var width = picTableData[i*2];
            var height = picTableData[i*2+1];
            
            // validation
            if (width <= 0 || width > 640 || height <= 0 || height > 400)
            {
                throw new Exception($"Invalid file size: {width}x{height} for pic {i}");
            }
            
            var dimension = new Vector2(width, height);
            dimensions.Add(dimension);
        }
        
        return dimensions;
    }

    private byte[] DeplaneData(byte[] source, Vector2 dimensions)
    {
        int width = dimensions.X;
        int height = dimensions.Y;
        
        int x, y, plane;
        ushort size, pwidth;
        byte[] temp, dest, srcline;

        size = (ushort)(width * height);

        if ((width & 3) != 0)
            throw new Exception("DeplaneData: width not divisible by 4!");

        temp = new byte[size];// SafeMalloc(size);

        //
        // munge pic into the temp buffer
        //
        srcline = source;
        var srcLineIndex = 0;
        pwidth = (ushort)(width >> 2); // width/4

        for (plane = 0; plane < 4; plane++)
        {
            dest = temp;

            for (y = 0; y < height; y++)
            {
                for (x = 0; x < pwidth; x++)
                {
                    if (srcLineIndex >= srcline.Length)
                        continue;
                    dest[(width * y) + ((x << 2) + plane)] = srcline[srcLineIndex++];
                }
            }
        }

        //
        // copy the temp buffer back into the original source
        //
        //Array.Copy(temp, source, size);

        return temp;
    }

    private record VgaLump : Asset
    {
        public string Name { get; init; } = null!;
        //public int Index { get; init; }
        //public byte[] CompressedData { get; init; } = [];
    }
}