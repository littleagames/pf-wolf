using System.Reflection;
using System.Runtime.InteropServices;
using PFWolf.Loaders;
using SDL2;

namespace PFWolf.Startup;

/// <summary>
/// Where SDL2 is loaded from. SDL2-CS asks for "SDL2", which finds SDL2.dll on Windows but not
/// the names elsewhere: the bundled library (Ultz.Native.SDL's libSDL2-2.0.so/.dylib), else the
/// system's (a Linux distribution's libSDL2-2.0.so.0, Homebrew's on macOS). Also the SDL calls
/// newer than SDL2-CS 1.0.82's bindings, declared here.
/// </summary>
internal static partial class NativeSdl
{
    private const string LibraryName = "SDL2";

    /// <summary>Before anything calls SDL</summary>
    internal static void UseBundled()
    {
        if (OperatingSystem.IsWindows())
            return;
        NativeLibrary.SetDllImportResolver(typeof(SDL).Assembly, Resolve);
        // (This assembly's own SDL calls, below)
        NativeLibrary.SetDllImportResolver(typeof(NativeSdl).Assembly, Resolve);
    }

    /// <summary>A window's size in pixels: twice its size in points on a Retina Mac (SDL 2.26 and later)</summary>
    [LibraryImport(LibraryName)]
    internal static partial void SDL_GetWindowSizeInPixels(IntPtr window, out int width, out int height);

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
