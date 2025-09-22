using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PFWolf.Common.Assets;

public record Text : Asset
{
    public string Content { get; set; } = string.Empty;
    public Text()
    {
        Type = AssetType.Text;
    }
}
