using Avalonia.Media;

namespace PFWolf.Editor.Views;

/// <summary>
/// The editor's toolbar and tool icons: shapes on a 24 × 24 grid, filled even-odd (an inner
/// shape cuts a hole), for PathIcon
/// </summary>
public static class EditorIcons
{
    // Tools
    public static Geometry Select { get; } = Parse(
        "M3,3 H9 V5 H5 V9 H3 Z M15,3 H21 V9 H19 V5 H15 Z M3,15 H5 V19 H9 V21 H3 Z M19,15 H21 V21 H15 V19 H19 Z "
        + "M11,3 H13 V5 H11 Z M11,19 H13 V21 H11 Z M3,11 H5 V13 H3 Z M19,11 H21 V13 H19 Z");
    public static Geometry MagicWand { get; } = Parse(
        "M3,19.5 L13,9.5 L15,11.5 L5,21.5 Z M18,2 L19,5 L22,6 L19,7 L18,10 L17,7 L14,6 L17,5 Z M8,4 L8.6,5.4 L10,6 L8.6,6.6 L8,8 L7.4,6.6 L6,6 L7.4,5.4 Z");
    public static Geometry Paint { get; } = Parse("M4,20 L4.8,15.6 L14,6.4 L17.6,10 L8.4,19.2 Z M15.4,5 L17.2,3.2 L20.8,6.8 L19,8.6 Z");
    public static Geometry Erase { get; } = Parse(
        "M9,8.5 L13.5,4 L21,11.5 L16.5,16 Z M13.5,6.8 L18.2,11.5 L16.5,13.2 L11.8,8.5 Z "
        + "M8,9.5 L15.5,17 L13,19.5 H8.5 L4,15 Z M3,21 H21 V22.5 H3 Z");
    public static Geometry Line { get; } = Parse("M5.5,17 L17,5.5 L18.5,7 L7,18.5 Z M2,18 H6 V22 H2 Z M18,2 H22 V6 H18 Z");
    public static Geometry Rectangle { get; } = Parse("M3,5 H21 V19 H3 Z M5,7 V17 H19 V7 Z");
    public static Geometry Ellipse { get; } = Parse("M3,12 A9,7 0 1 1 21,12 A9,7 0 1 1 3,12 Z M5,12 A7,5 0 1 0 19,12 A7,5 0 1 0 5,12 Z");
    public static Geometry Room { get; } = Parse("M3,4 H21 V20 H14 V18 H19 V6 H5 V18 H10 V20 H3 Z M8,9 H16 V15 H8 Z");
    public static Geometry Fill { get; } = Parse(
        "M10,3 L18,11 L11,18 L3,10 Z M10,6 L6,10 L11,15 L15,11 Z M6.8,10.8 L14.2,10.8 L11,14 Z "
        + "M19,13 C19,13 22,16.5 22,18.5 A3,3 0 0 1 16,18.5 C16,16.5 19,13 19,13 Z");
    public static Geometry Stamp { get; } = Parse("M9,3 H15 V9 L13,11 H19 V16 H5 V11 H11 L9,9 Z M4,18 H20 V21 H4 Z");
    public static Geometry Scatter { get; } = Parse(
        "M4,4 H7 V7 H4 Z M11,3 H14 V6 H11 Z M18,6 H21 V9 H18 Z M7,11 H10 V14 H7 Z M15,12 H18 V15 H15 Z "
        + "M3,18 H6 V21 H3 Z M11,18 H14 V21 H11 Z M19,18 H22 V21 H19 Z");
    // A thing (a dot) inside a ring, off the corner of a tile
    public static Geometry FreeThing { get; } = Parse(
        "M2,2 H11 V4 H4 V11 H2 Z M15,9 A6,6 0 1 1 15,21 A6,6 0 1 1 15,9 Z M15,11 A4,4 0 1 0 15,19 A4,4 0 1 0 15,11 Z "
        + "M15,13 A2,2 0 1 1 15,17 A2,2 0 1 1 15,13 Z");
    public static Geometry Pick { get; } = Parse("M15,3 L21,9 L18,12 L12,6 Z M12.5,8.5 L15.5,11.5 L7,20 L4,20 L4,17 Z");

    // Commands
    public static Geometry Undo { get; } = Parse("M9,5 L3,10 L9,15 V11.5 H14 A4.5,4.5 0 0 1 14,20.5 H11 V22.5 H14 A6.5,6.5 0 0 0 14,9.5 H9 Z");
    public static Geometry Redo { get; } = Parse("M15,5 L21,10 L15,15 V11.5 H10 A4.5,4.5 0 0 0 10,20.5 H13 V22.5 H10 A6.5,6.5 0 0 1 10,9.5 H15 Z");
    public static Geometry Save { get; } = Parse("M4,3 H17 L21,7 V21 H4 Z M7,5 V9 H15 V5 Z M7,13 V19 H18 V13 Z");
    public static Geometry Play { get; } = Parse("M7,4 L20,12 L7,20 Z");
    public static Geometry View3D { get; } = Parse(
        "M12,2 L21,7 V17 L12,22 L3,17 V7 Z M12,4.3 L5,8.2 L12,12 L19,8.2 Z M5,9.9 V15.9 L11,19.4 V13.4 Z M13,13.4 V19.4 L19,15.9 V9.9 Z");
    public static Geometry RotateClockwise { get; } = Parse("M12,21 A8,8 0 0 1 12,5 V7 A6,6 0 0 0 12,19 Z M12,2 L17,6 L12,10 Z");
    public static Geometry RotateCounterClockwise { get; } = Parse("M12,21 A8,8 0 0 0 12,5 V7 A6,6 0 0 1 12,19 Z M12,2 L7,6 L12,10 Z");
    public static Geometry FlipHorizontal { get; } = Parse(
        "M11,3 H13 V7 H11 Z M11,10 H13 V14 H11 Z M11,17 H13 V21 H11 Z M9,6 V18 H2 Z M15,6 L22,18 H15 Z M16.5,11 L19.4,16.5 H16.5 Z");
    public static Geometry FlipVertical { get; } = Parse(
        "M3,11 H7 V13 H3 Z M10,11 H14 V13 H10 Z M17,11 H21 V13 H17 Z M6,9 H18 V2 Z M6,15 L18,22 V15 Z M11,16.5 L16.5,19.4 V16.5 Z");

    private static Geometry Parse(string data) => Geometry.Parse(data);
}
