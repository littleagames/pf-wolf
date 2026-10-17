using Avalonia.Input;
using PFWolf.Editor.Editing;

namespace PFWolf.Editor.Rendering;

/// <summary>The keys the map and the 3D view share for working on the selection</summary>
internal static class EditKeys
{
    /// <summary>The rotate and flip keys: ] and [ turn clockwise and back, Shift+H and Shift+V flip</summary>
    public static TileTransform? Transform(Key key, KeyModifiers modifiers) => (key, modifiers) switch
    {
        (Key.OemCloseBrackets, KeyModifiers.None) => TileTransform.RotateClockwise,
        (Key.OemOpenBrackets, KeyModifiers.None) => TileTransform.RotateCounterClockwise,
        (Key.H, KeyModifiers.Shift) => TileTransform.FlipHorizontal,
        (Key.V, KeyModifiers.Shift) => TileTransform.FlipVertical,
        _ => null,
    };
}
