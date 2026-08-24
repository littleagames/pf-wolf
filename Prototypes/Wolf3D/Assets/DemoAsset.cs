using System;
using System.Collections.Generic;
using System.Text;

namespace Wolf3D.Assets;

internal record DemoAsset : Asset
{
    public DemoAsset(byte[] data)
    {
        RawData = data;
    }

    public override void Merge(Asset other)
    {
        // For now, do nothing
    }
}
