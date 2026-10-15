using PFWolf.Assets;

namespace PFWolf;

internal partial class Program
{
    // Rain and snow (Rendering/Weather.cs)
    static Weather weather = null!;     // made in InitServices

    static Weather MakeWeather() => new(_gameEngineManager, _videoManager, _mapManager, lighting, render, session, FloorRow, IsArch);

    internal static WeatherInfo? levelweather => weather.levelweather;
    internal static string? levelweathername => weather.levelweathername;
    static bool weatheron => weather.weatheron;

    internal static Dictionary<string, WeatherInfo> WeatherTypes() => weather.WeatherTypes();
    internal static void SetWeather(string? name) => weather.SetWeather(name);
    static void SetupWeather() => weather.SetupWeather();
    static void NoteOccluders() => weather.NoteOccluders();
    static void ProjectWeather() => weather.ProjectWeather();
    static void DrawWeatherBeyond(int height) => weather.DrawWeatherBeyond(height);
}