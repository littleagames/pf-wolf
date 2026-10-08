using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using PFWolf.Editor.Data;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The text browser (help screens, end texts, briefings), beside the main window (F2)</summary>
public partial class TextBrowserWindow : Window
{
    public TextBrowserWindow()
    {
        InitializeComponent();

        // Enter in the search goes to the texts found (the text box would take it on the way up)
        Search.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                (List.ContainerFromItem(List.SelectedItem ?? new object()) ?? (Control)List).Focus();
                e.Handled = true;
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // The page follows the caret through the text
        SourceBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.CaretIndexProperty && SourceBox.IsFocused)
                ViewModel.ShowPageOf(SourceBox.CaretIndex);
        };

        Page.PointerPressed += (_, e) =>
        {
            // The picture is drawn at twice its size
            var at = e.GetPosition(Page);
            ViewModel.GoToPoint(at.X * ArticleLayout.ScreenWidth / Page.Bounds.Width, at.Y * ArticleLayout.ScreenHeight / Page.Bounds.Height);
            e.Handled = true;
        };
    }

    private TextBrowserViewModel ViewModel => (TextBrowserViewModel)DataContext!;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is TextBrowserViewModel viewModel)
            viewModel.SelectInSource += (_, spot) => Select(spot.Index, spot.Length);
    }

    // Selects part of the text and scrolls to it, once the box has the text
    private void Select(int index, int length)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var text = SourceBox.Text ?? "";
            index = Math.Clamp(index, 0, text.Length);
            SourceBox.Focus();
            SourceBox.SelectionStart = index;
            // The caret goes to the selection's end with it (setting CaretIndex would undo the selection)
            SourceBox.SelectionEnd = Math.Min(index + length, text.Length);
            SourceBox.ScrollToLine(LineOf(text, index));
        });
    }

    private static int LineOf(string text, int index)
    {
        int line = 0;
        for (int i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
                line++;
        }
        return line;
    }

    private void OnProblemPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (ProblemList.SelectedItem is ArticleProblem problem)
            ViewModel.GoTo(problem);
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
        else if (e.Key == Key.PageUp && !SourceBox.IsFocused)
        {
            if (ViewModel.PreviousPageCommand.CanExecute(null))
                ViewModel.PreviousPageCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.PageDown && !SourceBox.IsFocused)
        {
            if (ViewModel.NextPageCommand.CanExecute(null))
                ViewModel.NextPageCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
