using CommunityToolkit.Mvvm.ComponentModel;
using PFWolf.Constants;
using PFWolf.Editor.Editing;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// A level's width and height as the size fields show them, and which part of it stays where
/// it is when they change (picked on a 3x3 grid of corners, edges and the middle)
/// </summary>
public sealed partial class MapSizeViewModel : ObservableObject
{
    public MapSizeViewModel(int width, int height)
    {
        OldWidth = width;
        OldHeight = height;
        _width = width;
        _height = height;
    }

    public int OldWidth { get; }
    public int OldHeight { get; }

    public static decimal Minimum => MapConstants.MinMapSize;
    public static decimal Maximum => MapConstants.MaxMapSize;

    /// <summary>The anchor grid's cells, in <see cref="MapAnchor"/> order</summary>
    public static IReadOnlyList<string> AnchorGlyphs { get; } = ["↖", "↑", "↗", "←", "•", "→", "↙", "↓", "↘"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChanged), nameof(Summary))]
    private decimal? _width;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChanged), nameof(Summary))]
    private decimal? _height;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Anchor), nameof(Summary))]
    private int _anchorIndex = (int)MapAnchor.Center;

    public MapAnchor Anchor => (MapAnchor)Math.Clamp(AnchorIndex, 0, 8);

    /// <summary>The fields hold a size other than the level's</summary>
    public bool IsChanged => Width is { } width && Height is { } height && ((int)width != OldWidth || (int)height != OldHeight);

    /// <summary>What happens to the level at the size entered</summary>
    public string Summary
    {
        get
        {
            if (Width is not { } width || Height is not { } height)
                return $"It's {OldWidth}x{OldHeight} now.";
            if (!IsChanged)
                return $"It's {OldWidth}x{OldHeight} now; Wolf3D's levels are 64x64.";
            var where = MapResizing.AnchorNames[(int)Anchor];
            return $"{OldWidth}x{OldHeight} becomes {(int)width}x{(int)height}, the {where} staying where it is. "
                   + "New tiles are wall; what no longer fits is cut off. One undo step puts it back.";
        }
    }

    /// <summary>The size entered, checked against what a level can be</summary>
    /// <exception cref="FormatException">Saying what's wrong with it</exception>
    public (int Width, int Height) Build()
    {
        if (Width is not { } width || Height is not { } height)
            throw new FormatException("the level needs a width and a height");
        if (MapConstants.CheckMapSize((int)width, (int)height) is { } bad)
            throw new FormatException(bad);
        return ((int)width, (int)height);
    }
}
