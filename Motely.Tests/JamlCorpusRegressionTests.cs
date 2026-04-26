using System.IO;
using Motely.Filters;
using Xunit;
using Xunit.Abstractions;

namespace Motely.Tests;

/// <summary>
/// Regression test that loads every committed <c>JamlFilters/*.jaml</c> file (excluding the
/// <c>old/</c> archive) and asserts it parses clean. Tightening the schema in v13 silently
/// invalidated existing user JAML — this test exists so that never happens to anyone again
/// without the build going red FIRST.
///
/// <para>Add new corpus files by simply dropping them in <c>JamlFilters/</c>. The test
/// auto-discovers and verifies them. To deliberately exclude a broken/legacy file, move it
/// to <c>JamlFilters/old/</c>.</para>
/// </summary>
public class JamlCorpusRegressionTests
{
    private readonly ITestOutputHelper _output;

    public JamlCorpusRegressionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static TheoryData<string> CorpusFiles
    {
        get
        {
            var data = new TheoryData<string>();
            var root = LocateCorpusRoot();
            foreach (var file in Directory.EnumerateFiles(root, "*.jaml", SearchOption.TopDirectoryOnly))
            {
                data.Add(Path.GetFileName(file));
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public void EveryCommittedJamlFilter_ParsesClean(string fileName)
    {
        var root = LocateCorpusRoot();
        var path = Path.Combine(root, fileName);
        var jaml = File.ReadAllText(path);

        var ok = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(ok, $"Failed to parse {fileName}: {error}");
        Assert.NotNull(config);
    }

    private static string LocateCorpusRoot()
    {
        // Test runs out of bin/{Configuration}/{TFM} — walk up to the MotelyJAML repo root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "JamlFilters");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate JamlFilters/ corpus directory by walking up from " + AppContext.BaseDirectory);
    }
}
