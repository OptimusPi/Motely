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
    }

    public string GetSeed()
    {
        var ctx = _inner.Instance();
        return ctx.GetSeed();
    }

    public int GetBossForAnte(int ante)
    {
        var ctx = _inner.Instance();
        var bossStream = ctx.CreateBossStream();
        var voucherState = new MotelyRunState();
        MotelyBossBlind boss = default;
        for (int a = 1; a <= ante; a++)
        {
            boss = ctx.GetBossForAnte(ref bossStream, a, ref voucherState);
        }
        return (int)boss;
    }

    public WasmSingleSearchContext GetContext()
    {
        return new WasmSingleSearchContext(_inner);
    }

    public void Dispose() => _inner.Dispose();
}
