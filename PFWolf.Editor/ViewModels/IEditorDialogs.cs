using PFWolf.Editor.Editing;

namespace PFWolf.Editor.ViewModels;

/// <summary>What to do with levels that have changes nobody saved</summary>
public enum UnsavedChoice
{
    Save,
    Discard,
    Cancel,
}

/// <summary>The questions the editor asks, which the window answers with its dialogs</summary>
public interface IEditorDialogs
{
    /// <summary>
    /// A mod to save in: one of <paramref name="loadedMods"/>, or another folder or pk3 picked
    /// now; null when none is picked
    /// </summary>
    Task<string?> PickModToSaveIn(string title, IReadOnlyList<string> loadedMods);

    /// <summary>Asks what kind of mod to make and where; null when cancelled</summary>
    Task<NewModRequest?> AskNewMod(IReadOnlyList<GameChoice> games, string modsFolder);

    /// <summary>
    /// A line of text, or null when cancelled. <paramref name="check"/> says what's wrong with
    /// an answer, or returns null for one that will do.
    /// </summary>
    Task<string?> AskText(string title, string prompt, string initial, Func<string, string?> check);

    Task<UnsavedChoice> AskUnsaved(string message);

    /// <summary>Shows the level properties dialog; true when it's closed with OK (and the properties build)</summary>
    Task<bool> EditProperties(MapPropertiesViewModel properties);

    /// <summary>Shows the texture and sprite browser beside the editor, or brings it to the front</summary>
    void ShowArtBrowser(ArtBrowserViewModel browser);

    /// <summary>Shows the sound browser beside the editor, or brings it to the front</summary>
    void ShowSoundBrowser(SoundBrowserViewModel browser);

    /// <summary>Shows the palette browser beside the editor, or brings it to the front</summary>
    void ShowPaletteBrowser(PaletteBrowserViewModel browser);

    /// <summary>Shows the text browser beside the editor, or brings it to the front</summary>
    void ShowTextBrowser(TextBrowserViewModel browser);
}
