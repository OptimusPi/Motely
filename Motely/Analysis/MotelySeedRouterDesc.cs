using Motely.Filters.Native;

namespace Motely.Analysis;

public sealed class MotelySeedRouterDesc : IMotelySeedRouterDesc, IDisposable
{
    private MotelySearchParameters _searchParams;
    private MotelySearchContextParams _contextParams;
    private int _lane;
    private readonly IMotelySearch? _ownedSearch;

    /// <summary>Direct construction — runs a single-seed search internally, keeps it alive.</summary>
    public MotelySeedRouterDesc(string seed, MotelyDeck deck, MotelyStake stake)
    {
        PassthroughFilterDesc filterDesc = new();
        var settings = new MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(filterDesc)
            .WithDeck(deck)
            .WithStake(stake)
            .WithListSearch([seed])
            .WithThreadCount(1)
            .WithSeedRouter(this);
        _ownedSearch = settings.Start();
        _ownedSearch.AwaitCompletion();
    }

    IMotelySeedRouter IMotelySeedRouterDesc.CreateSeedRouter(ref MotelyFilterCreationContext ctx)
        => new ContextCapturingRouter(this);

    private readonly struct ContextCapturingRouter(MotelySeedRouterDesc desc) : IMotelySeedRouter
    {
        public void InjectSingleSeedContext(in MotelySingleSearchContext ctx)
        {
            desc._searchParams = ctx.SearchParameters;
            desc._contextParams = ctx.SearchContextParams;
            desc._lane = ctx.VectorLane;
        }
    }

    /// <summary>WASM-internal: stream cursor and router helpers call this; do not serialize to JS.</summary>
    internal MotelySingleSearchContext Instance() =>
        new(in _searchParams, in _contextParams, _lane);

    public string GetSeed() => Instance().GetSeed();

    public MotelyBossBlind GetBossForAnte(int ante)
    {
        var ctx = Instance();
        var bossStream = ctx.CreateBossStream();
        var runState = new MotelyRunState();
        return ctx.GetBossForAnte(ref bossStream, ante, ref runState);
    }

    public void Dispose() => _ownedSearch?.Dispose();
}