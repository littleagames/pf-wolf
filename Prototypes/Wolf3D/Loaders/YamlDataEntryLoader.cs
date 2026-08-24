using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Wolf3D.Loaders;

internal class YamlDataEntryLoader
{
    public static T Read<T>(Stream stream) where T : new()
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var rawData = ms.ToArray();
        var encoded = System.Text.Encoding.UTF8.GetString(rawData);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            //.WithDuplicateKeyChecking()
            .IgnoreUnmatchedProperties()
            .WithCaseInsensitivePropertyMatching()
            .Build();

        try
        {
            var deserializedValue = deserializer.Deserialize<T>(encoded);
            return deserializedValue;
        }
        catch (YamlDotNet.Core.SyntaxErrorException ex)
        {
            // Log full YAML content and exception details for diagnosis
            Console.WriteLine($"YamlDotNet.SyntaxErrorException: {ex.Message}");
            Console.WriteLine($"Exception details:");
            Console.WriteLine($"  Start: Line {ex.Start.Line}, Column {ex.Start.Column}");
            Console.WriteLine($"  End: Line {ex.End.Line}, Column {ex.End.Column}");
            Console.WriteLine("\nYAML Content:");
            Console.WriteLine("--- BEGIN YAML ---");

            var lines = encoded.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                var lineNum = i + 1;
                var marker = (lineNum >= ex.Start.Line && lineNum <= ex.End.Line) ? ">>> " : "    ";
                Console.WriteLine($"{marker}{lineNum:D3}: {lines[i]}");
            }

            Console.WriteLine("--- END YAML ---\n");
            throw;
        }
    }
}
