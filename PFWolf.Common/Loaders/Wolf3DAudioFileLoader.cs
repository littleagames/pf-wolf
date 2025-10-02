using PFWolf.Common.Assets;
using PFWolf.Common.Extensions;

namespace PFWolf.Common.Loaders;

public class Wolf3DAudioFileLoader : BaseFileLoader
{
    private readonly byte[] _headerData;
    private readonly string _audioDataFilePath;
    //private readonly byte[] _audioData;
    private const string AssetMarker = "!ID!";

    public Wolf3DAudioFileLoader(string directory, string audioHed, string audioT)
        : base(directory)
    {
        var audioHeaderFilePath = Path.Combine(Directory, audioHed);
        _headerData = File.ReadAllBytes(audioHeaderFilePath);

        _audioDataFilePath = Path.Combine(Directory, audioT);
        //_audioData = File.ReadAllBytes(audioDataFilePath);
    }

    public override List<KeyValuePair<string, Asset>> Load(GamePackAssetReference assetReferenceMap)
    {
        var headerList = GetAudioHeaderList();
        var audioAssets = new List<KeyValuePair<string, Asset>>(headerList.Count);

        for (var i = 0; i < headerList.Count; i++)
        {
            var header = headerList[i];
            var pos = header.DataFilePosition;
            var chunkLength = header.Size;

            // Find the music start marker
            if (chunkLength == 4)
            {
                // marker
                continue;
            }

            var audioAssetName = GetReferencedName(assetReferenceMap.Audio, i);
            switch (header.Namespace.Name)
            {
                case nameof(PcSound):
                    audioAssets.Add(new KeyValuePair<string, Asset>(audioAssetName, new AssetReference<PcSound>(() => LoadAudioAsset<PcSound>())));
                    break;
                case nameof(AdLibSound):
                    audioAssets.Add(new KeyValuePair<string, Asset>(audioAssetName, new AssetReference<AdLibSound>(() => LoadAudioAsset<AdLibSound>())));
                    break;
                case nameof(DigitizedSound):
                    audioAssets.Add(new KeyValuePair<string, Asset>(audioAssetName, new AssetReference<DigitizedSound>(() => LoadAudioAsset<DigitizedSound>())));
                    break;
                case nameof(ImfMusic):
                    audioAssets.Add(new KeyValuePair<string, Asset>(audioAssetName, new AssetReference<ImfMusic>(() => LoadAudioAsset<ImfMusic>())));
                    break;
            }
        }

        return audioAssets;
    }

    public T LoadAudioAsset<T>()
    {
        throw new NotImplementedException();
    }

    public List<AudioHeaderData> GetAudioHeaderList()
    {
        uint[] segmentStarts = [0, 0, 0, 0];
        uint currentSegment = 0;
        var numLumps = _headerData.Length / 4 - 1;

        var audioStarts = _headerData.ToInt32Array(_headerData.Length / sizeof(uint));

        var audioHeaders = new List<AudioHeaderData>(numLumps);

        var segmentAssetType = new[] { typeof(PcSound), typeof(AdLibSound), typeof(DigitizedSound), typeof(ImfMusic) };
        var audioFileStream = File.OpenRead(_audioDataFilePath);
        for (var index = 0; index < numLumps; index++)
        {
            int size = audioStarts[index + 1] - audioStarts[index];
            audioHeaders.Add(new AudioHeaderData
            {
                Index = index,
                DataFilePosition = audioStarts[index],
                Size = size,
                Namespace = segmentAssetType[currentSegment]
            });

            // There are 4 segments in the audiot file (pc, adlib, digi, music)
            // and they are all separated by a 4 byte marker "!ID!"

            // Try to find !ID! tags
            if (currentSegment < 3 && size >= 4)
            // Okay, size < 4, that's either 0 empty, or 4, a marker
            // What files have the !ID! tag? (This is also ASCII)
            {
                var tagBytes = new byte[4];
                var position = audioStarts[index] + size - 4;
                audioFileStream.Seek(position, System.IO.SeekOrigin.Begin);
                var bytesRead = 
                audioFileStream.Read(tagBytes);
                if (bytesRead < 4)
                    continue;
                //var tagBytes = _audioData.Skip(audioStarts[index] + size - 4).Take(4).ToArray();
                var tag = System.Text.Encoding.UTF8.GetString(tagBytes);
                if (tag.Equals(AssetMarker))
                {
                    // TODO: Add "marker" to assets
                    segmentStarts[++currentSegment] = (uint)index + 1;
                    // Lump end contains a !ID! tag, remove it from the lump
                    audioHeaders[index].Size -= 4;
                }
            }
        }

        // If after checking all of the segments, and there were not 4,
        // Find the audio entry that is 0 to 4 bytes in length
        // Most wolf3d files don't have digi sounds stored, so they'll be 0
        // and there
        if (currentSegment != 3)
        {
            for (int i = numLumps - numLumps % 3 - 1; i >= 0; i -= 3)
            {
                if (audioHeaders[i].Size <= 4)
                {
                    segmentStarts[3] = (uint)++i;
                    for (; i < numLumps; ++i)
                        audioHeaders[i].Namespace = typeof(ImfMusic);
                    break;
                }
            }
        }

        return audioHeaders;
    }

    private static string GetReferencedName(List<string> assetReferences, int i)
    {
        if (i < 0 || i >= assetReferences.Count)
            return $"AUD{i:D5}";

        return assetReferences[i];
    }
}

public class AudioHeaderData
{
    public int Index { get; init; }
    public string Name => $"Audio Header {Index} (Pos: {DataFilePosition}), NS:{Namespace}";

    /// <summary>
    /// Position in data file where chunk is stored
    /// </summary>
    public int DataFilePosition { get; init; }

    public int Size { get; set; }
    public required Type Namespace { get; set; }
}