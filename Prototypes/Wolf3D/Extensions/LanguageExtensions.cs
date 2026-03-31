using Wolf3D.Entities;

namespace Wolf3D.Extensions;

internal static class LanguageExtensions
{
    internal static string ToLanguageText(this string text, LanguageMetadata? language)
    {
        if (language == null)
            return text;

        if (text.StartsWith("$"))
        {
            if (!language.TextStrings.TryGetValue(text, out var result))
                return text;

            return text.Replace(text, result);
        }

        return text;
    }
}
