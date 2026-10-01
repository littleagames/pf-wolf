using System.Globalization;

namespace Wolf3D.Entities.Actors;

/// <summary>
/// A state's `light:` (actordefs), which overrides the actor's light (its `light.*` properties)
/// while the actor is on that state's frames:
///   light: none                            no light
///   light: 200                             the actor's light, at this intensity
///   light: { intensity: 200, radius: 2, color: "#FFD080" }
///                                          any of them, the rest from the actor's light
/// </summary>
internal sealed record StateLight(bool Off, int? Intensity, double? Radius, string? Color = null)
{
    public static StateLight? Parse(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case Dictionary<object, object> fields:
            {
                int? intensity = null;
                double? radius = null;
                string? color = null;
                foreach (var (key, field) in fields)
                {
                    switch (key.ToString()?.ToLowerInvariant())
                    {
                        case "intensity": intensity = ToInt(field, "intensity"); break;
                        case "radius": radius = ToDouble(field, "radius"); break;
                        case "color": color = field?.ToString(); break;
                        default: Console.WriteLine($"Unknown state light field '{key}' (intensity, radius or color)"); break;
                    }
                }
                return new StateLight(false, intensity, radius, color);
            }
            default:
            {
                var text = value.ToString()?.Trim() ?? "";
                if (text.Equals("none", StringComparison.OrdinalIgnoreCase) || text.Equals("off", StringComparison.OrdinalIgnoreCase))
                    return new StateLight(true, null, null);
                return new StateLight(false, ToInt(text, "intensity"), null);
            }
        }
    }

    private static int? ToInt(object? value, string what)
    {
        if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            return Math.Clamp(number, 0, 255);
        Console.WriteLine($"State light {what} '{value}' isn't a number from 0 to 255");
        return null;
    }

    private static double? ToDouble(object? value, string what)
    {
        if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && number >= 0)
            return number;
        Console.WriteLine($"State light {what} '{value}' isn't a number of tiles");
        return null;
    }
}
