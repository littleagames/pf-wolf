using PFWolf.Assets;

namespace PFWolf;

internal static partial class GameHost
{
    // Rain and snow (Rendering/Weather.cs)
    static Weather weather = null!;     // made in InitServices

    static Weather MakeWeather() => new(_gameEngineManager, _videoManager, _mapManager, lighting, render, session, FloorRow, IsArch);
}
