using System;
using System.Collections.Generic;
using System.Text;

namespace PFWolf.Assets;

public record Tile8Asset : Asset
{
    public Tile8Asset(byte[] data)
    {
        RawData = data;
    }
}
