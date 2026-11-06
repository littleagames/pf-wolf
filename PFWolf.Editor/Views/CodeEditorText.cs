using Avalonia;
using Avalonia.Data;
using AvaloniaEdit;

namespace PFWolf.Editor.Views;

/// <summary>
/// A bindable text for AvaloniaEdit's <see cref="TextEditor"/> (its own Text isn't a property a binding can use):
/// <c>v:CodeEditorText.Text="{Binding Source}"</c>. The editor's edits go back through the binding, and a new
/// text from the view model (another file picked, Revert) replaces the editor's, scrolled to the top with no undo.
/// The editor has AvaloniaEdit's find panel: Ctrl+F, F3 / Shift+F3, Ctrl+H to replace.
/// </summary>
public static class CodeEditorText
{
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, string?>("Text", typeof(CodeEditorText), defaultBindingMode: BindingMode.TwoWay);

    // Set on an editor once its edits are sent back
    private static readonly AttachedProperty<bool> HookedProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, bool>("Hooked", typeof(CodeEditorText));

    // Set while the editor is given the view model's text, so it isn't sent back as an edit
    private static readonly AttachedProperty<bool> ShowingProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, bool>("Showing", typeof(CodeEditorText));

    static CodeEditorText() => TextProperty.Changed.AddClassHandler<TextEditor>(OnTextChanged);

    public static string? GetText(TextEditor editor) => editor.GetValue(TextProperty);
    public static void SetText(TextEditor editor, string? value) => editor.SetValue(TextProperty, value);

    private static void OnTextChanged(TextEditor editor, AvaloniaPropertyChangedEventArgs e)
    {
        if (!editor.GetValue(HookedProperty))
        {
            editor.SetValue(HookedProperty, true);
            editor.TextChanged += (_, _) =>
            {
                if (!editor.GetValue(ShowingProperty) && editor.GetValue(TextProperty) != editor.Text)
                    editor.SetCurrentValue(TextProperty, editor.Text);
            };
        }

        var text = e.NewValue as string ?? "";
        if (editor.GetValue(ShowingProperty) || editor.Text == text)
            return;
        editor.SetValue(ShowingProperty, true);
        try
        {
            editor.Text = text;
            editor.ScrollToHome();
        }
        finally
        {
            editor.SetValue(ShowingProperty, false);
        }
    }
}
