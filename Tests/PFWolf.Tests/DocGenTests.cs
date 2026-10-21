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
