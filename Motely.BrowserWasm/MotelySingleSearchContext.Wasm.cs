#nullable enable
using Motely;
using Motely.Analysis;

namespace Motely.BrowserWasm;

public interface IMotelySingleSearchContext
{
    IMotelySingleSearchContextImpl Open(string seed, MotelyDeck deck, MotelyStake stake);
}

public sealed class MotelySingleSearchContext : IMotelySingleSearchContext
{
    private MotelySeedRouterDesc? _router;

    public IMotelySingleSearchContextImpl Open(string seed, MotelyDeck deck, MotelyStake stake)
    {
        _router?.Dispose();
        _router = new MotelySeedRouterDesc(seed, deck, stake);
        return new MotelySingleSearchContextImpl(_router);
    }
}
