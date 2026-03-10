using Motely.Filters;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Regression tests for every JAML filter file in the v0-balatro-seed-hosting /data/filters folder.
/// Each filter must: parse without errors, compile into a search plan, and successfully search ≥1 seed.
/// New filters added to the data folder are automatically picked up — no code change needed.
/// </summary>
public class V0FilterRegressionTests
{
    /// <summary>
    /// Absolute path to the v0 filter data folder.
    /// If you move the repo, update this constant.
    /// </summary>
    private const string FilterDataPath = @"x:\v0-balatro-seed-hosting\data\filters";

    public static IEnumerable<object[]> AllFilterFiles()
    {
        if (!Directory.Exists(FilterDataPath))
            yield break;

        foreach (var file in Directory.GetFiles(FilterDataPath, "*.jaml").OrderBy(f => f))
            yield return [Path.GetFileName(file), File.ReadAllText(file)];
    }

    [Theory]
    [MemberData(nameof(AllFilterFiles))]
    public void Filter_ParsesAndRuns(string fileName, string jaml)
    {
        var parsed = JamlConfigLoader.TryLoad(jaml, out var config, out var error);
        Assert.True(parsed, $"[{fileName}] JAML parse failed: {error}");
        Assert.NotNull(config);

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithSequentialSearch()
            .WithBatchCharacterCount(2)
            .WithStartBatchIndex(0)
            .WithEndBatchIndex(1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        search.AwaitCompletion();

        Assert.True(search.IsCompleted,        $"[{fileName}] Search did not complete");
        Assert.True(search.TotalSeedsSearched > 0, $"[{fileName}] No seeds were searched");
    }

    [Theory]
    [MemberData(nameof(AllFilterFiles))]
    public void Filter_HasRequiredMetadata(string fileName, string jaml)
    {
        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"[{fileName}] JAML parse failed: {error}"
        );
        Assert.NotNull(config);

        Assert.False(
            string.IsNullOrWhiteSpace(config!.Name),
            $"[{fileName}] Missing 'name' field"
        );
        Assert.True(
            config.Must.HasAnyClauses,
            $"[{fileName}] 'must' section is empty — every filter needs at least one must clause"
        );
    }

    [Theory]
    [MemberData(nameof(AllFilterFiles))]
    public void Filter_DeckAndStakeAreValid(string fileName, string jaml)
    {
        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"[{fileName}] JAML parse failed: {error}"
        );
        Assert.NotNull(config);

        // Deck must be a known value (not defaulted to an invalid enum)
        Assert.True(
            Enum.IsDefined(config!.Deck),
            $"[{fileName}] Invalid or unrecognised deck: '{config.Deck}'"
        );
        Assert.True(
            Enum.IsDefined(config.Stake),
            $"[{fileName}] Invalid or unrecognised stake: '{config.Stake}'"
        );
    }

    [Fact]
    public void FilterDataFolder_IsNotEmpty()
    {
        Assert.True(
            Directory.Exists(FilterDataPath),
            $"Filter data folder not found: {FilterDataPath}"
        );
        var files = Directory.GetFiles(FilterDataPath, "*.jaml");
        Assert.True(files.Length > 0, "No .jaml files found in data folder");
    }
}
