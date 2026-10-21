using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using PFWolf.DocGen;

// PFWolf.DocGen --out <dir>: writes <dir>/schema.json, every definition file and its fields, for
// the documentation site (its docs-site/README.md describes the format), and says how many
// fields have no description yet

var outDir = "docs-out";
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--out" && i + 1 < args.Length)
        outDir = args[++i];
    else
    {
        Console.Error.WriteLine($"Unknown argument: {args[i]}");
        Console.Error.WriteLine("Usage: PFWolf.DocGen [--out <dir>]");
        return 1;
    }
}

var engineVersion = typeof(DefinitionFiles).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
    .FirstOrDefault(a => a.Key == "EngineVersion")?.Value ?? "unknown";

var files = new JsonArray();
var undocumented = new List<string>();
foreach (var file in DefinitionFiles.All)
{
    var exporter = new SchemaExporter(file.Id);
    files.Add(new JsonObject
    {
        ["id"] = file.Id,
        ["title"] = file.Title,
        ["summary"] = file.Summary,
        ["modPath"] = file.ModPath,
        ["basePath"] = file.BasePath,
        ["root"] = exporter.Export(file.Schema()),
    });
    undocumented.AddRange(exporter.Undocumented);
}

var schema = new JsonObject
{
    ["format"] = 1,
    ["engineVersion"] = engineVersion,
    ["files"] = files,
};

Directory.CreateDirectory(outDir);
var path = Path.Combine(outDir, "schema.json");
File.WriteAllText(path, schema.ToJsonString(new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
}));

Console.WriteLine($"Wrote {path}: PFWolf {engineVersion}, {files.Count} files");
if (undocumented.Count > 0)
    Console.WriteLine($"{undocumented.Count} fields have no description yet (doc comments on their properties give them one)");
return 0;
