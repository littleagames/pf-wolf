using System.Text.Json;

namespace PFWolf.Editor;

/// <summary>
/// What the editor remembers between runs (%APPDATA%\PFWolf\Editor\editor.json): the game
/// folder, game and mods last opened
/// </summary>
public sealed class EditorSettings
{
    /// <summary>The PFWolf folder: pfwolf.pk3 and the game's data files</summary>
    public string GameFolder { get; set; } = "";

    /// <summary>A game pack id ("wolf3d", "spear"), or empty to pick by the data files, as the game does</summary>
    public string Game { get; set; } = "";

    /// <summary>Mods loaded over pfwolf.pk3, in load order</summary>
    public List<string> Mods { get; set; } = [];

    /// <summary>The skill Play starts on, 1 for the easiest; 0 for the game's default</summary>
    public int PlaySkill { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PFWolf", "Editor", "editor.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static EditorSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<EditorSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A settings file that can't be read starts the editor fresh
        }

        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Not remembering is no reason to stop
        }
    }
}
