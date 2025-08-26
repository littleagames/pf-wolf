using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PFWolf.Common.Loaders;

internal class YamlDataEntryLoader
{
    public T Read<T>(Stream stream) where T : new()
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var rawData = ms.ToArray();
        var encoded = System.Text.Encoding.UTF8.GetString(rawData);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .WithCaseInsensitivePropertyMatching()
            .Build();

        var deserializedValue = deserializer.Deserialize<T>(encoded);
        return deserializedValue;
    }
}
