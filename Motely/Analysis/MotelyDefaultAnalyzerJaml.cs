namespace Motely.Analysis;

public static class MotelyDefaultAnalyzerJaml
{
    public const string Jaml = """
        id: analyzer
        name: Default Legacy Analyzer
        description: "Built-in pass-through JAML for the legacy analyzer subset: bosses, vouchers, tags, shop queue, visible shop packs, pack contents, and Erratic deck composition."
        deck: Red
        stake: White
        should:
          - event: LuckyMoney
            label: legacy-analyzer-pass-through
            score: 0
            min: 0
            rolls: [0]
        """;

    public static JamlConfig CreateConfig(MotelyDeck deck, MotelyStake stake)
    {
        if (!JamlConfigLoader.TryLoad(Jaml, out var config, out var error) || config is null)
            throw new InvalidOperationException(
                error ?? "Default analyzer JAML could not be loaded."
            );

        config.Deck = deck;
        config.Stake = stake;
        return config;
    }

    public static MotelyJamlyzerResult AnalyzeSeeds(
        IEnumerable<string> seeds,
        MotelyDeck deck,
        MotelyStake stake
    ) => MotelyJamlyzer.AnalyzeSeeds(CreateConfig(deck, stake), seeds.ToArray());

    public static MotelyJamlyzerResult AnalyzeSeed(
        string seed,
        MotelyDeck deck,
        MotelyStake stake
    ) => MotelyJamlyzer.AnalyzeSeed(CreateConfig(deck, stake), seed);
}
