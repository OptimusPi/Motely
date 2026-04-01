#nullable enable
using Motely.Analysis;

namespace Motely.BrowserWasm;

/// <summary>
/// JS-facing seed analyzer handle. Hold it alive, call Init* to position to an ante,
/// call Next* to scroll forward. Bootsharp registers this as a live instance.
/// </summary>
public interface IMotelyAnalyzer : IDisposable
{
    void InitShop(int ante);
    MotelyItem NextShopItem();
    void InitVouchers(int ante);
    MotelyVoucher NextVoucher();
    void InitTags(int ante);
    MotelyTag NextTag();
    MotelyBossBlind GetBoss(int ante);
}

public sealed class MotelyAnalyzer(MotelySeedRouterDesc desc) : IMotelyAnalyzer
{
    private readonly MotelySeedRouterDesc _desc = desc;
    private MotelySingleShopItemStream _shopStream;
    private MotelySingleVoucherStream _voucherStream;
    private MotelySingleTagStream _tagStream;

    public void InitShop(int ante) =>
        _shopStream = _desc.Instance().CreateShopItemStream(ante);

    public MotelyItem NextShopItem()
    {
        var ctx = _desc.Instance();
        return ctx.GetNextShopItem(ref _shopStream);
    }

    public void InitVouchers(int ante) =>
        _voucherStream = _desc.Instance().CreateVoucherStream(ante);

    public MotelyVoucher NextVoucher()
    {
        var ctx = _desc.Instance();
        return ctx.GetNextVoucher(ref _voucherStream, ctx.Deck.GetDefaultRunState());
    }

    public void InitTags(int ante) =>
        _tagStream = _desc.Instance().CreateTagStream(ante);

    public MotelyTag NextTag()
    {
        var ctx = _desc.Instance();
        return ctx.GetNextTag(ref _tagStream);
    }

    public MotelyBossBlind GetBoss(int ante)
    {
        var ctx = _desc.Instance();
        var stream = ctx.CreateBossStream();
        var runState = ctx.Deck.GetDefaultRunState();
        return ctx.GetBossForAnte(ref stream, ante, ref runState);
    }

    public void Dispose() => _desc.Dispose();
}
