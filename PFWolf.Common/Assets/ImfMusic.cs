using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PFWolf.Common.Assets;

public record ImfMusic : Asset
{
    public ImfMusic()
    {
        Type = AssetType.Music;
    }
}
