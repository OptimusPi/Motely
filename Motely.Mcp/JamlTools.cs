using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using Motely;
using Motely.Filters;
using Motely.Filters.Jaml;
using Motely.Filters.Jummy;

namespace Motely.Mcp;

/// <summary>
/// JAML tools exposed over MCP. Every tool calls the REAL Motely engine surface
/// (verified against Motely.CLI's run path) — no reimplementation, no fakes.
/// </summary>
[McpServerToolType]
public static class JamlTools
{
    [McpServerTool, Description(
        "Validate a JAML (or JSON) filter against the real Motely loader. Returns 'OK' if valid, "
        + "or the exact loader error. Note: legendaries (Perkeo/Triboulet/Canio/Yorick/Chicot) must "
        + "use 'legendaryJoker:' with arcanaPacks/spectralPacks sources — never shopItems.")]
    public static string JamlValidate(
        [Description("JAML or JSON filter text")] string jaml)
    {
        return JamlConfigLoader.TryLoad(jaml, out _, out var error)
            ? "OK — valid JAML."
            : $"INVALID: {error}";
    }

    [McpServerTool, Description(
        "Run a BOUNDED Balatro seed search on the real Motely SIMD engine and return matching seeds. "
        + "Bounded by maxResults and maxSeconds so it never grinds the full ~2.3T seed space. "
        + "Returns the matching seeds plus real throughput (seeds searched, elapsed, seeds/sec).")]
    public static async Task<string> JamlSearch(
        [Description("JAML or JSON filter text")] string jaml,
        [Description("Stop after this many matching seeds (default 10)")] int maxResults = 10,
        [Description("Stop after this many seconds (default 30, max 300)")] int maxSeconds = 30,
        [Description("Worker threads (0 = processor count)")] int threads = 0)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error) || config is null)
            return $"INVALID JAML: {error}";

        JamlSearchPlan plan;
        try { plan = JamlSearchBuilder.CreatePlan(config, 0); }
        catch (Exception ex) { return $"PLAN ERROR: {ex.Message}"; }

        var seeds = new List<string>(maxResults);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(Math.Clamp(maxSeconds, 1, 300)));

        void Collect(string seed)
        {
            lock (seeds)
            {
                if (seeds.Count < maxResults && seen.Add(seed))
                    seeds.Add(seed);
                if (seeds.Count >= maxResults)
                    cts.Cancel();
            }
        }

        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(threads > 0 ? threads : Environment.ProcessorCount)
            .WithAutoScoreCutoff(true)
            // must-only filters report via seed-match; scored (should-clause) filters via scored result.
            .WithSeedMatchCallback(Collect)
            .WithScoredResultCallback(tally => Collect(tally.Seed));

        using var search = settings.Start(cts.Token);
        try { await search.WaitForCompletionAsync(cts.Token); }
        catch (OperationCanceledException) { /* hit a maxResults/maxSeconds bound — expected */ }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {seeds.Count} seed(s) for '{config.Name ?? "filter"}' ({config.Deck} {config.Stake}):");
        foreach (var s in seeds)
            sb.AppendLine($"  {s}");

        double secs = search.ElapsedMs / 1000.0;
        double speed = secs > 0 ? search.TotalSeedsSearched / secs : 0;
        sb.AppendLine(
            $"Searched {search.TotalSeedsSearched:N0} seeds in {secs:F1}s "
            + $"({speed:N0} seeds/sec). Matched {search.MatchingSeeds:N0} total.");
        if (seeds.Count == 0)
            sb.AppendLine("No matches inside the bound — raise maxSeconds, or the filter is very rare.");
        return sb.ToString();
    }

    [McpServerTool, Description(
        "Parse a one-line JUMMY string (e.g. 'Eternal Blueprint in antes 1 or 2') and report whether "
        + "it maps to a valid JAML clause, or the parse error.")]
    public static string JummyValidate(
        [Description("A single JUMMY line")] string line)
    {
        return JummyLine.TryToClause(line, out _, out var err)
            ? "OK — parses to a valid clause."
            : $"INVALID JUMMY: {err}";
    }
}
