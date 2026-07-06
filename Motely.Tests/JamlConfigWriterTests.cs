namespace Motely.Tests;

public sealed class JamlConfigWriterTests
{
    [Fact]
    public void ToYaml_RoundTripsEveryCorpusFile()
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
            var relative = Path.GetRelativePath(corpusDir, file);
            var original = JamlConfigLoader.FromYaml(File.ReadAllText(file));

            string written;
            try
            {
                written = JamlConfigLoader.ToYaml(original);
            }
            catch (Exception ex)
            {
                failures.Add($"{relative}: ToYaml threw: {ex.Message}");
                continue;
            }

            if (!JamlConfigLoader.TryLoad(written, out var reloaded, out var error))
            {
                failures.Add($"{relative}: round-tripped YAML failed to reload: {error}\n---\n{written}");
                continue;
            }

            Assert.NotNull(reloaded);
            if (reloaded.Must.Count != original.Must.Count
                || reloaded.Should.Count != original.Should.Count
                || reloaded.MustNot.Count != original.MustNot.Count)
            {
                failures.Add(
                    $"{relative}: clause counts changed on round trip "
                        + $"(must {original.Must.Count}->{reloaded.Must.Count}, "
                        + $"should {original.Should.Count}->{reloaded.Should.Count}, "
                        + $"mustNot {original.MustNot.Count}->{reloaded.MustNot.Count})"
                );
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
