using PFWolf.Common.Assets;
using PFWolf.Common.Extensions;
using System;

namespace PFWolf.Common.Loaders;

public class Wolf3DMapFileLoader : BaseFileLoader
{
    private readonly string _mapHeaderFilePath;

    private readonly byte[] _headerData;
    private readonly string _gameMapsFilePath;

    //private readonly byte[] _gameMapData;
    private const int DefaultNumberOfPlanes = 3;
    private const int DefaultMapNameSize = 16;

    public Wolf3DMapFileLoader(string directory, string mapHead, string gameMap) : base(directory)
    {
        _mapHeaderFilePath = Path.Combine(Directory, mapHead);
        _headerData = File.ReadAllBytes(_mapHeaderFilePath);

        _gameMapsFilePath = Path.Combine(Directory, gameMap);
        //_gameMapData = File.ReadAllBytes(gameMapsFilePath);
    }

    public MapHeader GetHeader()
    {
        var position = 0;
        
         var rlewTag = BitConverter.ToUInt16(_headerData, 0);
         position += sizeof(ushort);
         
         // Original map seems to have 100 maps placed
         var offsetLength = (_headerData.Length - position) / sizeof(int);
         var headerOffsets = _headerData.Skip(position).ToArray().ToInt32Array(offsetLength);

         return new MapHeader
         {
             RLEWTag = rlewTag,
             NumPlanes = DefaultNumberOfPlanes,
             HeaderOffsets = headerOffsets,
             NumAvailableMaps = headerOffsets.Length,
             NumMaps = headerOffsets.Count(x => x > 0) // Filter out sparse maps
         };
    }

    //public List<MapHeaderSegment> GetMapHeaderSegmentsList()
    //{
    //    var mapHeader = GetHeader();
    //    var segments = new List<MapHeaderSegment>();
    //    var position = 0;
        
    //    foreach (var offset in mapHeader.HeaderOffsets)
    //    {
    //        // Sparse map
    //        if (offset == 0) continue;
            
    //        position = offset;
    //        var planeStarts = _gameMapData.Skip(position).Take(sizeof(int) * DefaultNumberOfPlanes).ToArray().ToInt32Array(DefaultNumberOfPlanes);
    //        position += sizeof(int) * DefaultNumberOfPlanes;
            
    //        var planeLengths = _gameMapData.Skip(position).Take(sizeof(ushort) * DefaultNumberOfPlanes).ToArray().ToUInt16Array(DefaultNumberOfPlanes);
    //        position += sizeof(ushort) * DefaultNumberOfPlanes;

    //        var width = BitConverter.ToUInt16(_gameMapData.Skip(position).ToArray());
    //        position += sizeof(ushort);
            
    //        var height = BitConverter.ToUInt16(_gameMapData.Skip(position).ToArray());
    //        position += sizeof(ushort);

    //        var nameBytes = _gameMapData.Skip(position)
    //            .Take(sizeof(byte) * DefaultMapNameSize).ToArray();
    //        // Find the string terminator \0
    //        var nameEndOfString = Array.FindIndex(nameBytes, x => x == 0x00);
    //        var name = System.Text.Encoding.UTF8.GetString(nameBytes.Take(nameEndOfString).ToArray());
    //        segments.Add(new MapHeaderSegment
    //        {
    //            PlaneStarts = planeStarts,
    //            PlaneLengths = planeLengths,
    //            Height = height,
    //            Width = width,
    //            Name = name
    //        });
    //    }

    //    return segments;
    //}

    public override List<KeyValuePair<string, Asset>> Load(GamePackAssetReference assetNameReferences)
    {
        var assets = new List<KeyValuePair<string, Asset>>();

        var mapHeader = GetHeader();
        for (var i = 0; i < mapHeader.NumAvailableMaps; i++)
        {
            if (mapHeader.HeaderOffsets[i] == 0) // sparse map
                continue;

            var assetName = GetReferenceName(assetNameReferences.Maps, i) ?? $"MAP{i:D2}";
            var mapAsset = new AssetReference<Map>(() => LoadMapAsset(mapHeader, i, _gameMapsFilePath));
            assets.Add(new KeyValuePair<string, Asset>(assetName, mapAsset));
        }

        return assets;
    }

    private string? GetReferenceName(List<string> assetNameReferences, int i)
    {
        if (i < 0 || i > assetNameReferences.Count)
            return null;

        return assetNameReferences[i];
    }

    public Map LoadMapAsset(MapHeader header, int index, string gameMapFilePath)
    {
        var mapAsset = new Map
        {
            //Width = segment.Width,
            //Height = segment.Height//,
            //PlaneData = new byte[DefaultNumberOfPlanes][]
        };

        //for (var plane = 0; plane < DefaultNumberOfPlanes; plane++)
        //{
        //    var position = segment.PlaneStarts[plane];
        //    var compressedSize = segment.PlaneLengths[plane];
        //    if (compressedSize == 0)
        //        continue;

        //    var compressedData = _gameMapData.Skip(position).Take(compressedSize).ToArray();
        //    var carmackCompression = new CarmackCompression(/*nearTag, farTag*/);

        //    //
        //    // unhuffman, then unRLEW
        //    // The huffman'd chunk has a two byte expanded length first
        //    // The resulting RLEW chunk also does, even though it's not really
        //    // needed
        //    //
        //    var expanded = BitConverter.ToUInt16(compressedData.Take(sizeof(ushort)).ToArray());
        //    var expandedData = carmackCompression.Expand(compressedData.Skip(sizeof(ushort)).ToArray()).Take(expanded).ToArray();

        //    var rlewCompression = new RLEWCompression(/*rlewTag*/);
        //    var size = expandedData.First();
        //    var mapPlaneData = rlewCompression.Expand(Converters.UInt16ArrayToByteArray(expandedData.Skip(1).ToArray())).Take(size).ToArray();
        //    mapAsset.PlaneData[plane] = mapPlaneData;
        //}
        return mapAsset;
    }

    public record MapHeader
    {
        public ushort RLEWTag { get; init; }
        public ushort NumPlanes { get; init; } = 3;
        public int[] HeaderOffsets { get; init; } = [];

        public int NumAvailableMaps { get; set; }
        public int NumMaps { get; set; }
    }

    public record MapHeaderSegment
    {
        public int[] PlaneStarts { get; set; } = new int[DefaultNumberOfPlanes];
        public ushort[] PlaneLengths { get; set; } = new ushort[DefaultNumberOfPlanes];
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}