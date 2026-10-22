using CommunityToolkit.Mvvm.ComponentModel;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// A level in the list: its asset name, title, and whether it has unsaved changes. A hub game's
/// floor shows its label ("M1 F2") in place of its name, and an unused slot is dimmed.
/// </summary>
public sealed partial class MapItem(string name, string title, string label = "", bool unused = false) : ObservableObject
{
    public string Name { get; } = name;
    public string Title { get; } = title;
    public string Label { get; } = label;
    public bool Unused { get; } = unused;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    private bool _isDirty;

    public string Display => $"{(Label.Length > 0 ? Label : Name)}{(IsDirty ? " *" : "")}  {Title}";

    /// <summary>The list's tooltip: the level's asset name, which a label stands in for</summary>
    public string ToolTip => Unused ? $"{Name}: a slot the game never plays" : Name;

    public double Opacity => Unused ? 0.5 : 1;
}
