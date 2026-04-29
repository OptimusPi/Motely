using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Motely.WasmTools;

namespace Motely.Tests;

/// <summary>
/// Locks the JSON Schema emitted by <see cref="MotelyJamlSchemaGenerator.Generate"/> against a
/// committed golden so the in-flight DTO refactor surfaces every shape change as a diff.
/// Set environment variable <c>UPDATE_JAML_GOLDEN=1</c> to overwrite the golden after an
/// intentional change, then commit the new golden alongside the code change.
/// </summary>
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
      },
      paths
    );
    Assert.DoesNotContain(paths, p => p.Contains(
      Path.Combine("tools", "jaml-language"),
      StringComparison.OrdinalIgnoreCase
    ));
  }

  [Fact]
  public void Schema_MatchesGolden()
  {
    var current = MotelyJamlSchemaGenerator.Generate().ToJsonString(new JsonSerializerOptions
    {
      WriteIndented = true,
      Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    var goldenPath = Path.Combine(GoldenDirectory.Resolve(), "jaml.schema.baseline.json");

    if (Environment.GetEnvironmentVariable("UPDATE_JAML_GOLDEN") == "1")
    {
      Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
      File.WriteAllText(goldenPath, current);
    }

    Assert.True(
      File.Exists(goldenPath),
      $"Golden file missing at {goldenPath}. Run with UPDATE_JAML_GOLDEN=1 to bootstrap."
    );

    var golden = File.ReadAllText(goldenPath);

    Assert.Equal(Normalize(golden), Normalize(current));
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
      Assert.Equal("#/$defs/JamlCriterion", items["$ref"]?.GetValue<string>());
    }
  }

  private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd();
}
