using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace PFWolf.Editor.Rendering;

/// <summary>Lua's colors in the script browser's code editor: Resources/Lua.xshd, read once</summary>
public static class LuaHighlighting
{
    private static IHighlightingDefinition? _definition;

    public static IHighlightingDefinition Definition => _definition ??= Load();

    private static IHighlightingDefinition Load()
    {
        using var stream = typeof(LuaHighlighting).Assembly.GetManifestResourceStream("Lua.xshd")
            ?? throw new InvalidOperationException("Lua.xshd isn't in the editor's resources");
        using var reader = XmlReader.Create(stream);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}
