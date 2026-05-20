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
      Assert.Equal("#/$defs/JamlClauseUnion", items["$ref"]?.GetValue<string>());
    }

    var aestheticsSchema = Assert.IsType<JsonObject>(properties["aesthetics"]);
    var aestheticsItems = Assert.IsType<JsonObject>(aestheticsSchema["items"]);
    Assert.Equal("#/$defs/JamlAesthetic", aestheticsItems["$ref"]?.GetValue<string>());

    var defs = Assert.IsType<JsonObject>(schema["$defs"]);
    var eventTypeDef = Assert.IsType<JsonObject>(defs["MotelyEventType"]);
    var eventEnum = Assert.IsType<JsonArray>(eventTypeDef["enum"]);
    Assert.Contains(eventEnum, item => item?.GetValue<string>() == "LuckyMoney");

    var clauseDef = Assert.IsType<JsonObject>(defs["JamlClauseUnion"]);
    var oneOf = Assert.IsType<JsonArray>(clauseDef["oneOf"]);
    Assert.True(oneOf.Count >= 40, "expected one branch per clause discriminator");

    static JsonObject? FindBranch(JsonArray branches, string requiredKey)
    {
      foreach (var item in branches)
      {
        if (item is not JsonObject branch)
          continue;
        if (branch["required"] is not JsonArray required)
          continue;
        if (required.Any(r => r?.GetValue<string>() == requiredKey))
          return branch;
      }
      return null;
    }

    var jokerBranch = FindBranch(oneOf, "joker");
    Assert.NotNull(jokerBranch);
    var jokerProps = Assert.IsType<JsonObject>(jokerBranch["properties"]);
    Assert.False(jokerProps.ContainsKey("uncommonJoker"));
    Assert.False(jokerProps.ContainsKey("event"));
    Assert.Equal("#/$defs/Joker", jokerProps["joker"]?["$ref"]?.GetValue<string>());

    var eventBranch = FindBranch(oneOf, "event");
    Assert.NotNull(eventBranch);
    var eventProps = Assert.IsType<JsonObject>(eventBranch["properties"]);
    var eventSchema = Assert.IsType<JsonObject>(eventProps["event"]);
    Assert.Equal("#/$defs/MotelyEventType", eventSchema["$ref"]?.GetValue<string>());

    var sourcesDef = Assert.IsType<JsonObject>(defs["JamlSources"]);
    var sourceProps = Assert.IsType<JsonObject>(sourcesDef["properties"]);
    var earlyAntesMaxPackSchema = Assert.IsType<JsonObject>(sourceProps["earlyAntesMaxPack"]);
    Assert.Equal("integer", earlyAntesMaxPackSchema["type"]?.GetValue<string>());
  }
}
