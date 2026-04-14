namespace Motely;

public record MotelyBossStreamResult(
    MotelyBossBlind Boss,
    MotelySingleBossStream Stream,
    MotelyJsRunState RunState
);

public record MotelyTagStreamResult(
    MotelyTag Tag,
    MotelySingleTagStream Stream
);

public record MotelyBoosterPackStreamResult(
    MotelyBoosterPack Pack,
    MotelySingleBoosterPackStream Stream
);

public record MotelyShopItemStreamResult(
    MotelyItem Item,
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

public record MotelyBoolPrngStreamResult(
    bool Value,
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

    MotelyVoucherStateResult GetAnteFirstVoucher(int ante, MotelyJsRunState runState);

    MotelySingleTagStream CreateTagStream(int ante);
    MotelyTagStreamResult GetNextTag(MotelySingleTagStream stream);

    MotelySingleBoosterPackStream CreateBoosterPackStream(int ante);
    MotelyBoosterPackStreamResult GetNextBoosterPack(MotelySingleBoosterPackStream stream);

    MotelySingleShopItemStream CreateShopItemStream(
        int ante,
        MotelyJsRunState runState,
        MotelyShopStreamFlags flags,
        MotelyJokerStreamFlags jokerFlags
    );
    MotelyShopItemStreamResult GetNextShopItem(MotelySingleShopItemStream stream);

    MotelySinglePrngStream CreateMisprintPrngStream();
    MotelyIntPrngStreamResult GetNextMisprintMult(MotelySinglePrngStream stream);

    MotelySinglePrngStream CreateLuckyCardMoneyStream();
    MotelyBoolPrngStreamResult GetNextLuckyMoney(MotelySinglePrngStream stream, double baseLuck);

    MotelySinglePrngStream CreateLuckyCardMultStream();
    MotelyBoolPrngStreamResult GetNextLuckyMult(MotelySinglePrngStream stream, double baseLuck);

    MotelySinglePrngStream CreateErraticDeckPrngStream();
    MotelyItemPrngStreamResult GetNextErraticDeckCard(MotelySinglePrngStream stream);
}
