using Motely.Analysis;

namespace Motely.BrowserWasm;

public interface IMotelyJsUi
{
    void NotifySearchProgress(int completed, int total);
    void NotifySearchHit(ShopItemDto hit);
    void NotifySearchComplete(string summary);
}
