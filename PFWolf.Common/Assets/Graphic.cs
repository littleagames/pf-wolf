﻿namespace PFWolf.Common.Assets;

public record Graphic : Asset
{
    public Graphic()
    {
    }
    public byte[] Data { get; set; } = [];
    public Vector2 Dimensions { get; set; } = Vector2.Zero;
    public Vector2 Offset { get; set; } = Vector2.Zero;
}
