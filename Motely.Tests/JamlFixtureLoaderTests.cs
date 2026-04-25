using System.Text;
using Motely.Filters;

namespace Motely.Tests;

/// <summary>
/// Characterization test: loads every <c>JamlFilters/*.jaml</c> in the repo through
/// <see cref="JamlConfigLoader.TryLoad"/> and snapshots the per-file outcome. Detects
/// regressions during the strict-enum / typed-DTO refactor — any fixture that loads
/// today but breaks tomorrow shows up as a diff.
/// Set <c>UPDATE_JAML_GOLDEN=1</c> to refresh the baseline.
/// </summary>
public class JamlFixtureLoaderTests
{
  [Fact]
  public void AllFixtures_LoadOutcomesMatchGolden()
  {
    var fixturesDir = GoldenDirectory.ResolveRepoSubdir("JamlFilters");
    var files = Directory
      .GetFiles(fixturesDir, "*.jaml", SearchOption.TopDirectoryOnly)
      .Concat(Directory.GetFiles(fixturesDir, "*.JAML", SearchOption.TopDirectoryOnly))
      .Select(p => Path.GetFileName(p))
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
      .ToArray();

    // Progress marker so a stack-overflow / process crash on a single fixture pinpoints
    // which file blew up — written-and-flushed before each TryLoad call.
    var progressPath = Path.Combine(Path.GetTempPath(), "jaml-fixture-progress.txt");
    File.WriteAllText(progressPath, "");

    var report = new StringBuilder();
    foreach (var file in files)
    {
      File.AppendAllText(progressPath, "BEFORE: " + file + Environment.NewLine);
      var path = Path.Combine(fixturesDir, file);
      var jaml = File.ReadAllText(path);
      var ok = JamlConfigLoader.TryLoad(jaml, out _, out var error);
      File.AppendAllText(progressPath, "AFTER:  " + file + Environment.NewLine);
      report.Append(file).Append(": ");
      if (ok)
        report.AppendLine("OK");
      else
        report.Append("FAIL: ").AppendLine(SingleLine(error ?? "(no error message)"));
    }

    var current = report.ToString();
    var goldenPath = Path.Combine(GoldenDirectory.Resolve(), "jaml-fixtures.baseline.txt");

    if (Environment.GetEnvironmentVariable("UPDATE_JAML_GOLDEN") == "1")
    {
      Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
      File.WriteAllText(goldenPath, current);
    }

    Assert.True(
      File.Exists(goldenPath),
      $"Golden file missing at {goldenPath}. Run with UPDATE_JAML_GOLDEN=1 to bootstrap."
    );

    Assert.Equal(Normalize(File.ReadAllText(goldenPath)), Normalize(current));
  }

  private static string SingleLine(string s) =>
    s.Replace("\r", " ").Replace("\n", " ").Trim();

  private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd();
}
