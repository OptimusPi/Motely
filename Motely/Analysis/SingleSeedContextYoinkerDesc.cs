namespace Motely.Analysis;

public sealed class SingleSeedContextYoinkerDesc : IMotelySeedContextProviderDesc, IDisposable
{
    private MotelySearchParameters _searchParams;
    private MotelySearchContextParams _contextParams;
    private int _lane;
    private bool _hasContext;
    private IMotelySearch? _ownedSearch;

    /// <summary>Direct construction — runs a single-seed search internally, keeps it alive.</summary>
    public SingleSeedContextYoinkerDesc(string seed, MotelyDeck deck, MotelyStake stake)
    {
        PassthroughFilterDesc filterDesc = new();
        var settings = new MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(filterDesc)
            .WithDeck(deck)
            .WithStake(stake)
            .WithListSearch([seed])
            .WithThreadCount(1)
            .WithSeedContextProvider(this);
        _ownedSearch = settings.Start();
        _ownedSearch.AwaitCompletion();
    }

    public IMotelySeedContextProvider CreateContextProvider(ref MotelyFilterCreationContext ctx)
        => new YoinkerProvider(this);

    private readonly struct YoinkerProvider(SingleSeedContextYoinkerDesc desc) : IMotelySeedContextProvider
    {
        public void ProvideSeedContext(ref MotelySingleSearchContext ctx)
        {
            if (desc._hasContext) return;
            desc._searchParams = ctx.SearchParameters;
            desc._contextParams = ctx.SearchContextParams;
            desc._lane = ctx.VectorLane;
            desc._hasContext = true;
        }
    }

    public MotelySingleSearchContext CreateContext()
    {
        if (!_hasContext)
            throw new InvalidOperationException("No context yoinked yet.");
        return new MotelySingleSearchContext(in _searchParams, in _contextParams, _lane);
    }

    public void Dispose() => _ownedSearch?.Dispose();
}
