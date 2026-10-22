using System.Text.Json.Nodes;
using PFWolf.DocGen;

namespace PFWolf.Tests;

/// <summary>PFWolf.DocGen's schema.json: every definition file, and the fields the docs site shows</summary>
public class DocGenTests
{
    private static IEnumerable<TestCaseData> Files =>
        DefinitionFiles.All.Select(file => new TestCaseData(file).SetArgDisplayNames(file.Id));

    [TestCaseSource(nameof(Files))]
    public void Every_Definition_File_Exports_What_It_Holds(DefinitionFile file)
    {
        // Arrange
        var exporter = new SchemaExporter(file.Id);

        // Act
        var root = exporter.Export(file.Schema());

        // Assert: named fields, or entries of one kind (colors.yaml's name: color)
        Assert.That(CountFields(root) > 0 || root["item"] is JsonObject, Is.True, "the file documents nothing");
    }

    [Test]
    public void Definition_File_Ids_Are_Unique()
    {
        // Act
        var ids = DefinitionFiles.All.Select(file => file.Id).ToList();

        // Assert
        Assert.That(ids, Is.Unique);
    }

    // Every field of every definition file has a description, so the documentation has one for
    // each: a property added without a doc comment fails here
    [TestCaseSource(nameof(Files))]
    public void Every_Field_Is_Described(DefinitionFile file)
    {
        // Arrange
        var exporter = new SchemaExporter(file.Id);

        // Act
        exporter.Export(file.Schema());

        // Assert
        Assert.That(exporter.Undocumented, Is.Empty, "give these properties doc comments");
    }

    // A page of things that are called or typed shows each one's signature: every entry has one,
    // and only those pages do
    [TestCaseSource(nameof(Files))]
    public void Api_Pages_And_Only_They_Have_Signatures(DefinitionFile file)
    {
        // Act
        var entries = Entries(new SchemaExporter(file.Id).Export(file.Schema())).ToList();

        // Assert
        if (file.Api)
            Assert.That(entries.Where(e => e["signature"] == null).Select(e => (string?)e["key"]), Is.Empty);
        else
            Assert.That(entries.Where(e => e["signature"] != null).Select(e => (string?)e["key"]), Is.Empty);
    }

    // What a nested record's type is (its summary) reaches the reference: as the description of a
    // dictionary's entries, which have none of their own, and beside a field's own as its about
    [Test]
    public void Nested_Records_Carry_Their_Types_Summary()
    {
        // Arrange
        var gameInfo = DefinitionFiles.All.Single(file => file.Id == "game-info");

        // Act
        var root = new SchemaExporter(gameInfo.Id).Export(gameInfo.Schema());
        var defaultMap = Field(root, "default-map");
        var zone = Field(defaultMap, "zones")["item"]!;
        var shading = Field(defaultMap, "shading");

        // Assert
        Assert.That((string?)zone["description"], Does.Contain("plane 5"));
        Assert.That((string?)shading["description"], Does.StartWith("Distance shading"));
        Assert.That((string?)shading["about"], Does.Contain("The sky is never shaded"));
    }

    private static JsonObject Field(JsonObject record, string key) =>
        record["fields"]!.AsArray().OfType<JsonObject>().Single(field => (string?)field["key"] == key);

    // The fields that aren't records themselves (an api page's entries), however deep
    private static IEnumerable<JsonObject> Entries(JsonObject node) =>
        node["fields"] is JsonArray fields
            ? fields.OfType<JsonObject>().SelectMany(f => f["type"]?.GetValue<string>() == "record" ? Entries(f) : [f])
            : [];

    // The fields in a record, and in the records, lists and dictionaries inside it
    private static int CountFields(JsonObject node)
    {
        var count = 0;
        if (node["fields"] is JsonArray fields)
            count += fields.Count + fields.OfType<JsonObject>().Sum(CountFields);
        if (node["item"] is JsonObject item)
            count += CountFields(item);
        return count;
    }
}
