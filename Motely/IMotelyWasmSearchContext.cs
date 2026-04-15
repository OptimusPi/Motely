namespace Motely;

public record MotelyBossStreamResult(
    MotelyBossBlind Boss,
    MotelySingleBossStream Stream,
    MotelyJsRunState RunState
);

public record MotelyBossChunkResult(
    MotelyBossBlind[] Bosses,
    MotelySingleBossStream Stream,
    MotelyJsRunState RunState
);

public record MotelyTagStreamResult(
    MotelyTag Tag,
    MotelySingleTagStream Stream
);

public record MotelyTagChunkResult(
    MotelyTag[] Tags,
    MotelySingleTagStream Stream
);

public record MotelyBoosterPackStreamResult(
    MotelyBoosterPack Pack,
    MotelySingleBoosterPackStream Stream
);

public record MotelyBoosterPackChunkResult(
    MotelyBoosterPack[] Packs,
    MotelySingleBoosterPackStream Stream
);

public record MotelyShopItemStreamResult(
    MotelyItem Item,
    MotelySingleShopItemStream Stream
);

public record MotelyShopItemChunkResult(
    int[] Items,
    MotelySingleShopItemStream Stream
);

public record MotelyItemPrngStreamResult(
    MotelyItem Item,
    MotelySinglePrngStream Stream
);

public record MotelyIntPrngStreamResult(
    int Value,
    MotelySinglePrngStream Stream
);

public record MotelyIntPrngChunkResult(
    int[] Values,
    MotelySinglePrngStream Stream
);

public record MotelyBoolPrngStreamResult(
    bool Value,
    MotelySinglePrngStream Stream
);

public record MotelyBoolPrngChunkResult(
    bool[] Values,
    MotelySinglePrngStream Stream
);

public record MotelyVoucherChunkResult(
    MotelyVoucher[] Vouchers,
    MotelyJsRunState RunState
);

public record MotelyItemChunkResult(
    int[] Items,
    MotelySinglePrngStream Stream
);

public interface IMotelyWasmSearchContext : IDisposable
{
    MotelySingleBossStream CreateBossStream();
    MotelyBossStreamResult GetNextBossForAnte(
        MotelySingleBossStream stream,
        int ante,
        MotelyJsRunState runState
    );
    MotelyBossChunkResult GetNextBossForAnteChunk(
        MotelySingleBossStream stream,
        int startAnte,
        int count,
        MotelyJsRunState runState
    );

    MotelyVoucherStateResult GetAnteFirstVoucher(int ante, MotelyJsRunState runState);
    MotelyVoucherChunkResult GetAnteFirstVoucherChunk(int startAnte, int count, MotelyJsRunState runState);

    MotelySingleTagStream CreateTagStream(int ante);
    MotelyTagStreamResult GetNextTag(MotelySingleTagStream stream);
    MotelyTagChunkResult GetNextTagChunk(MotelySingleTagStream stream, int count);

    MotelySingleBoosterPackStream CreateBoosterPackStream(int ante);
    MotelyBoosterPackStreamResult GetNextBoosterPack(MotelySingleBoosterPackStream stream);
    MotelyBoosterPackChunkResult GetNextBoosterPackChunk(MotelySingleBoosterPackStream stream, int count);

    MotelySingleShopItemStream CreateShopItemStream(
        int ante,
        MotelyJsRunState runState,
        MotelyShopStreamFlags flags,
        MotelyJokerStreamFlags jokerFlags
    );
    MotelyShopItemStreamResult GetNextShopItem(MotelySingleShopItemStream stream);
    MotelyShopItemChunkResult GetNextShopItemChunk(MotelySingleShopItemStream stream, int count);

    MotelySinglePrngStream CreateMisprintPrngStream();
    MotelyIntPrngStreamResult GetNextMisprintMult(MotelySinglePrngStream stream);
    MotelyIntPrngChunkResult GetNextMisprintMultChunk(MotelySinglePrngStream stream, int count);

    MotelySinglePrngStream CreateLuckyCardMoneyStream();
    MotelyBoolPrngStreamResult GetNextLuckyMoney(MotelySinglePrngStream stream, double baseLuck);
    MotelyBoolPrngChunkResult GetNextLuckyMoneyChunk(MotelySinglePrngStream stream, int count, double baseLuck);

    MotelySinglePrngStream CreateLuckyCardMultStream();
    MotelyBoolPrngStreamResult GetNextLuckyMult(MotelySinglePrngStream stream, double baseLuck);
    MotelyBoolPrngChunkResult GetNextLuckyMultChunk(MotelySinglePrngStream stream, int count, double baseLuck);

    MotelySinglePrngStream CreateErraticDeckPrngStream();
    MotelyItemPrngStreamResult GetNextErraticDeckCard(MotelySinglePrngStream stream);
    MotelyItemChunkResult GetNextErraticDeckCardChunk(MotelySinglePrngStream stream, int count);
}
