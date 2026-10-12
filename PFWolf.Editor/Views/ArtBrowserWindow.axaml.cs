using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PFWolf.Editor.Data;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The texture and sprite browser, beside the main window (F6)</summary>
public partial class ArtBrowserWindow : Window
{
    public ArtBrowserWindow()
    {
        InitializeComponent();

        // Enter in the search goes to the pictures found (the text box would take it on the way up)
        Search.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                (Thumbs.ContainerFromItem(Thumbs.SelectedItem ?? new object()) ?? (Control)Thumbs).Focus();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
    }

    private ArtBrowserViewModel ViewModel => (ArtBrowserViewModel)DataContext!;

    // The pixel editors open, by kind and picture name
    private readonly Dictionary<(ArtKind, string), PixelEditorWindow> _pixelEditors = [];

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ArtBrowserViewModel viewModel)
            viewModel.PixelEditRequested += OpenPixelEditor;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        // The pixel editors close with it: one with changes nobody saved has to be dealt with first
        if (_pixelEditors.Values.FirstOrDefault(window => window.DataContext is PixelEditorViewModel { IsDirty: true }) is { } unsaved)
        {
            e.Cancel = true;
            ViewModel.Status = "A pixel editor has changes that aren't saved: save it or close it first";
            unsaved.Activate();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is ArtBrowserViewModel viewModel)
        {
            viewModel.PixelEditRequested -= OpenPixelEditor;
            viewModel.StopPlaying();
        }
    }

    private void OnFrameDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedFrame is { } frame)
            ViewModel.ShowFrame(frame);
    }

    private void OpenPixelEditor(object? sender, PixelEditorViewModel editor)
    {
        // One window a picture: one already open comes to the front, with what's drawn in it
        var key = (editor.Document.Kind, editor.Document.Name);
        if (_pixelEditors.TryGetValue(key, out var open))
        {
            editor.Dispose();
            open.Activate();
            return;
        }

        var window = new PixelEditorWindow { DataContext = editor };
        window.Closed += (_, _) => _pixelEditors.Remove(key);
        _pixelEditors[key] = window;
        window.Show(this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;

        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            Search.Focus();
            Search.SelectAll();
            e.Handled = true;
        }
        else if (e.Key is Key.OemComma or Key.OemPeriod && e.KeyModifiers == KeyModifiers.None && !Search.IsFocused)
        {
            // Turns a rotating sprite (not while typing in the search box)
            ViewModel.TurnCommand.Execute(e.Key == Key.OemComma ? "-1" : "1");
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnUseDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.UseInPaletteCommand.CanExecute(null))
            ViewModel.UseInPaletteCommand.Execute(null);
    }

    private async void CopyName(object? sender, RoutedEventArgs e)
    {
        // The side shown, for a rotating sprite ("GARDA3")
        if (ViewModel.CurrentAssetName is { Length: > 0 } name && Clipboard is { } clipboard)
            await clipboard.SetValueAsync(DataFormat.Text, name);
    }

    private async void ExportPng(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.Preview is not { } bitmap)
            return;

        var name = ViewModel.CurrentAssetName;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {name} as a PNG",
            SuggestedFileName = $"{name}.png",
            DefaultExtension = "png",
            FileTypeChoices = [FilePickerFileTypes.ImagePng],
        });
        if (file == null)
            return;

        await using var stream = await file.OpenWriteAsync();
        bitmap.Save(stream);
    }

    private async void PickImageEditor(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "The paint program to edit pictures in (Aseprite, Paint.NET, GIMP…)",
            FileTypeFilter = [new FilePickerFileType("Programs") { Patterns = ["*.exe"] }],
        });
        if (files is [var file, ..] && file.TryGetLocalPath() is { } path)
        {
            ViewModel.ImageEditor = path;
            ViewModel.Status = $"Edit outside opens pictures in {path}";
        }
    }

    private void UseDefaultImageEditor(object? sender, RoutedEventArgs e)
    {
        ViewModel.ImageEditor = "";
        ViewModel.Status = "Edit outside opens pictures in the program Windows edits PNGs with";
    }

    private static readonly FilePickerFileType PictureFiles = new("Pictures (PNG, BMP)") { Patterns = ["*.png", "*.bmp"] };

    private async void ReplacePicture(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem == null)
            return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"A picture to put in place of {ViewModel.CurrentAssetName}",
            FileTypeFilter = [PictureFiles],
        });
        if (files is not [var file, ..])
            return;

        var data = await ReadAll(file);
        try
        {
            ArtFiles.Check(data);
        }
        catch (Exception error) when (error is InvalidDataException or NotSupportedException or IndexOutOfRangeException)
        {
            ViewModel.Status = $"{file.Name} can't be read: {error.Message}";
            return;
        }
        await ViewModel.ReplaceAsync(file.Name, data);
    }

    private async void AddPictures(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Pictures to add to a mod",
            AllowMultiple = true,
            FileTypeFilter = [PictureFiles],
        });
        if (files.Count == 0)
            return;

        // Each read and checked first: one the game can't read isn't offered
        var pictures = new List<(string Name, byte[] Data, string Size)>();
        var unreadable = new List<string>();
        foreach (var file in files)
        {
            var data = await ReadAll(file);
            try
            {
                var (width, height, _) = ArtFiles.Check(data);
                pictures.Add((Path.GetFileNameWithoutExtension(file.Name).ToUpperInvariant(), data, $"{width} × {height}"));
            }
            catch (Exception error) when (error is InvalidDataException or NotSupportedException or IndexOutOfRangeException)
            {
                unreadable.Add($"{file.Name} ({error.Message})");
            }
        }
        if (pictures.Count == 0)
        {
            ViewModel.Status = $"Nothing to add: {string.Join(", ", unreadable)} can't be read";
            return;
        }

        if (await AskAdd(pictures, unreadable) is { } answer)
            await ViewModel.AddAsync(answer.Kind, answer.Pictures);
    }

    private static async Task<byte[]> ReadAll(IStorageFile file)
    {
        await using var stream = await file.OpenReadAsync();
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        return copy.ToArray();
    }

    private sealed record KindChoice(ArtKind Kind, string Label)
    {
        public override string ToString() => Label;
    }

    // Asks what kind the pictures are and, for one, its name; null when cancelled
    private async Task<(ArtKind Kind, List<(string Name, byte[] Data)> Pictures)?> AskAdd(
        List<(string Name, byte[] Data, string Size)> pictures, List<string> unreadable)
    {
        var startKind = ViewModel.SelectedKind?.Kind ?? ViewModel.CurrentKind ?? ArtKind.Texture;
        var kind = new ComboBox { ItemsSource = Kinds, SelectedItem = Kinds.First(choice => choice.Kind == startKind), MinWidth = 240 };
        var name = new TextBox { Text = pictures[0].Name, IsVisible = pictures.Count == 1 };
        var info = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0.8 };
        var error = new TextBlock { Foreground = Avalonia.Media.Brushes.OrangeRed, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var ok = new Button { Content = "Add", IsDefault = true, Classes = { "accent" } };
        var cancel = new Button { Content = "Cancel", IsCancel = true };

        List<(string Name, byte[] Data)> Named()
            => pictures.Count == 1 ? [((name.Text ?? "").Trim(), pictures[0].Data)] : pictures.Select(p => (p.Name, p.Data)).ToList();

        // What's added, and what it replaces of the game's
        void Describe()
        {
            var picked = ((KindChoice)kind.SelectedItem!).Kind;
            var lines = pictures.Count == 1
                ? [$"{pictures[0].Size}"]
                : pictures.Select(p => $"{p.Name}  {p.Size}").ToList();
            var replaces = Named().Select(p => ViewModel.WhatAddingReplaces(picked, p.Name)).OfType<string>().ToList();
            info.Text = string.Join("\n", lines.Take(12)) + (lines.Count > 12 ? $"\n…and {lines.Count - 12} more" : "")
                        + (replaces.Count > 0 ? "\n\n" + string.Join("\n", replaces.Take(6)) + (replaces.Count > 6 ? $"\n…and {replaces.Count - 6} more" : "") : "")
                        + (unreadable.Count > 0 ? $"\n\nLeft out (can't be read): {string.Join(", ", unreadable)}" : "");
            error.Text = Named().Select(p => ArtFiles.CheckName(p.Name) is { } problem ? $"{p.Name}: {problem}" : null).OfType<string>().FirstOrDefault() ?? "";
        }
        kind.SelectionChanged += (_, _) => Describe();
        name.TextChanged += (_, _) => Describe();
        Describe();

        var dialog = MakeDialog(pictures.Count == 1 ? $"Add {pictures[0].Name}" : $"Add {pictures.Count} pictures",
            new TextBlock { Text = "They go in a mod picked next, named by their file names:", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            kind,
            new TextBlock { Text = "Name", IsVisible = pictures.Count == 1 },
            name,
            info,
            error,
            Buttons(ok, cancel));

        (ArtKind, List<(string, byte[])>)? answer = null;
        ok.Click += (_, _) =>
        {
            Describe();
            if (error.Text is { Length: > 0 })
                return;
            answer = (((KindChoice)kind.SelectedItem!).Kind, Named());
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
        return answer;
    }

    private static readonly KindChoice[] Kinds =
    [
        new(ArtKind.Texture, "Wall texture (textures/)"),
        new(ArtKind.Flat, "Flat (flats/)"),
        new(ArtKind.Sprite, "Sprite (sprites/)"),
        new(ArtKind.Picture, "Picture (graphics/)"),
    ];

    // Asks what to draw: its kind, name and size; then opens it in the pixel editor
    private async void NewPicture(object? sender, RoutedEventArgs e)
    {
        var startKind = ViewModel.SelectedKind?.Kind ?? ViewModel.CurrentKind ?? ArtKind.Texture;
        var kind = new ComboBox { ItemsSource = Kinds, SelectedItem = Kinds.First(choice => choice.Kind == startKind), MinWidth = 240 };
        var name = new TextBox { Text = "NEW" };
        var width = new NumericUpDown { Minimum = 1, Maximum = 1024, Value = 64, FormatString = "0", Width = 130 };
        var height = new NumericUpDown { Minimum = 1, Maximum = 1024, Value = 64, FormatString = "0", Width = 130 };
        var clear = new CheckBox { Content = "See-through to start with", IsChecked = startKind == ArtKind.Sprite };
        var note = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0.7, FontSize = 12 };
        var error = new TextBlock { Foreground = Avalonia.Media.Brushes.OrangeRed, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var ok = new Button { Content = "Draw it", IsDefault = true, Classes = { "accent" } };
        var cancel = new Button { Content = "Cancel", IsCancel = true };

        void KindChanged()
        {
            var picked = ((KindChoice)kind.SelectedItem!).Kind;
            clear.IsEnabled = picked != ArtKind.Texture;
            if (picked == ArtKind.Texture)
                clear.IsChecked = false;
            note.Text = picked switch
            {
                ArtKind.Texture => "Wall textures are 64 wide; taller ones are tall walls, 64 a story.",
                ArtKind.Flat => "Flats are 64 × 64; see-through pixels show the floor or ceiling color.",
                ArtKind.Sprite => "Wolf3D's sprites are 64 × 64, standing on the bottom row.",
                _ => "The screen is 320 × 200.",
            };
        }
        kind.SelectionChanged += (_, _) =>
        {
            KindChanged();
            if (((KindChoice)kind.SelectedItem!).Kind == ArtKind.Picture && width.Value == 64 && height.Value == 64)
                (width.Value, height.Value) = (320, 200);
        };
        KindChanged();

        var size = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        size.Children.AddRange([width, new TextBlock { Text = "×", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, height]);
        var dialog = MakeDialog("New picture", kind, new TextBlock { Text = "Name" }, name, new TextBlock { Text = "Size" }, size, clear, note, error,
            Buttons(ok, cancel));

        bool drawn = false;
        ok.Click += (_, _) =>
        {
            var picked = ((KindChoice)kind.SelectedItem!).Kind;
            if (ViewModel.CheckNewName(picked, (name.Text ?? "").Trim()) is { } problem)
            {
                error.Text = problem;
                return;
            }
            drawn = true;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);

        if (drawn)
            ViewModel.NewPicture(((KindChoice)kind.SelectedItem!).Kind, (name.Text ?? "").Trim(), (int)(width.Value ?? 64), (int)(height.Value ?? 64),
                clear.IsChecked == true);
    }

    private static Window MakeDialog(string title, params Control[] children)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.AddRange(children);
        return new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Content = new Border { Padding = new Avalonia.Thickness(16), Child = panel },
        };
    }

    private static StackPanel Buttons(params Button[] buttons)
    {
        var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        panel.Children.AddRange(buttons);
        return panel;
    }
}
