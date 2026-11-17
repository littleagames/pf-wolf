using PFWolf.Entities;

namespace PFWolf.Extensions;

internal static class LanguageExtensions
{
    /// <summary>
    /// Looks up a $NAME text key (anything else is used as-is), then fills in the
    /// {YesButtonName} / {NoButtonName} placeholders with the confirm keys.
    /// </summary>
    internal static string ToLanguageText(this string text, LanguageMetadata? language)
        => Menus.LanguageStrings.Translate(text, language?.TextStrings);
}
