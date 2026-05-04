using System.Text.Json.Nodes;
using Motely.WasmTools;

namespace Motely.Tests;

public class JamlSchemaSnapshotTests
{
  [Fact]
  public void DefaultOutputPaths_DoNotTargetDeletedLanguageTooling()
  {
    var repoRoot = Path.Combine("repo", "root");
    var paths = MotelyJamlSchemaGenerator.DefaultOutputPaths(repoRoot);

    Assert.Equal(
      new[]
      {
        Path.Combine(repoRoot, "jaml.schema.json"),
        Path.Combine(repoRoot, "motely-wasm", "jaml.schema.json"),
        Path.Combine(repoRoot, "packages", "jaml-language-core", "schema", "jaml.schema.json"),
        Path.Combine(repoRoot, "packages", "jaml-language-support", "schema", "jaml.schema.json"),
      },
      paths
    );
    Assert.DoesNotContain(paths, p => p.Contains(
      Path.Combine("tools", "jaml-language"),
      StringComparison.OrdinalIgnoreCase
    ));
  }

  [Fact]
  public void Schema_PreservesPublicJamlContract()
  {
    var schema = MotelyJamlSchemaGenerator.Generate();

    Assert.Equal("https://www.seedfinder.app/jaml.schema.json", schema["$id"]?.GetValue<string>());
    Assert.Equal("JAML — Jimbo's Ante Markup Language", schema["title"]?.GetValue<string>());

    var properties = Assert.IsType<JsonObject>(schema["properties"]);
    foreach (var section in new[] { "must", "should", "mustNot" })
    {
      var sectionSchema = Assert.IsType<JsonObject>(properties[section]);
      var items = Assert.IsType<JsonObject>(sectionSchema["items"]);
      Assert.Equal("#/$defs/JamlClauseDto", items["$ref"]?.GetValue<string>());
    }
  }
}
