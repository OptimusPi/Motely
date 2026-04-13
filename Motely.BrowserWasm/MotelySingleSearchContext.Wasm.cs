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
    MotelyVoucher GetAnteFirstVoucher(int ante);
    MotelyTag GetNextTag(int ante);
    MotelyBossBlind GetBossForAnte(int ante);
    MotelyItem GetNextShopItem(int ante);
    bool GetNextLuckyMoney(double baseLuck = 1);
    bool GetNextLuckyMult(double baseLuck = 1);
    int GetNextMisprintMult();
}

public sealed class MotelySingleSearchContext : IMotelySingleSearchContext
{
    private MotelySeedRouterDesc? _router;
    private IMotelySingleSearchContextImpl? _impl;

    public void OpenInternal(string seed, MotelyDeck deck, MotelyStake stake)
    {
        _router?.Dispose();
        _router = new MotelySeedRouterDesc(seed, deck, stake);
        _impl = new MotelySingleSearchContextImpl(_router);
    }

    void IMotelySingleSearchContext.Open(string seed, MotelyDeck deck, MotelyStake stake) => OpenInternal(seed, deck, stake);
    string IMotelySingleSearchContext.GetSeed() => _impl?.GetSeed() ?? "";
    double IMotelySingleSearchContext.PseudoHash(string key, bool isCached) => _impl?.PseudoHash(key, isCached) ?? 0;
    MotelyVoucher IMotelySingleSearchContext.GetAnteFirstVoucher(int ante) => _impl?.GetAnteFirstVoucher(ante) ?? default;
    MotelyTag IMotelySingleSearchContext.GetNextTag(int ante) => _impl?.GetNextTag(ante) ?? default;
    MotelyBossBlind IMotelySingleSearchContext.GetBossForAnte(int ante) => _impl?.GetBossForAnte(ante) ?? default;
    MotelyItem IMotelySingleSearchContext.GetNextShopItem(int ante) => _impl?.GetNextShopItem(ante) ?? default;
    bool IMotelySingleSearchContext.GetNextLuckyMoney(double baseLuck) => _impl?.GetNextLuckyMoney(baseLuck) ?? false;
    bool IMotelySingleSearchContext.GetNextLuckyMult(double baseLuck) => _impl?.GetNextLuckyMult(baseLuck) ?? false;
    int IMotelySingleSearchContext.GetNextMisprintMult() => _impl?.GetNextMisprintMult() ?? 0;

    // Internal helpers for Host to call safely
    internal MotelyBossBlind GetBossForAnteInternal(int ante) => _impl?.GetBossForAnte(ante) ?? default;
    internal MotelyVoucher GetAnteFirstVoucherInternal(int ante) => _impl?.GetAnteFirstVoucher(ante) ?? default;
    internal MotelyTag GetNextTagInternal(int ante) => _impl?.GetNextTag(ante) ?? default;
    internal MotelyItem GetNextShopItemInternal(int ante) => _impl?.GetNextShopItem(ante) ?? default;
    internal bool GetNextLuckyMoneyInternal(double baseLuck) => _impl?.GetNextLuckyMoney(baseLuck) ?? false;
    internal bool GetNextLuckyMultInternal(double baseLuck) => _impl?.GetNextLuckyMult(baseLuck) ?? false;
    internal int GetNextMisprintMultInternal() => _impl?.GetNextMisprintMult() ?? 0;
}
