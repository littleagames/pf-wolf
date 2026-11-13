using System.Reflection;
using System.Runtime.InteropServices;
using PFWolf.Loaders;
using SDL2;

namespace PFWolf.Startup;

/// <summary>
/// Where SDL2 is loaded from. SDL2-CS asks for "SDL2", which finds SDL2.dll on Windows but not
/// the names elsewhere: the bundled library (Ultz.Native.SDL's libSDL2-2.0.so/.dylib), else the
/// system's (a Linux distribution's libSDL2-2.0.so.0, Homebrew's on macOS).
/// </summary>
internal static class NativeSdl
{
    private const string LibraryName = "SDL2";

    /// <summary>Before anything calls SDL</summary>
    internal static void UseBundled()
    {
        if (OperatingSystem.IsWindows())
            return;
        NativeLibrary.SetDllImportResolver(typeof(SDL).Assembly, Resolve);
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibraryName)
            return IntPtr.Zero;
        // Zero leaves it to the usual search, which then fails with its own message
        return NativeLibraries.TryLoadFirst(assembly, Names, out var handle, out _) != null ? handle : IntPtr.Zero;
    }

    private static IEnumerable<string> Names => OperatingSystem.IsMacOS()
        ? ["libSDL2-2.0.dylib", "libSDL2-2.0.0.dylib", "libSDL2.dylib",
           "/opt/homebrew/lib/libSDL2-2.0.0.dylib", "/usr/local/lib/libSDL2-2.0.0.dylib"]
        : ["libSDL2-2.0.so", "libSDL2-2.0.so.0", "libSDL2.so"];
}
