using CommunityToolkit.Mvvm.ComponentModel;

namespace PFWolf.Editor.ViewModels;

/// <summary>A level in the list: its asset name, title, and whether it has unsaved changes</summary>
public sealed partial class MapItem(string name, string title) : ObservableObject
{
    public string Name { get; } = name;
    public string Title { get; } = title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    private bool _isDirty;

    public string Display => $"{Name}{(IsDirty ? " *" : "")}  {Title}";
}
