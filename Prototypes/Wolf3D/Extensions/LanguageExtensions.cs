using Wolf3D.Entities;

namespace Wolf3D.Extensions;

internal static class LanguageExtensions
{
    /// <summary>
    /// Looks up a $NAME text key (anything else is used as-is), then fills in the
    /// {YESBUTTONNAME} / {NOBUTTONNAME} placeholders with the confirm keys.
    /// </summary>
    internal static string ToLanguageText(this string text, LanguageMetadata? language)
    {
        if (language != null && text.StartsWith("$") && language.TextStrings.TryGetValue(text, out var result))
            text = result;

        return text
            .Replace("{YESBUTTONNAME}", Program.YESBUTTONNAME)
            .Replace("{NOBUTTONNAME}", Program.NOBUTTONNAME);
    }
}
