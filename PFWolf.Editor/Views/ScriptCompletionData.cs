using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using PFWolf.Editor.Data;

namespace PFWolf.Editor.Views;

/// <summary>One name in the script editor's autocomplete list: the name and what it is, with its signature and help beside the list</summary>
public sealed class ScriptCompletionData(CompletionOption option) : ICompletionData
{
    public CompletionOption Option { get; } = option;

    public IImage? Image => null;

    public string Text => Option.Text;

    // The name, with what it is in grey after it
    public object Content => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        Children =
        {
            new TextBlock { Text = Option.Text, FontFamily = new FontFamily("Consolas,Menlo,monospace") },
            new TextBlock { Text = Option.Kind, Opacity = 0.55, FontSize = 11, VerticalAlignment = VerticalAlignment.Center },
        },
    };

    public object Description => new StackPanel
    {
        MaxWidth = 420,
        Spacing = 4,
        Children =
        {
            new TextBlock { Text = Option.Signature, FontFamily = new FontFamily("Consolas,Menlo,monospace"), TextWrapping = TextWrapping.Wrap },
            new TextBlock { Text = Option.Summary, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 },
        },
    };

    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        => textArea.Document.Replace(completionSegment, Text);
}
