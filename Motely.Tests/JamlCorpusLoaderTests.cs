using Motely.Filters;
using Motely.Filters.Jaml;
using Xunit;

namespace Motely.Tests;

public sealed class JamlCorpusLoaderTests
{
    [Fact]
    public void JamlFiltersCorpus_LoadsAllJamlFiles()
    {
        var corpusDir = FindCorpusDir();
        var files = Directory
            .GetFiles(corpusDir, "*.jaml", SearchOption.AllDirectories)
            .OrderBy(static f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(files);

        var failures = new List<string>();
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            if (!JamlConfigLoader.TryLoad(content, out var config, out var error))
            {
                failures.Add($"{Path.GetRelativePath(corpusDir, file)}: {error}");
                continue;
            }

            Assert.NotNull(config);
            try
            {
                if (config.HasAnyClauses())
                    _ = JamlSearchBuilder.CreatePlan(config);
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetRelativePath(corpusDir, file)}: plan failed: {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static string FindCorpusDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "JamlFilters");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find JamlFilters from test output directory."
        );
    }
}
