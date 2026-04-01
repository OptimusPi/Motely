namespace Motely;

public sealed class MotelySeedExplorer : IMotelySeedExplorer, IMotelySeedRouterDesc, IMotelySeedRouter
{
    private readonly IMotelySearch _search;
    private MotelySingleSearchContext _context;

    public MotelySingleSearchContext GetContext() => _context;

    public MotelySeedExplorer(string seed, MotelyDeck deck = MotelyDeck.Red, MotelyStake stake = MotelyStake.White)
    {
        var settings = new MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(new PassthroughFilterDesc())
            .WithDeck(deck)
            .WithStake(stake)
            .WithListSearch([seed])
            .WithThreadCount(1)
            .WithSeedRouter(this);

        _search = settings.Start();
        _search.AwaitCompletion();
    }

    public void InjectSingleSeedContext(in MotelySingleSearchContext ctx)
    {
        _context = ctx;
    }

    IMotelySeedRouter IMotelySeedRouterDesc.CreateSeedRouter(ref MotelyFilterCreationContext ctx) => this;

    public void Dispose() => _search.Dispose();

    private sealed class PassthroughFilterDesc : IMotelySeedFilterDesc<PassthroughFilterDesc.PassthroughFilter>
    {
        public PassthroughFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new();

        public readonly struct PassthroughFilter : IMotelySeedFilter
        {
            public VectorMask Filter(ref MotelyVectorSearchContext ctx) => VectorMask.AllBitsSet;
        }
    }
}
