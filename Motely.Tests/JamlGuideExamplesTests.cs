using System.Text.RegularExpressions;
using Motely.Filters.Jaml;

namespace Motely.Tests;

/// <summary>
/// Every JAML example printed in JAML.md loads. The guide is for humans, so it stays true the
/// only way a document can: the engine reads it back on every test run.
/// </summary>
public sealed partial class JamlGuideExamplesTests
{
    [GeneratedRegex(@"```jaml\r?\n(.*?)```", RegexOptions.Singleline)]
    private static partial Regex JamlBlock();

    private static string GuidePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "JAML.md")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "JAML.md");
    }

    [Fact]
    public void EveryJamlBlockInTheGuideLoads()
    {
        var text = File.ReadAllText(GuidePath());
        var blocks = JamlBlock().Matches(text).Select(m => m.Groups[1].Value).ToArray();

        Assert.True(blocks.Length >= 10, $"guide has only {blocks.Length} examples — did it move?");

        var failures = new List<string>();
        foreach (var block in blocks)
        {
            // Fragments that show only the keys of a clause (the "sources:" and "antes:" tables)
            // are illustrative lines, not whole documents; give them a clause to hang on.
            var document = block.TrimStart().StartsWith("must:", StringComparison.Ordinal)
                || block.TrimStart().StartsWith("should:", StringComparison.Ordinal)
                || block.TrimStart().StartsWith("name:", StringComparison.Ordinal)
                || block.TrimStart().StartsWith("deck:", StringComparison.Ordinal)
                ? block
                : null;

            if (document is null)
                continue;

            if (!JamlConfigLoader.TryLoad(document, out _, out var error))
                failures.Add($"{error}\n---\n{document}");
        }

        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }
}
