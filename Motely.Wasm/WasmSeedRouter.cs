using System;
using Bootsharp;
using Motely;
using Motely.Analysis;

namespace Motely.Wasm;

public sealed class WasmSeedRouter : IDisposable
{
    private readonly MotelySeedRouterDesc _inner;

    public WasmSeedRouter(string seed, MotelyDeck deck, MotelyStake stake)
    {
        _inner = new MotelySeedRouterDesc(seed, deck, stake);
        return _inner.Instance(); // Force context capture and keep it alive.
    }

    public void Dispose()
    {
        _inner.Dispose();
    }
}
