using Motely.Analysis;

namespace Motely.BrowserWasm;

/// <summary>WASM interop handle: keeps <see cref="Analysis.MotelySeedRouterDesc"/> + shop stream; uses real <see cref="MotelySingleSearchContext"/> inside each call.</summary>
public interface IMotelySingleSearchContext : IDisposable
{
    void BeginShopStream(int ante);

    /// <summary>Same shape as <see cref="SeedAnalysisDto"/> shop queue: <c>id</c> = <see cref="Motely.MotelyItem.Type"/>, <c>name</c> = <see cref="Motely.FormatUtils.FormatItem"/>, <c>value</c> = packed <see cref="Motely.MotelyItem"/> bits for sprite lookup.</summary>
    ShopItemDto GetNextShopItem();
}
