using System.IO;
using Motely.Filters;
using Xunit;
using Xunit.Abstractions;

namespace Motely.Tests;

/// <summary>
/// Regression test that loads committed <c>Motely.Tests/GoldenJamlFiles/*.jaml</c> fixtures and asserts it parses clean. Tightening the schema in v13 silently
/// invalidated existing user JAML — this test exists so that never happens to anyone again
/// without the build going red FIRST.
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
        var candidate = GoldenDirectory.ResolveGoldenJamlFiles();
        if (Directory.Exists(candidate)) return candidate;
        throw new DirectoryNotFoundException("Could not locate GoldenJamlFiles fixture directory at " + candidate);
    }
}
