using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PFWolf.Common.Assets;

public record Sprite : Asset
{
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] PixelData { get; set; } = Array.Empty<byte>();
    public Sprite()
    {
        Type = AssetType.Sprite;
    }
}
