using Motely.Analysis;

namespace Motely.BrowserWasm;

public class MotelyWasmBackend(IMotelyJsUi ui) : IMotelyWasmBackend
{
    public IReadOnlyList<int> GetShopItems(string seed, string deck, string stake, int ante, int offset, int count)
    {
        using var router = new MotelySeedRouterDesc(
            seed,
            Enum.Parse<MotelyDeck>(deck),
            Enum.Parse<MotelyStake>(stake));

        var ctx = router.CreateContext();
        var stream = ctx.CreateShopItemStream(ante);

        for (int i = 0; i < offset; i++)
            ctx.GetNextShopItem(ref stream);

        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = ctx.GetNextShopItem(ref stream).Value;

        return items;
    }

    public string AnalyzeSeed(string seed, string deck, string stake)
    {
        var config = new MotelySeedAnalysisConfig(
            seed,
            Enum.Parse<MotelyDeck>(deck),
            Enum.Parse<MotelyStake>(stake));

        var analysis = MotelySeedAnalyzer.Analyze(config);
        return analysis.ToString();
    }

    public bool ValidateJaml(string jamlContent)
    {
        // TODO: wire to JAML validator
        return true;
    }

    public string ValidateJamlWithError(string jamlContent)
    {
        // TODO: wire to JAML validator
        return "";
    }

    public void StartJamlSearch(string jamlContent, int threadCount)
    {
        // TODO: wire to search pipeline with IMotelyJsUi events
    }

    public void StopSearch()
    {
        // TODO: wire to search cancellation
    }

    public string GetVersion() => typeof(MotelyWasmBackend).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    public bool IsSimdEnabled() => true;
    public int GetProcessorCount() => Environment.ProcessorCount;
}
