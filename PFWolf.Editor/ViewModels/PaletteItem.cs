using Avalonia.Media.Imaging;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;

namespace PFWolf.Editor.ViewModels;

/// <summary>A palette entry as listed, with its picture</summary>
public sealed record PaletteItem(PaletteEntry Entry, Bitmap? Icon)
{
    public string Label => Entry.Label;
    public string Group => Entry.Group;
}

/// <summary>A tool button</summary>
public sealed record ToolOption(EditTool Tool, string Label, string Key)
{
    public string ToolTip => $"{Label} ({Key})";
}

/// <summary>A plane the tools can edit</summary>
public sealed record PlaneOption(int Plane, string Label)
{
    public override string ToString() => Label;
}
