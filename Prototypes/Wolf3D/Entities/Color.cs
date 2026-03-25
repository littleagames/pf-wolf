namespace Wolf3D.Entities;

public record Color
{
    public byte Red { get; set; }
    public byte Green { get; set; }
    public byte Blue { get; set; }
    public byte Alpha { get; set; }
    
    public static Color FromByteRGB(string rgbValue)
    {
        if (string.IsNullOrWhiteSpace(rgbValue))
            throw new ArgumentException("Value cannot be null or empty.", nameof(rgbValue));

        string[] parts = rgbValue.Split(' ');

        if (parts.Length != 3)
            throw new ArgumentException("Value must be in the format 'R G B'.", nameof(rgbValue));

        return new Color
        {
            Red   = byte.Parse(parts[0]),
            Green = byte.Parse(parts[1]),
            Blue  = byte.Parse(parts[2]),
            Alpha = 255
        };
    }

    public static Color FromHexRGBA(string rgbaValue)
    {
        if (string.IsNullOrWhiteSpace(rgbaValue))
            throw new ArgumentException("Value cannot be null or empty.", nameof(rgbaValue));

        string hex = rgbaValue.TrimStart('#');

        if (hex.Length != 6 && hex.Length != 8)
            throw new ArgumentException("Value must be in the format #RRGGBB or #RRGGBBAA.", nameof(rgbaValue));

        return new Color
        {
            Red   = Convert.ToByte(hex[0..2], 16),
            Green = Convert.ToByte(hex[2..4], 16),
            Blue  = Convert.ToByte(hex[4..6], 16),
            Alpha = hex.Length == 8 ? Convert.ToByte(hex[6..8], 16) : (byte)255
        };
    }
}
