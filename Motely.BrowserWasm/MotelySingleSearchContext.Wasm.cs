#nullable enable
using Motely;
using Motely.Analysis;

namespace Motely.BrowserWasm;

// WASM-safe interface that doesn't expose UnmanagedCallersOnly methods
public interface IMotelySingleSearchContext
{
    void Open(string seed, MotelyDeck deck, MotelyStake stake);
    string GetSeed();
    double PseudoHash(string key, bool isCached);
    string GetAnteFirstVoucher(int ante);
    string GetNextTag(int ante);
    string GetBossForAnte(int ante);
    string GetNextShopItem(int ante);
}

public sealed class MotelySingleSearchContext : IMotelySingleSearchContext
{
    private MotelySeedRouterDesc? _router;
    private IMotelySingleSearchContextImpl? _impl;

    public void Open(string seed, MotelyDeck deck, MotelyStake stake)
    {
        _router?.Dispose();
        _router = new MotelySeedRouterDesc(seed, deck, stake);
        _impl = new MotelySingleSearchContextImpl(_router);
    }

    public string GetSeed() => _impl?.GetSeed() ?? "";
    public double PseudoHash(string key, bool isCached) => _impl?.PseudoHash(key, isCached) ?? 0;
    public string GetAnteFirstVoucher(int ante) => System.Text.Json.JsonSerializer.Serialize(_impl?.GetAnteFirstVoucher(ante));
    public string GetNextTag(int ante) => System.Text.Json.JsonSerializer.Serialize(_impl?.GetNextTag(ante));
    public string GetBossForAnte(int ante) => System.Text.Json.JsonSerializer.Serialize(_impl?.GetBossForAnte(ante));
    public string GetNextShopItem(int ante) => System.Text.Json.JsonSerializer.Serialize(_impl?.GetNextShopItem(ante));
}
