using System.Text.Json.Nodes;
using PFWolf.Definitions;

namespace PFWolf.DocGen;

/// <summary>A file's FieldSchema as schema.json's fields, noting the fields with no description</summary>
internal sealed class SchemaExporter(string fileId)
{
    // The record types being exported, outermost first, so a type inside itself stops there
    private readonly List<Type> _records = [];

    /// <summary>Where each field with no description is ("game-info: default-map.shading.light")</summary>
    public List<string> Undocumented { get; } = [];

    public JsonObject Export(FieldSchema schema) => Export(schema, "");

    private JsonObject Export(FieldSchema schema, string path)
    {
        var description = schema.Description;
        if (description == null && schema.Type == FieldType.Record && path.Length == 0)
            description = DocComments.TypeSummary(schema.ClrType);
        if (description == null && schema.Key.Length > 0)
            Undocumented.Add($"{fileId}: {path}");

        var node = new JsonObject
        {
            ["key"] = schema.Key,
            ["label"] = schema.Label,
            ["type"] = schema.Type.ToString().ToLowerInvariant(),
        };
        if (description != null)
            node["description"] = description;
        if (schema.Default != null)
            node["default"] = schema.Default;
        if (schema.Hint != FieldHint.None)
            node["hint"] = schema.Hint.ToString().ToLowerInvariant();

        switch (schema.Type)
        {
            case FieldType.Choice:
                node["choices"] = new JsonArray(schema.Choices.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray());
                break;

            case FieldType.List when schema.Item is { } item:
                node["item"] = Export(item, path);
                break;

            case FieldType.Dictionary when schema.Item is { } item:
                node["keyType"] = IsInteger(schema.KeyType) ? "integer" : "text";
                node["item"] = Export(item, path.Length == 0 ? "*" : $"{path}.*");
                break;

            case FieldType.Record:
                // A hand-made record (ActorSchema...) is typeof(object), and can't hold itself
                var recursive = schema.ClrType != typeof(object) && _records.Contains(schema.ClrType);
                if (recursive)
                {
                    node["description"] = $"{description ?? ""} Its fields are those of {schema.ClrType.Name} above.".Trim();
                    node["fields"] = new JsonArray();
                    break;
                }

                _records.Add(schema.ClrType);
                node["fields"] = new JsonArray(schema.Fields
                    .Select(field => (JsonNode?)Export(field, path.Length == 0 ? field.Key : $"{path}.{field.Key}"))
                    .ToArray());
                _records.RemoveAt(_records.Count - 1);
                break;
        }
        return node;
    }

    private static bool IsInteger(Type? type)
        => type == typeof(int) || type == typeof(short) || type == typeof(long) || type == typeof(byte)
           || type == typeof(ushort) || type == typeof(uint) || type == typeof(sbyte);
}
