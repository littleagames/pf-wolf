using System.Reflection;
using System.Runtime.InteropServices;

namespace PFWolf.Loaders;

/// <summary>
/// The native libraries the game and the editor bundle, from NuGet packages: SDL2 from
/// Ultz.Native.SDL and OpenAL Soft from Silk.NET.OpenAL.Soft.Native, for every runtime. Built,
/// they're in runtimes/&lt;rid&gt;/native; published for one runtime, beside the exe. Their names
/// aren't the ones the bindings ask for (SDL2-CS's "SDL2", OpenTK's OpenAL32.dll and
/// libopenal.so.1), so they're loaded by name here, ahead of the system's own.
/// </summary>
public static class NativeLibraries
{
    /// <summary>OpenAL Soft as Silk.NET.OpenAL.Soft.Native names it</summary>
    public static string BundledOpenAL =>
        OperatingSystem.IsWindows() ? "soft_oal.dll"
        : OperatingSystem.IsMacOS() ? "libopenal.dylib"
        : "libopenal.so";

    /// <summary>
    /// Loads the bundled OpenAL Soft, searching as a DllImport from <paramref name="openTk"/>
    /// would (so the runtimes folders too), and returns its name for OpenTK's
    /// OpenALLibraryNameContainer.OverridePath. The library stays loaded, so OpenTK's load of the
    /// name gets the same one. Null, with why, when it won't load: OpenTK's own names are tried.
    /// </summary>
    public static string? LoadBundledOpenAL(Assembly openTk, out string? failure)
        => TryLoadFirst(openTk, [BundledOpenAL], out _, out failure);

    /// <summary>
    /// The first of <paramref name="names"/> that loads, searching as a DllImport from
    /// <paramref name="assembly"/> would; null when none does, with each one's reason
    /// </summary>
    public static string? TryLoadFirst(Assembly assembly, IEnumerable<string> names, out IntPtr handle, out string? failure)
    {
        var reasons = new List<string>();
        foreach (var name in names)
        {
            try
            {
                handle = NativeLibrary.Load(name, assembly, null);
                failure = null;
                return name;
            }
            catch (Exception e) when (e is DllNotFoundException or BadImageFormatException)
            {
                reasons.Add($"{name}: {e.Message}");
            }
        }
        handle = IntPtr.Zero;
        failure = string.Join(Environment.NewLine, reasons);
        return null;
    }
}
