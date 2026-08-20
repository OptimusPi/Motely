namespace Motely;

public enum MotelySearchInputMode
{
    Sequential,
    SeedList,
    Keyword,
    Random,
    Aesthetic,
}

public sealed record MotelySearchIntent(
    MotelySearchInputMode Mode = MotelySearchInputMode.Sequential,
    string[]? Seeds = null,
    string[]? Keywords = null,
    int? RandomSeedCount = null,
    JamlAesthetic? Aesthetic = null,
    JamlAesthetic[]? Aesthetics = null,
    string? PaddingAlphabet = null,
    MotelyDeck? Deck = null,
    MotelyStake? Stake = null,
    int? ThreadCount = null,
    int? SequentialBatchCharacterCount = null,
    int? ProviderBatchSeedCount = null,
    long? StopAfterMatches = null
)
{
    public IMotelySearchSettings ApplyTo(IMotelySearchSettings settings)
    {
        if (Deck.HasValue)
            settings = settings.WithDeck(Deck.Value);
        if (Stake.HasValue)
            settings = settings.WithStake(Stake.Value);
        if (ThreadCount.HasValue)
            settings = settings.WithThreadCount(ThreadCount.Value);
        if (SequentialBatchCharacterCount.HasValue)
            settings = settings.WithBatchCharacterCount(SequentialBatchCharacterCount.Value);
        if (ProviderBatchSeedCount.HasValue)
            settings = settings.WithProviderBatchSeedCount(ProviderBatchSeedCount.Value);

        var paddingAlphabet = ResolvePaddingAlphabet();

        settings = Mode switch
        {
            MotelySearchInputMode.Sequential => settings.WithSequentialSearch(),
            MotelySearchInputMode.SeedList => settings.WithSeedList(Seeds ?? []),
            MotelySearchInputMode.Keyword => settings.WithKeywordSearch(Keywords ?? [], paddingAlphabet),
            MotelySearchInputMode.Random => settings.WithRandomSearch(RandomSeedCount ?? 0),
            MotelySearchInputMode.Aesthetic => ApplyAestheticSearch(settings, paddingAlphabet),
            _ => throw new InvalidOperationException($"Unknown search input mode '{Mode}'."),
        };

        return StopAfterMatches.HasValue ? settings.StopAfter(StopAfterMatches.Value) : settings;
    }

    private IMotelySearchSettings ApplyAestheticSearch(
        IMotelySearchSettings settings,
        char[]? paddingAlphabet
    )
    {
        if (Aesthetics is { Length: > 0 })
        {
            return settings.WithProviderSearch(
                new MotelySeedListProvider(
                    Aesthetics.SelectMany(aesthetic =>
                        JamlAesthetics.EnumerateSeeds(aesthetic, paddingAlphabet)
                    ),
                    Aesthetics.Sum(aesthetic =>
                        JamlAesthetics.GetSeedCount(aesthetic, paddingAlphabet)
                    )
                )
            );
        }

        return settings.WithAestheticSearch(
            Aesthetic ?? throw new InvalidOperationException("Aesthetic mode requires an aesthetic."),
            paddingAlphabet
        );
    }

    private char[]? ResolvePaddingAlphabet()
    {
        if (string.IsNullOrWhiteSpace(PaddingAlphabet))
            return null;

        var parsed = MotelyGlobals.ParsePaddingChars(PaddingAlphabet);
        return Mode == MotelySearchInputMode.Keyword && parsed is null ? [] : parsed;
    }
}