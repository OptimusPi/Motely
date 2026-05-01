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
    private static readonly string[] LegacyKeys = ["mixedJoker", "soulJoker", "shopSlots"];

    private readonly ITestOutputHelper _output;

    public JamlCorpusRegressionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static TheoryData<string> CanonicalCorpusFiles
    {
        get
        {
            var data = new TheoryData<string>();
            var root = LocateCorpusRoot();
            foreach (var file in Directory.EnumerateFiles(root, "*.jaml", SearchOption.TopDirectoryOnly))
            {
                var jaml = File.ReadAllText(file);
                if (!ContainsLegacyKey(jaml))
                    data.Add(Path.GetFileName(file));
            }
            return data;
        }
    }

    public static TheoryData<string> LegacyCorpusFiles
    {
        get
        {
            var data = new TheoryData<string>();
            var root = LocateCorpusRoot();
            foreach (var file in Directory.EnumerateFiles(root, "*.jaml", SearchOption.TopDirectoryOnly))
            {
                var jaml = File.ReadAllText(file);
                if (ContainsLegacyKey(jaml))
                    data.Add(Path.GetFileName(file));
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CanonicalCorpusFiles))]
    public void EveryCanonicalCommittedJamlFilter_ParsesClean(string fileName)
    {
        var root = LocateCorpusRoot();
        var path = Path.Combine(root, fileName);
        var jaml = File.ReadAllText(path);

        var ok = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(ok, $"Failed to parse {fileName}: {error}");
        Assert.NotNull(config);
    }

    [Fact]
    public void EveryLegacyCommittedJamlFilter_FailsWithLegacyKeyError()
    {
        foreach (var fileName in LegacyCorpusFiles)
        {
            var root = LocateCorpusRoot();
            var path = Path.Combine(root, fileName);
            var jaml = File.ReadAllText(path);

            var ok = JamlConfigLoader.TryLoad(jaml, out _, out var error);

            Assert.False(ok, $"Legacy file unexpectedly parsed: {fileName}");
            Assert.False(string.IsNullOrWhiteSpace(error), $"Expected parse error for {fileName}");
            Assert.Contains("Unknown property", error!, StringComparison.Ordinal);
        }
    }

    private static bool ContainsLegacyKey(string jaml)
    {
        foreach (var key in LegacyKeys)
        {
            if (jaml.Contains($"{key}:", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string LocateCorpusRoot()
    {
        var candidate = GoldenDirectory.ResolveGoldenJamlFiles();
        if (Directory.Exists(candidate)) return candidate;
        throw new DirectoryNotFoundException("Could not locate GoldenJamlFiles fixture directory at " + candidate);
    }
}
