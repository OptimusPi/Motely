using Motely.Analysis;

namespace Motely.BrowserWasm;


public sealed class MotelySingleSearchContextInterop : IRetarded
{
    private readonly MotelySeedRouterDesc _router;
    private MotelySingleShopItemStream _shopStream;
    private bool _hasStream;
    private bool _disposed;

    public MotelySingleSearchContextInterop(string seed, MotelyDeck deck, MotelyStake stake) =>
        _router = new MotelySeedRouterDesc(seed, deck, stake);

    public void BeginShopStream(int ante) // re-implement motely because im retarded/ 
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        MotelySingleSearchContext ctx = _router.CreateContext();
        _shopStream = ctx.CreateShopItemStream(ante);
        _hasStream = true;
    }

    public ShopItemDto GetNextShopItem()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_hasStream)
            throw new InvalidOperationException("BeginShopStream must be called first.");

        MotelySingleSearchContext ctx = _router.CreateContext();
        MotelyItem item = ctx.GetNextShopItem(ref _shopStream);
        return new ShopItemDto
        {
            Id = item.Type.ToString(),
            Name = FormatUtils.FormatItem(item),
            Value = item.Value,
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _router.Dispose();
    }
}
