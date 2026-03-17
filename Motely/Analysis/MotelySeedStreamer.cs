namespace Motely.Analysis;

/// <summary>
/// Streams PRNG results for a single seed using the cursor pattern.
/// State = double. Pass it back to resume. No burn calls.
/// </summary>
public static class MotelySeedStreamer
{
    // ── Tier 1: Simple event streams (single double cursor) ─────────────

    /// <summary>
    /// Stream Lucky Money results for a seed.
    /// state=null → start fresh (creates initial stream from PseudoHash).
    /// state=savedDouble → resume from cursor position.
    /// Returns results + nextState (pass back to JS for next call).
    /// </summary>
    public static (bool[] Results, double NextState) StreamLuckyMoney(
        string seed,
        MotelyDeck deck,
        MotelyStake stake,
        double? state,
        int take,
        double baseLuck = 1
    )
    {
        bool[]? results = null;
        double nextState = 0;

        RunStream(seed, deck, stake, (ref MotelySingleSearchContext ctx) =>
        {
            var stream = state.HasValue
                ? new MotelySinglePrngStream(state.Value)
                : ctx.CreateLuckyCardMoneyStream();

            results = new bool[take];
            for (int i = 0; i < take; i++)
                results[i] = ctx.GetNextLuckyMoney(ref stream, baseLuck);

            nextState = stream.State;
        });

        return (results!, nextState);
    }

    public static (bool[] Results, double NextState) StreamLuckyMult(
        string seed,
        MotelyDeck deck,
        MotelyStake stake,
        double? state,
        int take,
        double baseLuck = 1
    )
    {
        bool[]? results = null;
        double nextState = 0;

        RunStream(seed, deck, stake, (ref MotelySingleSearchContext ctx) =>
        {
            var stream = state.HasValue
                ? new MotelySinglePrngStream(state.Value)
                : ctx.CreateLuckyCardMultStream();

            results = new bool[take];
            for (int i = 0; i < take; i++)
                results[i] = ctx.GetNextLuckyMult(ref stream, baseLuck);

            nextState = stream.State;
        });

        return (results!, nextState);
    }

    public static (int[] Results, double NextState) StreamMisprint(
        string seed,
        MotelyDeck deck,
        MotelyStake stake,
        double? state,
        int take
    )
    {
        int[]? results = null;
        double nextState = 0;

        RunStream(seed, deck, stake, (ref MotelySingleSearchContext ctx) =>
        {
            var stream = state.HasValue
                ? new MotelySinglePrngStream(state.Value)
                : ctx.CreateMisprintPrngStream();

            results = new int[take];
            for (int i = 0; i < take; i++)
                results[i] = ctx.GetNextMisprintMult(ref stream);

            nextState = stream.State;
        });

        return (results!, nextState);
    }

    public static (bool[] Results, double NextState) StreamCavendish(
        string seed,
        MotelyDeck deck,
        MotelyStake stake,
        double? state,
        int take,
        double baseLuck = 1
    )
    {
        bool[]? results = null;
        double nextState = 0;

        RunStream(seed, deck, stake, (ref MotelySingleSearchContext ctx) =>
        {
            var stream = state.HasValue
                ? new MotelySinglePrngStream(state.Value)
                : ctx.CreateCavendishPrngStream();

            results = new bool[take];
            for (int i = 0; i < take; i++)
                results[i] = ctx.GetNextCavendishExtinct(ref stream, baseLuck);

            nextState = stream.State;
        });

        return (results!, nextState);
    }

    public static (bool[] Results, double NextState) StreamGrosMichel(
        string seed,
        MotelyDeck deck,
        MotelyStake stake,
        double? state,
        int take,
        double baseLuck = 1
    )
    {
        bool[]? results = null;
        double nextState = 0;

        RunStream(seed, deck, stake, (ref MotelySingleSearchContext ctx) =>
        {
            var stream = state.HasValue
                ? new MotelySinglePrngStream(state.Value)
                : ctx.CreateGrosMichelPrngStream();

            results = new bool[take];
            for (int i = 0; i < take; i++)
                results[i] = ctx.GetNextGrosMichelExtinct(ref stream, baseLuck);

            nextState = stream.State;
        });

        return (results!, nextState);
    }

    public static (string[] Results, double NextState) StreamErraticDeck(
        string seed,
        MotelyDeck deck,
        MotelyStake stake,
        double? state,
        int take
    )
    {
        string[]? results = null;
        double nextState = 0;

        RunStream(seed, deck, stake, (ref MotelySingleSearchContext ctx) =>
        {
            var stream = state.HasValue
                ? new MotelySinglePrngStream(state.Value)
                : ctx.CreateErraticDeckPrngStream();

            results = new string[take];
            for (int i = 0; i < take; i++)
                results[i] = FormatUtils.FormatItem(ctx.GetNextErraticDeckCard(ref stream));

            nextState = stream.State;
        });

        return (results!, nextState);
    }

    public static (string[] Results, double NextState) StreamWheelOfFortune(
        string seed,
        MotelyDeck deck,
        MotelyStake stake,
        double? state,
        int take,
        double baseLuck = 1
    )
    {
        string[]? results = null;
        double nextState = 0;

        RunStream(seed, deck, stake, (ref MotelySingleSearchContext ctx) =>
        {
            var stream = state.HasValue
                ? new MotelySinglePrngStream(state.Value)
                : ctx.CreateWheelOfFortuneStream();

            results = new string[take];
            for (int i = 0; i < take; i++)
                results[i] = ctx.GetNextWheelOfFortune(ref stream, baseLuck).ToString();

            nextState = stream.State;
        });

        return (results!, nextState);
    }

    // ── Tier 2: Per-ante streams ────────────────────────────────────────

    public static (string[] Results, double NextState) StreamTags(
        string seed,
        MotelyDeck deck,
        MotelyStake stake,
        int ante,
        double? state,
        int take
    )
    {
        string[]? results = null;
        double nextState = 0;

        RunStream(seed, deck, stake, (ref MotelySingleSearchContext ctx) =>
        {
            var stream = state.HasValue
                ? new MotelySingleTagStream(
                    new MotelySingleResampleStream { InitialPrngStream = new(state.Value) },
                    ante
                  )
                : ctx.CreateTagStream(ante);

            results = new string[take];
            for (int i = 0; i < take; i++)
                results[i] = FormatUtils.FormatTag(ctx.GetNextTag(ref stream));

            nextState = stream.ResampleStream.InitialPrngStream.State;
        });

        return (results!, nextState);
    }

    public static (string[] Results, double NextState, bool GeneratedFirstPack) StreamBoosterPacks(
        string seed,
        MotelyDeck deck,
        MotelyStake stake,
        int ante,
        double? state,
        bool generatedFirstPack,
        int take
    )
    {
        string[]? results = null;
        double nextState = 0;
        bool nextGeneratedFirstPack = false;

        RunStream(seed, deck, stake, (ref MotelySingleSearchContext ctx) =>
        {
            var stream = state.HasValue
                ? new MotelySingleBoosterPackStream(new(state.Value), generatedFirstPack)
                : ctx.CreateBoosterPackStream(ante);

            results = new string[take];
            for (int i = 0; i < take; i++)
                results[i] = FormatUtils.FormatPackName(ctx.GetNextBoosterPack(ref stream));

            nextState = stream.PrngStream.State;
            nextGeneratedFirstPack = stream.GeneratedFirstPack;
        });

        return (results!, nextState, nextGeneratedFirstPack);
    }

    public static (string Result, double NextState) StreamAnteFirstVoucher(
        string seed,
        MotelyDeck deck,
        MotelyStake stake,
        int ante
    )
    {
        string? result = null;
        double nextState = 0;

        RunStream(seed, deck, stake, (ref MotelySingleSearchContext ctx) =>
        {
            // GetAnteFirstVoucher handles its own stream creation — no cursor
            var voucher = ctx.GetAnteFirstVoucher(ante);
            result = FormatUtils.FormatVoucher(voucher);
            // No meaningful cursor here — self-contained per ante
            nextState = -1;
        });

        return (result!, nextState);
    }

    public static (string[] Results, double NextState) StreamVouchers(
        string seed,
        MotelyDeck deck,
        MotelyStake stake,
        int ante,
        int voucherBitfield,
        double? state,
        int take
    )
    {
        string[]? results = null;
        double nextState = 0;

        RunStream(seed, deck, stake, (ref MotelySingleSearchContext ctx) =>
        {
            var stream = state.HasValue
                ? new MotelySingleVoucherStream(
                    ante,
                    new MotelySingleResampleStream { InitialPrngStream = new(state.Value) }
                  )
                : ctx.CreateVoucherStream(ante);

            MotelyRunState runState = new() { VoucherBitfield = voucherBitfield };

            results = new string[take];
            for (int i = 0; i < take; i++)
                results[i] = FormatUtils.FormatVoucher(ctx.GetNextVoucher(ref stream, in runState));

            nextState = stream.ResampleStream.InitialPrngStream.State;
        });

        return (results!, nextState);
    }

    // ── Infrastructure ──────────────────────────────────────────────────

    private static void RunStream(
        string seed,
        MotelyDeck deck,
        MotelyStake stake,
        StreamFilterDesc.StreamCallback callback
    )
    {
        var filterDesc = new StreamFilterDesc(callback);

        var searchSettings = new MotelySearchSettings<StreamFilterDesc.StreamFilter>(filterDesc)
            .WithDeck(deck)
            .WithStake(stake)
            .WithListSearch([seed])
            .WithThreadCount(1);

        using var search = searchSettings.Start();
        search.AwaitCompletion();
    }
}

/// <summary>
/// Minimal filter descriptor for streaming. Same pattern as MotelyAnalyzerFilterDesc
/// but stripped to the bone.
/// </summary>
file sealed class StreamFilterDesc : IMotelySeedFilterDesc<StreamFilterDesc.StreamFilter>
{
    public delegate void StreamCallback(ref MotelySingleSearchContext ctx);

    private readonly StreamCallback _callback;

    public StreamFilterDesc(StreamCallback callback)
    {
        _callback = callback;
    }

    public StreamFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new(this);

    public struct StreamFilter(StreamFilterDesc desc) : IMotelySeedFilter
    {
        private readonly StreamFilterDesc _desc = desc;

        public readonly VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            return ctx.SearchIndividualSeeds(CheckSeed);
        }

        private readonly bool CheckSeed(ref MotelySingleSearchContext ctx)
        {
            _desc._callback(ref ctx);
            return false; // Not searching — just streaming
        }
    }
}
