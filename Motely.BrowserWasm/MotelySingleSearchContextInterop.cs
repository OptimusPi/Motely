using Motely;
using Motely.Analysis;

namespace Motely.BrowserWasm;

/// <summary>Browser host only — not a second engine in Motely.dll.</summary>
public sealed class MotelySingleSearchContextInterop : IMotelySingleSearchContext
{
    private readonly MotelySeedRouterDesc _router;
    private MotelySingleShopItemStream _stream;
    private bool _hasStream;

    public MotelySingleSearchContextInterop(string seed, MotelyDeck deck, MotelyStake stake) =>
        _router = new MotelySeedRouterDesc(seed, deck, stake);

    public void BeginShopStream(int ante)
    {
        var ctx = _router.CreateContext();
        _stream = ctx.CreateShopItemStream(ante);
        _hasStream = true;
    }

    public ShopItemDto GetNextShopItem()
    {
        if (!_hasStream)
            throw new InvalidOperationException("Call BeginShopStream before GetNextShopItem.");
        var ctx = _router.CreateContext();
        var item = ctx.GetNextShopItem(ref _stream);
        return new ShopItemDto
        {
            Id = item.Type.ToString(),
            Name = FormatUtils.FormatItem(item),
            Value = item.Value,
        };
    }

    public void Dispose() => _router.Dispose();
}
