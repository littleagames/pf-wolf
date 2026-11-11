using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Definitions;
using PFWolf.Editor.Data;
using PFWolf.Editor.Definitions;
using PFWolf.Editor.Editing;
using PFWolf.Entities.Actors;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.ViewModels;

/// <summary>A frame letter of a sprite, with its picture, for the letter picker</summary>
public sealed record LetterChoice(string Letter, Bitmap? Picture)
{
    public override string ToString() => Letter;
}

/// <summary>How a state can end, for its picker: loop, stop, or on to another state</summary>
public sealed record EndChoice(string Label, StateEnd End, string? Next)
{
    public override string ToString() => Label;
}

/// <summary>
/// The actor window's state editing: the frame the preview is on, its sprite, letter, tics, think,
/// action, modifiers, light and particles; frames added, copied, taken out and moved; states added,
/// renamed and taken out, and how each ends. A state is written whole into the class when it
/// changes (a parent's state changed becomes the class's own); a Raise the engine makes from a
/// class's Death is only shown.
/// </summary>
public sealed partial class ActorBrowserViewModel
{
    // Set while the frame's fields are filled from the class, so they don't change it
    private bool _filling;

    /// <summary>The sprite names (GARD) the game has, for the sprite field</summary>
    [ObservableProperty] private IReadOnlyList<string> _spriteNames = [];

    /// <summary>The thinks and the actions an actor of this kind can run (the engine's and its scripts'), for those fields</summary>
    [ObservableProperty] private IReadOnlyList<string> _thinkNames = [];
    [ObservableProperty] private IReadOnlyList<string> _actionNames = [];

    /// <summary>The letters the frame's sprite has, each with its picture</summary>
    [ObservableProperty] private IReadOnlyList<LetterChoice> _letterChoices = [];

    /// <summary>How the state picked can end</summary>
    [ObservableProperty] private IReadOnlyList<EndChoice> _endChoices = [];

    [ObservableProperty] private EndChoice? _selectedEnd;

    // The frame's fields
    [ObservableProperty] private string _frameSprite = "";
    [ObservableProperty] private LetterChoice? _frameLetter;
    [ObservableProperty] private decimal? _frameTics;
    [ObservableProperty] private string _frameThink = "";
    [ObservableProperty] private string _frameAction = "";
    [ObservableProperty] private string _frameModifiers = "";
    [ObservableProperty] private string _frameLight = "";
    [ObservableProperty] private string _frameParticles = "";

    /// <summary>What the frame's think and action do</summary>
    [ObservableProperty] private string _callHelp = "";

    /// <summary>Whose state it is: the class's own, a parent's (changing it makes it its own), or the engine's (a Raise)</summary>
    [ObservableProperty] private string _stateNote = "";

    /// <summary>The state picked can be changed: not a Raise the engine makes</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditFrame))]
    [NotifyCanExecuteChangedFor(nameof(AddFrameCommand), nameof(DuplicateFrameCommand), nameof(DeleteFrameCommand),
        nameof(MoveFrameLeftCommand), nameof(MoveFrameRightCommand))]
    private bool _stateEditable;

    /// <summary>The state picked is the class's own (so it can be renamed or taken out)</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameStateCommand), nameof(DeleteStateCommand))]
    private bool _stateIsOwn;

    /// <summary>The frame shown (its place in the state), or null while it plays</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditFrame))]
    [NotifyCanExecuteChangedFor(nameof(AddFrameCommand), nameof(DuplicateFrameCommand), nameof(DeleteFrameCommand),
        nameof(MoveFrameLeftCommand), nameof(MoveFrameRightCommand))]
    private int? _frameIndex;

    /// <summary>The frame's fields can be changed: paused on a frame of a state that can be</summary>
    public bool CanEditFrame => StateEditable && FrameIndex != null;

    partial void OnPlayingChanged(bool value) => UpdateFramePanel();

    // What the pickers offer, read again as the game loads
    private void LoadStatePickers(GameContent content)
    {
        SpriteNames = [.. content.Assets.AssetNames
            .Where(name => !name.Contains('/') && name.Length == 6 && char.IsDigit(name[^1]) && content.Assets.Exists<SpriteAsset>(name))
            .Select(name => name[..4].ToUpperInvariant()).Distinct().Order(StringComparer.Ordinal)];
    }

    // The thinks and actions for the class's kind, and its states' endings
    private void FillStatePickers(ActorEntry entry)
    {
        var fits = GameActions.All.Where(action => action.Use.HasFlag(ActionUse.Actor)
                                                   && (action.Needs == null || ActorClasses.IsA(entry.EngineClass, action.Needs))).ToList();
        ThinkNames = [.. fits.Where(action => action.Name.StartsWith("T_", StringComparison.Ordinal)).Select(action => action.Name)
            .Concat(_scriptActions).Distinct().Order(StringComparer.OrdinalIgnoreCase)];
        ActionNames = [.. fits.Where(action => !action.Name.StartsWith("T_", StringComparison.Ordinal)).Select(action => action.Name)
            .Concat(_scriptActions).Distinct().Order(StringComparer.OrdinalIgnoreCase)];
    }

    //
    // The state picked, as written
    //

    // The class's fields as edited (or as the files have them), to change
    private ClassEdit? CurrentEdit => SelectedItem != null && _edits.TryGetValue(SelectedItem.Name, out var edit) ? edit : null;

    // A state as written: the class's own, else the nearest parent's that has it; null for one the engine makes (Raise)
    private (StateSpec Spec, string Owner)? WrittenState(string state)
    {
        if (CurrentEdit is not { } edit || Shown is not { } entry || _classSet == null)
            return null;
        if (YamlNodes.Get(edit.Root.Build([]), ["states", state]) is { } own)
            return (StateFrames.Read(own), entry.Name);
        foreach (var ancestor in entry.Ancestors)
        {
            var value = _classSet.ValueOf([ancestor, "states", state], baseOnly: false) ?? YamlNodes.Get(_drafts.GetValueOrDefault(ancestor), ["states", state]);
            if (value != null)
                return (StateFrames.Read(value), ancestor);
        }
        return null;
    }

    // The state picked written anew (null: taken out of the class), as one change to undo
    private void ChangeState(string state, Func<StateSpec, StateSpec?> change, string? select = null, int? frame = null)
    {
        if (CurrentEdit is not { } edit || WrittenState(state) is not { } written)
            return;
        var result = change(written.Spec.Clone());
        if (edit.Root.Build([]) is not YamlMappingNode value)
            value = new YamlMappingNode();
        else
            value = (YamlMappingNode)YamlNodes.Clone(value);
        if (YamlNodes.Get(value, ["states"]) is not YamlMappingNode states)
        {
            states = new YamlMappingNode();
            value.Children[new YamlScalarNode("states")] = states;
        }
        var key = YamlNodes.FindKey(states, state) ?? new YamlScalarNode(state);
        if (result == null)
            states.Children.Remove(key);
        else
            states.Children[key] = StateFrames.Write(result);
        Apply(edit, value, select ?? state, frame ?? FrameIndex);
    }

    // The class's fields set to a value, as a step to undo, the state and frame shown kept
    private void Apply(ClassEdit edit, YamlNode value, string? state, int? frame)
    {
        if (edit.Undo.Count == 0 || DateTime.UtcNow - edit.LastStep > TimeSpan.FromSeconds(1))
            edit.Undo.Add(edit.Last == null ? null : YamlNodes.Clone(edit.Last));
        edit.Redo.Clear();
        edit.LastStep = DateTime.UtcNow;
        Playing = false;
        ShowFields(edit, value);
        if (state != null && States.Contains(state) && SelectedState != state)
            SelectedState = state;
        ShowFrameAt(frame);
    }

    // The preview paused on a frame of the state picked
    private void ShowFrameAt(int? index)
    {
        if (index is not int at || Shown?.States is not { } states || SelectedState == null || !states.TryGetValue(SelectedState, out var first))
            return;
        var frames = ActorCatalog.FramesOf(first);
        if (frames.Count == 0)
            return;
        Playing = false;
        JumpText = "";
        _player.Show(frames[Math.Clamp(at, 0, frames.Count - 1)]);
        Moved();
    }

    // The frame panel: the frame the preview's paused on, as written
    private void UpdateFramePanel()
    {
        _filling = true;
        try
        {
            var state = SelectedState;
            var written = state == null ? null : WrittenState(state);
            StateEditable = written != null;
            StateIsOwn = written?.Owner == SelectedItem?.Name && written != null;
            StateNote = state == null ? ""
                : written == null ? $"{state} is made by the engine from its Death, backwards: add a {state} state of its own to change it"
                : written.Value.Owner != SelectedItem?.Name ? $"{state} is {written.Value.Owner}'s: changing it gives {SelectedItem?.Name} its own"
                : "";
            if (written is { } w)
            {
                EndChoices = [new("loops back to its first frame", StateEnd.Loop, null), new("stops on its last frame", StateEnd.Stop, null),
                    .. States.Where(other => other != state && other != ActorStateResolver.RaiseState).Select(other => new EndChoice($"goes on to {other}", StateEnd.Next, other)),
                    .. w.Spec.End == StateEnd.Next && !States.Contains(w.Spec.Next ?? "") || w.Spec.Next == state
                        ? [new EndChoice($"goes on to {w.Spec.Next}", StateEnd.Next, w.Spec.Next)] : Array.Empty<EndChoice>()];
                SelectedEnd = EndChoices.FirstOrDefault(choice => choice.End == w.Spec.End && (choice.End != StateEnd.Next || choice.Next == w.Spec.Next));
            }
            else
            {
                EndChoices = [];
                SelectedEnd = null;
            }

            // The frame: the one paused on
            int? index = !Playing && _player.Frame is { } current ? Timeline.FirstOrDefault(item => item.Frame == current)?.Index : null;
            FrameIndex = written != null && index is int i && i < written.Value.Spec.Frames.Count ? i : null;
            if (FrameIndex is int at && written is { } found)
            {
                var frame = found.Spec.Frames[at];
                FrameSprite = frame.Sprite;
                LetterChoices = Letters(frame.Sprite, frame.Letter);
                FrameLetter = LetterChoices.FirstOrDefault(choice => choice.Letter == frame.Letter);
                FrameTics = decimal.TryParse(frame.Tics, NumberStyles.Float, CultureInfo.InvariantCulture, out var tics) ? tics : null;
                FrameThink = frame.Think ?? "";
                FrameAction = frame.Action ?? "";
                FrameModifiers = string.Join(", ", frame.ModifierList);
                FrameLight = frame.Light == null ? "" : YamlText.Inline(frame.Light);
                FrameParticles = frame.Particles == null ? "" : YamlText.Inline(frame.Particles);
            }
            else
            {
                (FrameSprite, FrameThink, FrameAction, FrameModifiers, FrameLight, FrameParticles) = ("", "", "", "", "", "");
                (FrameLetter, FrameTics, LetterChoices) = (null, null, []);
            }
            CallHelp = Help(FrameThink, FrameAction);
        }
        finally
        {
            _filling = false;
        }
    }

    // The letters a sprite has, each with its picture from the front; the frame's own kept even when it has none
    private IReadOnlyList<LetterChoice> Letters(string sprite, string? keep)
    {
        if (_content == null)
            return [];
        var letters = new SortedSet<string>(StringComparer.Ordinal);
        if (sprite.Length > 0)
            foreach (var name in _content.Assets.AssetNames.Where(name => name.Length == sprite.Length + 2 && name.StartsWith(sprite, StringComparison.OrdinalIgnoreCase)
                                                                         && char.IsDigit(name[^1]) && _content.Assets.Exists<SpriteAsset>(name)))
                letters.Add(name[^2..^1].ToUpperInvariant());
        if (!string.IsNullOrEmpty(keep))
            letters.Add(keep);
        return [.. letters.Select(letter => new LetterChoice(letter, LetterPicture(sprite, letter)))];
    }

    private Bitmap? LetterPicture(string sprite, string letter)
        => _content?.Find<SpriteAsset>($"{sprite}{letter}0") != null ? Picture($"{sprite}{letter}0")
            : _content?.Find<SpriteAsset>($"{sprite}{letter}1") != null ? Picture($"{sprite}{letter}1") : null;

    // What a think and an action do, from the engine's list
    private static string Help(string think, string action)
    {
        var lines = new List<string>();
        foreach (var call in new[] { think, action }.Where(call => !string.IsNullOrWhiteSpace(call)))
        {
            var name = ActorCatalog.CallName(call);
            lines.Add(GameActions.Find(name) is { } known
                ? $"{known.Signature}: {known.Description}" + (known.Needs != null ? $" (a {known.Needs}'s)" : "")
                : $"{name}: not one of the engine's (a script's game.defineaction, or a mistake)");
        }
        return string.Join("\n", lines);
    }

    //
    // The frame's fields changed
    //

    partial void OnFrameSpriteChanged(string value)
    {
        if (_filling)
            return;
        ChangeFrame(frame => frame with { Sprite = value.Trim().ToUpperInvariant() });
    }

    partial void OnFrameLetterChanged(LetterChoice? value)
    {
        if (_filling || value == null)
            return;
        ChangeFrame(frame => frame with { Letter = value.Letter });
    }

    partial void OnFrameTicsChanged(decimal? value)
    {
        if (_filling || value == null)
            return;
        ChangeFrame(frame => frame with { Tics = ((int)value.Value).ToString(CultureInfo.InvariantCulture) });
    }

    partial void OnFrameThinkChanged(string value)
    {
        if (!_filling)
            ChangeFrame(frame => frame with { Think = NullIfBlank(value) });
    }

    partial void OnFrameActionChanged(string value)
    {
        if (!_filling)
            ChangeFrame(frame => frame with { Action = NullIfBlank(value) });
    }

    partial void OnFrameModifiersChanged(string value)
    {
        if (!_filling)
            ChangeFrame(frame => frame with
            {
                Modifiers = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } list ? list : null,
            });
    }

    partial void OnFrameLightChanged(string value)
    {
        if (!_filling)
            ChangeFrame(frame => frame with { Light = Node(value) });
    }

    partial void OnFrameParticlesChanged(string value)
    {
        if (!_filling)
            ChangeFrame(frame => frame with { Particles = Node(value) });
    }

    private static string? NullIfBlank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    // A value typed as YAML on one line ({ intensity: 200 }, none, a list)
    private YamlNode? Node(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        try
        {
            return YamlNodes.Get(YamlText.ParseRoot("value: " + text.Trim()), ["value"]);
        }
        catch (Exception e) when (e is YamlDotNet.Core.YamlException or InvalidDataException)
        {
            Status = $"'{text}' doesn't read as YAML: {e.Message}";
            return new YamlScalarNode(text.Trim());
        }
    }

    // The frame shown changed
    private void ChangeFrame(Func<FrameSpec, FrameSpec> change)
    {
        if (SelectedState is not { } state || FrameIndex is not int at)
            return;
        ChangeState(state, spec =>
        {
            if (at < spec.Frames.Count)
                spec.Frames[at] = change(spec.Frames[at]);
            return spec;
        });
    }

    partial void OnSelectedEndChanged(EndChoice? value)
    {
        if (_filling || value == null || SelectedState is not { } state)
            return;
        ChangeState(state, spec =>
        {
            (spec.End, spec.Next) = (value.End, value.Next);
            return spec;
        });
    }

    //
    // Frames and states added, copied, taken out and moved
    //

    /// <summary>A frame after the one shown: the sprite's next letter, for as long, with no action</summary>
    [RelayCommand(CanExecute = nameof(CanEditFrame))]
    private void AddFrame()
    {
        if (SelectedState is not { } state || FrameIndex is not int at)
            return;
        ChangeState(state, spec =>
        {
            var shown = spec.Frames[at];
            var letter = shown.Letter.Length == 1 && shown.Letter[0] < 'Z' ? ((char)(shown.Letter[0] + 1)).ToString() : shown.Letter;
            spec.Frames.Insert(at + 1, shown with { Letter = letter, Action = null });
            return spec;
        }, frame: at + 1);
    }

    /// <summary>A copy of the frame shown, after it</summary>
    [RelayCommand(CanExecute = nameof(CanEditFrame))]
    private void DuplicateFrame()
    {
        if (SelectedState is not { } state || FrameIndex is not int at)
            return;
        ChangeState(state, spec =>
        {
            spec.Frames.Insert(at + 1, spec.Frames[at]);
            return spec;
        }, frame: at + 1);
    }

    /// <summary>Takes the frame shown out of the state (not its last one: take the state out instead)</summary>
    [RelayCommand(CanExecute = nameof(CanEditFrame))]
    private void DeleteFrame()
    {
        if (SelectedState is not { } state || FrameIndex is not int at)
            return;
        if (WrittenState(state) is { Spec.Frames.Count: <= 1 })
        {
            Status = "It's the state's only frame: take the state out instead";
            return;
        }
        ChangeState(state, spec =>
        {
            spec.Frames.RemoveAt(at);
            return spec;
        }, frame: Math.Max(at - 1, 0));
    }

    [RelayCommand(CanExecute = nameof(CanEditFrame))]
    private void MoveFrameLeft() => MoveFrame(-1);

    [RelayCommand(CanExecute = nameof(CanEditFrame))]
    private void MoveFrameRight() => MoveFrame(1);

    private void MoveFrame(int by)
    {
        if (SelectedState is not { } state || FrameIndex is not int at || WrittenState(state) is not { } written)
            return;
        int to = at + by;
        if (to < 0 || to >= written.Spec.Frames.Count)
            return;
        ChangeState(state, spec =>
        {
            (spec.Frames[at], spec.Frames[to]) = (spec.Frames[to], spec.Frames[at]);
            return spec;
        }, frame: to);
    }

    /// <summary>Moves a frame of the state picked to another place in it (a drag on the timeline)</summary>
    public void MoveFrame(int from, int to)
    {
        if (SelectedState is not { } state || !StateEditable || from == to || WrittenState(state) is not { } written
            || from < 0 || to < 0 || from >= written.Spec.Frames.Count || to >= written.Spec.Frames.Count)
            return;
        ChangeState(state, spec =>
        {
            var frame = spec.Frames[from];
            spec.Frames.RemoveAt(from);
            spec.Frames.Insert(to, frame);
            return spec;
        }, frame: to);
    }

    /// <summary>Sets a frame's tics (its edge dragged on the timeline): one step to undo</summary>
    public void SetFrameTics(int index, int tics)
    {
        if (SelectedState is not { } state || !StateEditable || WrittenState(state) is not { } written || index < 0 || index >= written.Spec.Frames.Count)
            return;
        tics = Math.Max(tics, 0);
        if (written.Spec.Frames[index].Tics == tics.ToString(CultureInfo.InvariantCulture))
            return;
        ChangeState(state, spec =>
        {
            spec.Frames[index] = spec.Frames[index] with { Tics = tics.ToString(CultureInfo.InvariantCulture) };
            return spec;
        }, frame: index);
        Status = $"{state} frame {index + 1}: {tics} tics";
    }

    /// <summary>
    /// A frame of the state picked put at the end of another state (dropped on its name): moved, or
    /// with <paramref name="copy"/> copied; a state's only frame is always copied. That state is
    /// shown after, on the frame.
    /// </summary>
    public void MoveFrameToState(int index, string target, bool copy)
    {
        if (SelectedState is not { } state || target == state || CurrentEdit is not { } edit
            || WrittenState(state) is not { } from || WrittenState(target) is not { } to
            || index < 0 || index >= from.Spec.Frames.Count)
        {
            if (target != SelectedState && WrittenState(target) == null)
                Status = $"{target} is made by the engine: add a {target} state of its own first";
            return;
        }
        copy |= from.Spec.Frames.Count == 1;

        var source = from.Spec.Clone();
        var destination = to.Spec.Clone();
        var frame = source.Frames[index];
        destination.Frames.Add(frame);
        if (!copy)
            source.Frames.RemoveAt(index);

        var value = (YamlMappingNode)YamlNodes.Clone(edit.Root.Build([]) ?? new YamlMappingNode());
        if (YamlNodes.Get(value, ["states"]) is not YamlMappingNode states)
            value.Children[new YamlScalarNode("states")] = states = new YamlMappingNode();
        if (!copy)
            states.Children[YamlNodes.FindKey(states, state) ?? new YamlScalarNode(state)] = StateFrames.Write(source);
        states.Children[YamlNodes.FindKey(states, target) ?? new YamlScalarNode(target)] = StateFrames.Write(destination);
        Apply(edit, value, target, destination.Frames.Count - 1);
        Status = $"{frame.Sprite}{frame.Letter} {(copy ? "copied" : "moved")} from {state} to the end of {target}";
    }

    /// <summary>The names a class's states usually have, for New state</summary>
    public static IReadOnlyList<string> UsualStates { get; } =
        ["Spawn", "See", "Path", "Chase", "Melee", "Attack", "Missile", "Pain", "Death", "XDeath", "Raise", "Use", "Pickup", "Ready", "Select", "Deselect", "Fire", "Hold"];

    /// <summary>A new state of the class: a copy of the frame shown, looping, or its parent's state of the name</summary>
    [RelayCommand(CanExecute = nameof(HasPicked))]
    private async Task AddStateAsync()
    {
        if (AskText == null || CurrentEdit is not { } edit || Shown is not { } entry)
            return;
        var missing = UsualStates.Where(name => !entry.States.ContainsKey(name) || name == ActorStateResolver.RaiseState && WrittenState(name) == null).Take(8);
        var name = await AskText("New state", $"Its name (Spawn, Chase, Death...; not yet: {string.Join(", ", missing)}):", "",
            candidate => StateNameProblem(candidate, taken: entry.Own.States.ContainsKey(candidate.Trim()) || YamlNodes.Get(edit.Root.Build([]), ["states", candidate.Trim()]) != null));
        if (name == null)
            return;
        name = name.Trim();

        // A parent's state of the name to start from, else the frame shown, else the class's first
        var start = WrittenState(name)?.Spec.Clone();
        if (start == null)
        {
            var frame = SelectedState is { } shown && FrameIndex is int at && WrittenState(shown) is { } written && at < written.Spec.Frames.Count
                ? written.Spec.Frames[at]
                : entry.States.Values.FirstOrDefault() is { } first ? new FrameSpec(first.Sprite, first.FrameLetter, "10") : new FrameSpec("TNT1", "A", "-1");
            start = new StateSpec { Frames = [frame with { Action = null }] };
        }

        var value = (YamlMappingNode)YamlNodes.Clone(edit.Root.Build([]) ?? new YamlMappingNode());
        if (YamlNodes.Get(value, ["states"]) is not YamlMappingNode states)
            value.Children[new YamlScalarNode("states")] = states = new YamlMappingNode();
        states.Children[new YamlScalarNode(name)] = StateFrames.Write(start);
        Apply(edit, value, name, 0);
        Status = $"{name} added";
    }

    /// <summary>What's wrong with a name for a state, or null when it'll do</summary>
    public static string? StateNameProblem(string candidate, bool taken)
    {
        var name = candidate.Trim();
        if (name.Length == 0 || name.Any(c => !(char.IsLetterOrDigit(c) || c is '.' or '_')))
            return "Letters, digits, dots and _ only (Death.Fire is the death by fire)";
        return taken ? $"It has a {name} state of its own already" : null;
    }

    /// <summary>Renames the class's own state picked, and its own states going on to it</summary>
    [RelayCommand(CanExecute = nameof(StateIsOwn))]
    private async Task RenameStateAsync()
    {
        if (AskText == null || CurrentEdit is not { } edit || SelectedState is not { } old || Shown is not { } entry)
            return;
        var name = await AskText($"Rename {old}", "Its new name:", old,
            candidate => candidate.Trim() == old ? "That's its name" : StateNameProblem(candidate, YamlNodes.Get(edit.Root.Build([]), ["states", candidate.Trim()]) != null));
        if (name == null)
            return;
        name = name.Trim();

        var value = (YamlMappingNode)YamlNodes.Clone(edit.Root.Build([])!);
        var states = (YamlMappingNode)YamlNodes.Get(value, ["states"])!;
        var renamed = new YamlMappingNode();
        foreach (var (key, list) in states.Children)
        {
            var spec = StateFrames.Read(list);
            if (spec.End == StateEnd.Next && spec.Next == old)
                spec.Next = name;
            var keyName = (key as YamlScalarNode)?.Value;
            renamed.Children[keyName == old ? new YamlScalarNode(name) : key] = keyName == old || spec.Next == name ? StateFrames.Write(spec) : list;
        }
        value.Children[YamlNodes.FindKey(value, "states")!] = renamed;
        Apply(edit, value, name, FrameIndex);
        Status = $"{old} is {name} now" + (entry.Ancestors.Count > 0 ? "; a parent's state going on to it still says " + old : "");
    }

    /// <summary>Takes the class's own state picked out (a parent's of the name comes back)</summary>
    [RelayCommand(CanExecute = nameof(StateIsOwn))]
    private void DeleteState()
    {
        if (SelectedState is not { } state)
            return;
        ChangeState(state, _ => null, select: FirstState(Shown!) ?? state, frame: 0);
        Status = States.Contains(state) ? $"{state} taken out: its parent's comes back" : $"{state} taken out";
    }
}
