using System;
using System.Collections.Generic;
using System.Text;

namespace Wolf3D.Assets;

public abstract record Asset
{
    public byte[] RawData { get; set; } = [];

    public int Size => RawData.Length;
}
