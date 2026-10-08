using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PFWolf.Loaders;

public class YamlDataEntryLoader
{
    public static T Read<T>(Stream stream) where T : new()
        => Deserialize<T>(ReadText(stream));

    /// <summary>
    /// A YAML file's text, without the byte order mark some editors save (YamlDotNet rejects it)
    /// </summary>
    public static string ReadText(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray()).TrimStart('﻿');
    }

    public static T Deserialize<T>(string encoded) where T : new()
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            //.WithDuplicateKeyChecking()
            .IgnoreUnmatchedProperties()
            .WithCaseInsensitivePropertyMatching()
            .WithNodeDeserializer(new StateDataNodeDeserializer(), s => s.OnTop())
            .Build();

        try
        {
            var deserializedValue = deserializer.Deserialize<T>(encoded);
            return deserializedValue;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            // Log full YAML content and exception details for diagnosis
            WarningLog.Write($"YamlDotNet.{ex.GetType().Name}: {ex.Message}");
            WarningLog.Write($"Exception details:");
            WarningLog.Write($"  Start: Line {ex.Start.Line}, Column {ex.Start.Column}");
            WarningLog.Write($"  End: Line {ex.End.Line}, Column {ex.End.Column}");
            WarningLog.Write("\nYAML Content:");
            WarningLog.Write("--- BEGIN YAML ---");

            var lines = encoded.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                var lineNum = i + 1;
                var marker = (lineNum >= ex.Start.Line && lineNum <= ex.End.Line) ? ">>> " : "    ";
                WarningLog.Write($"{marker}{lineNum:D3}: {lines[i]}");
            }

            WarningLog.Write("--- END YAML ---\n");
            throw;
        }
    }
}
