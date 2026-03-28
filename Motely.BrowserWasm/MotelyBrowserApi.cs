namespace Motely.BrowserWasm;

public sealed class MotelyBrowserApi : IMotelyBrowserApi
{
    public IMotelySingleSearchContext CreateSingleSearchContext(string seed, string deck, string stake) =>
        new MotelySingleSearchContextInterop(
            seed.Trim().Length > 0 ? seed.Trim() : "BALATRO1",
            Enum.Parse<Motely.MotelyDeck>(deck),
            Enum.Parse<Motely.MotelyStake>(stake));

    public string GetVersion() =>
        typeof(MotelyBrowserApi).Assembly.GetName().Version?.ToString() ?? "unknown";
}
