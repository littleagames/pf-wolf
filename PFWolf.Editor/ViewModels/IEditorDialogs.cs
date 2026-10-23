using PFWolf.Editor.Editing;

namespace PFWolf.Editor.ViewModels;

/// <summary>What to do with levels that have changes nobody saved</summary>
public enum UnsavedChoice
{
    Save,
    Discard,
    Cancel,
}

/// <summary>
/// What the import dialog offers: the file's levels, the level's planes to put them on, the
/// level being edited (null for none) and a name for a new one, checked by <see cref="CheckName"/>
/// </summary>
public sealed record ImportSetup(
    string FileName,
    IReadOnlyList<PFWolf.Loaders.WdcMap> Maps,
    IReadOnlyList<PlaneOption> Planes,
    string? CurrentLevel,
    string SuggestedName,
    Func<string, string?> CheckName);

/// <summary>A value Replace can find or put down, as it's listed</summary>
public sealed record ReplaceChoice(ushort Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// What the Replace dialog offers: the values on the level (to find) and the ones to put down
/// (null: any number), starting on <see cref="From"/> and <see cref="To"/>
/// </summary>
public sealed record ReplaceSetup(
    string PlaneName,
    IReadOnlyList<ReplaceChoice> Found,
    IReadOnlyList<ReplaceChoice>? Choices,
    ushort From,
    ushort To,
    bool HasSelection);

/// <summary>What to replace with what, and where</summary>
public sealed record ReplaceRequest(ushort From, ushort To, bool SelectionOnly);

/// <summary>A code form's answers, one number per field; <see cref="PickOnMap"/>: its target tile is to be clicked on the map</summary>
public sealed record CodeAnswer(int[] Values, bool PickOnMap);

/// <summary>
/// What the New mod from data files dialog answers: the folder of data files, the game they're
/// built on, the game's title and id, the mod folder to write, and the mod's own palette file
/// (null: the base game's palette)
/// </summary>
public sealed record RawDataModRequest(string DataFolder, PFWolf.Managers.BaseCandidate Base, string Title, string GameId, string ModFolder,
    string? PaletteFile = null);

/// <summary>Finds the games a folder of data files may be built on, with which file is which (null: by their names, else contents)</summary>
public delegate IReadOnlyList<PFWolf.Managers.BaseCandidate> DetectRawDataBase(string dataFolder, PFWolf.Loaders.DataFileSet? files);

/// <summary>The questions the editor asks, which the window answers with its dialogs</summary>
public interface IEditorDialogs
{
    /// <summary>
    /// Asks for a folder of raw data files, the game they're built on (from <paramref name="detect"/>,
    /// best first), a title and the mod folder; null when cancelled
    /// </summary>
    Task<RawDataModRequest?> AskRawDataMod(string modsFolder, DetectRawDataBase detect);

    /// <summary>Shows a page of text to read (a report), until it's closed</summary>
    Task ShowText(string title, string text);

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

    /// <summary>Asks whether to go ahead with something that can't be taken back; true for <paramref name="yes"/></summary>
    Task<bool> Confirm(string title, string message, string yes);

    /// <summary>A map file to import (.map or .wad), or null when none is picked</summary>
    Task<string?> PickMapFile();

    /// <summary>Asks which level of the file, which planes onto which, and where to; null when cancelled</summary>
    Task<ImportRequest?> AskImport(ImportSetup setup);

    /// <summary>Asks which value to replace with which, in the selection or the level; null when cancelled</summary>
    Task<ReplaceRequest?> AskReplace(ReplaceSetup setup);

    /// <summary>Asks a level code's questions (its value, floor, hint, target); null when cancelled</summary>
    Task<CodeAnswer?> AskCode(CodeForm form);

    /// <summary>Asks for the level's new width and height and what stays in place; true when it's closed with a size that builds</summary>
    Task<bool> AskResize(string levelName, MapSizeViewModel size);

    /// <summary>Shows the level properties dialog; true when it's closed with OK (and the properties build)</summary>
    Task<bool> EditProperties(MapPropertiesViewModel properties);

    /// <summary>Shows the texture and sprite browser beside the editor, or brings it to the front</summary>
    void ShowArtBrowser(ArtBrowserViewModel browser);

    /// <summary>Shows the sound browser beside the editor, or brings it to the front</summary>
    void ShowSoundBrowser(SoundBrowserViewModel browser);

    /// <summary>Shows the demo browser beside the editor, or brings it to the front</summary>
    void ShowDemoBrowser(DemoBrowserViewModel browser);

    /// <summary>Shows the palette browser beside the editor, or brings it to the front</summary>
    void ShowPaletteBrowser(PaletteBrowserViewModel browser);

    /// <summary>Shows the text browser beside the editor, or brings it to the front</summary>
    void ShowTextBrowser(TextBrowserViewModel browser);

    /// <summary>Shows the script browser beside the editor, or brings it to the front</summary>
    void ShowScriptBrowser(ScriptBrowserViewModel browser);

    /// <summary>Shows the definitions window beside the editor, or brings it to the front</summary>
    void ShowDefinitions(DefinitionsViewModel definitions);
}
