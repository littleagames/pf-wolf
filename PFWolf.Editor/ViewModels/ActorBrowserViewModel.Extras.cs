using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Editor.Data;
using PFWolf.Entities.Actors;

namespace PFWolf.Editor.ViewModels;

/// <summary>A sprite to draw in the pixel editor: one the game has, or a view it lacks, to start blank</summary>
public sealed record SpritePictureRequest(string Name, bool Exists);

/// <summary>
/// The actor window's extras: the class's sounds as the preview plays (off until asked for), the
/// frame's sprite in the pixel editor (a view it lacks started there), and the class picked in the
/// map's thing palette to put down
/// </summary>
public sealed partial class ActorBrowserViewModel
{
    private readonly ActorSoundPreview _sounds = new();

    /// <summary>Its sounds play as the preview passes the actions that make them (off: the window is quiet)</summary>
    [ObservableProperty] private bool _playSounds;

    /// <summary>The sounds the preview made last (whether or not they're played), for the line under it</summary>
    [ObservableProperty] private string _soundText = "";

    /// <summary>Asks the art window's pixel editor to draw a sprite</summary>
    public event EventHandler<SpritePictureRequest>? PictureEditRequested;

    /// <summary>Asks the map to put the class down: its first mapdefs thing picked in the palette</summary>
    public event EventHandler<string>? PlaceRequested;

    partial void OnPlaySoundsChanged(bool value)
    {
        if (!value)
            _sounds.Stop();
    }

    // The sounds of the actions that just ran, and of going from standing to chasing
    private void Sound(IEnumerable<StateEvent> events, string? fromState = null, string? toState = null)
    {
        if (Shown?.Merged is not { } merged)
            return;
        var names = events.Select(e => ActorSoundPreview.SoundOf(e.Action, merged)).OfType<string>().ToList();
        if (fromState != null && toState != null && ActorSoundPreview.SoundOfGoing(fromState, toState, merged) is { } see)
            names.Add(see);
        if (names.Count == 0)
            return;
        SoundText = "Sound: " + string.Join(", ", names);
        if (PlaySounds)
            _sounds.Play(names[^1]);
    }

    /// <summary>The frame's sprite from the view shown, in the art window's pixel editor; one it lacks is started there, blank</summary>
    [RelayCommand(CanExecute = nameof(HasPicked))]
    private void EditPicture()
    {
        if (_content == null || _player.Frame is not { } frame)
            return;
        if (ActorCatalog.PictureName(_content, frame, SelectedView, out var found) is not { } name)
        {
            Status = "The frame shows nothing (TNT1): give it a sprite first";
            return;
        }
        PictureEditRequested?.Invoke(this, new SpritePictureRequest(name, found));
        Status = found ? $"{name} in the pixel editor" : $"{name} is new: drawn in the pixel editor, it goes in a mod when it's saved";
    }

    private bool CanPlace => Shown?.Uses.Any(use => use.Target?.Kind == Definitions.DefinitionKind.MapDefs && use.Target.EntryPath is ["things", ..]) == true;

    /// <summary>The class picked in the map's thing palette, to put down on the level open</summary>
    [RelayCommand]
    private void PlaceOnMap()
    {
        if (SelectedItem is not { } item)
            return;
        if (!CanPlace)
        {
            Status = item.IsDraft ? $"{item.Name} goes in the palette once it's saved with its things"
                : $"No mapdefs thing is a {item.Name}: give it one (the Definitions window's mapdefs) to put it on a map";
            return;
        }
        PlaceRequested?.Invoke(this, item.Name);
    }
}
