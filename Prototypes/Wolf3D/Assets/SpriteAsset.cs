using System;
using System.Collections.Generic;
using System.Text;

namespace Wolf3D.Assets;

internal record SpriteAsset : Asset
{

    public override void Merge(Asset other)
    {
        // For now, do nothing
    }
}
