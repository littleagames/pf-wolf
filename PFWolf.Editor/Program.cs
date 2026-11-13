using Avalonia;

namespace PFWolf.Editor;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Also used by the XAML previewer. On macOS OpenGL is asked for ahead of Metal: the 3D view
    // (Rendering/LevelView3D) draws with OpenGL, through Avalonia's context.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new AvaloniaNativePlatformOptions
            {
                RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Metal, AvaloniaNativeRenderingMode.Software],
            })
            .WithInterFont()
            .LogToTrace();
}
