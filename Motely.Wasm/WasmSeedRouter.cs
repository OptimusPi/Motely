using System;
using Motely;
using Motely.Analysis;

namespace Motely.Wasm;

public sealed class WasmSeedRouter : IDisposable
{
    private readonly MotelySeedRouterDesc _inner;

    public WasmSeedRouter(string seed, MotelyDeck deck, MotelyStake stake)
    {
        _inner = new MotelySeedRouterDesc(seed, deck, stake);
    }

    public string GetSeed() => _inner.GetSeed();

    public int GetBossForAnte(int ante) => (int)_inner.GetBossForAnte(ante);

    public void Dispose() => _inner.Dispose();
}
