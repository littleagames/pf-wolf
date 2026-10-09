using System;
using System.Collections.Generic;
using System.Text;

namespace PFWolf.Assets;

public record DemoAsset : Asset
{
    public DemoAsset(byte[] data)
    {
        RawData = data;
    }
}
