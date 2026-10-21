using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace PFWolf.Ui;

/// <summary>
/// The mod catalog on the docs site (PFWolf.csproj's ModCatalog address): mods to download, which
/// the Mods menu opens in the player's browser
/// </summary>
internal static class ModCatalog
{
    /// <summary>Its address, or null in a build that doesn't name one</summary>
    internal static string? Url { get; } = typeof(ModCatalog).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "ModCatalog")?.Value;

    /// <summary>
    /// Its address for the menu font, which fits about 30 letters a line: without its https://,
    /// split before its path ("www.littleagames.com\n/pf-wolf/docs/mods/")
    /// </summary>
    internal static string ShortText(string url)
    {
        var text = url.Replace("https://", "").Replace("http://", "");
        var slash = text.IndexOf('/');
        return slash > 0 && slash < text.Length - 1 ? $"{text[..slash]}\n{text[slash..]}" : text;
    }

    /// <summary>Opens it in the player's browser; false when none could be started</summary>
    internal static bool Open()
    {
        if (Url == null)
            return false;
        try
        {
            Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            Console.WriteLine($"Couldn't open {Url} in a browser: {e.Message}");
            return false;
        }
    }
}
