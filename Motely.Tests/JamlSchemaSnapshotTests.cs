using System.Text.Encodings.Web;
using System.Text.Json;
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

  private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd();
}
