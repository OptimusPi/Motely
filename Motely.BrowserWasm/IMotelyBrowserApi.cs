namespace Motely.BrowserWasm;

public interface IMotelyBrowserApi
{
    IMotelySingleSearchContext CreateSingleSearchContext(string seed, string deck, string stake);

    string GetVersion();
}
