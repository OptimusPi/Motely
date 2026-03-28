namespace Motely.BrowserWasm;

public interface IMotelySingleSearchContext : IDisposable
{
    void BeginShopStream(int ante);
    ShopItemDto GetNextShopItem();
}
