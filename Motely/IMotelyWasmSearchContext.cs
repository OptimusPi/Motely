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

public record MotelyJokerStreamResult(
    MotelyItem Item,
    MotelySingleJokerStream Stream
);

public record MotelyJokerChunkResult(
    int[] Items,
    MotelySingleJokerStream Stream
);

public record MotelyFixedJokerStreamResult(
    MotelyItem Item,
    MotelySingleJokerFixedRarityStream Stream
);

public record MotelyFixedJokerChunkResult(
    int[] Items,
    MotelySingleJokerFixedRarityStream Stream
);

public record MotelyItemSetResult(
    int[] Items,
    MotelySingleJokerStream Stream
);

public record MotelyTarotStreamResult(
    MotelyItem Item,
    MotelySingleTarotStream Stream
);

public record MotelyTarotChunkResult(
    int[] Items,
    MotelySingleTarotStream Stream
);

public record MotelyTarotPairResult(
    MotelyItem First,
    MotelyItem Second,
    MotelySingleTarotStream Stream
);

public record MotelyBoolTarotStreamResult(
    bool Value,
    MotelySingleTarotStream Stream
);

public record MotelyPlanetStreamResult(
    MotelyItem Item,
    MotelySinglePlanetStream Stream
);

public record MotelyPlanetChunkResult(
    int[] Items,
    MotelySinglePlanetStream Stream
);

public record MotelySpectralStreamResult(
    MotelyItem Item,
    MotelySingleSpectralStream Stream
);

public record MotelySpectralChunkResult(
    int[] Items,
    MotelySingleSpectralStream Stream
);

public record MotelyBoolSpectralStreamResult(
    bool Value,
    MotelySingleSpectralStream Stream
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

    MotelySingleJokerStream CreateShopJokerStream(int ante, MotelyJokerStreamFlags flags);
    MotelyJokerStreamResult GetNextShopJoker(MotelySingleJokerStream stream);
    MotelyJokerChunkResult GetNextShopJokerChunk(MotelySingleJokerStream stream, int count);

    MotelySingleJokerStream CreateBuffoonPackJokerStream(int ante, MotelyJokerStreamFlags flags);
    MotelyJokerStreamResult GetNextBuffoonPackJoker(MotelySingleJokerStream stream);
    MotelyJokerChunkResult GetNextBuffoonPackJokerChunk(MotelySingleJokerStream stream, int count);
    MotelyItemSetResult GetNextBuffoonPackContents(MotelySingleJokerStream stream, MotelyBoosterPackSize size);
    MotelyItemSetResult GetNextBuffoonPackContentsSized(MotelySingleJokerStream stream, int size);

    MotelySingleJokerStream CreateJudgementJokerStream(int ante, MotelyJokerStreamFlags flags);
    MotelyJokerStreamResult GetNextJudgementJoker(MotelySingleJokerStream stream);
    MotelyJokerChunkResult GetNextJudgementJokerChunk(MotelySingleJokerStream stream, int count);

    MotelySingleJokerStream CreateWraithJokerStream(int ante, MotelyJokerStreamFlags flags);
    MotelyJokerStreamResult GetNextWraithJoker(MotelySingleJokerStream stream);
    MotelyJokerChunkResult GetNextWraithJokerChunk(MotelySingleJokerStream stream, int count);

    MotelySingleJokerFixedRarityStream CreateSoulJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags);
    MotelyFixedJokerStreamResult GetNextSoulJoker(MotelySingleJokerFixedRarityStream stream);
    MotelyFixedJokerChunkResult GetNextSoulJokerChunk(MotelySingleJokerFixedRarityStream stream, int count);

    MotelySingleJokerFixedRarityStream CreateRareTagJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags);
    MotelyFixedJokerStreamResult GetNextRareTagJoker(MotelySingleJokerFixedRarityStream stream);
    MotelyFixedJokerChunkResult GetNextRareTagJokerChunk(MotelySingleJokerFixedRarityStream stream, int count);

    MotelySingleJokerFixedRarityStream CreateUncommonTagJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags);
    MotelyFixedJokerStreamResult GetNextUncommonTagJoker(MotelySingleJokerFixedRarityStream stream);
    MotelyFixedJokerChunkResult GetNextUncommonTagJokerChunk(MotelySingleJokerFixedRarityStream stream, int count);

    MotelySingleJokerFixedRarityStream CreateRiffRaffJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags);
    MotelyFixedJokerStreamResult GetNextRiffRaffJoker(MotelySingleJokerFixedRarityStream stream);
    MotelyFixedJokerChunkResult GetNextRiffRaffJokerChunk(MotelySingleJokerFixedRarityStream stream, int count);

    MotelySingleJokerFixedRarityStream CreateCommonShopJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags);
    MotelyFixedJokerStreamResult GetNextCommonShopJoker(MotelySingleJokerFixedRarityStream stream);
    MotelyFixedJokerChunkResult GetNextCommonShopJokerChunk(MotelySingleJokerFixedRarityStream stream, int count);

    MotelySingleJokerFixedRarityStream CreateUncommonShopJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags);
    MotelyFixedJokerStreamResult GetNextUncommonShopJoker(MotelySingleJokerFixedRarityStream stream);
    MotelyFixedJokerChunkResult GetNextUncommonShopJokerChunk(MotelySingleJokerFixedRarityStream stream, int count);

    MotelySingleJokerFixedRarityStream CreateRareShopJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags);
    MotelyFixedJokerStreamResult GetNextRareShopJoker(MotelySingleJokerFixedRarityStream stream);
    MotelyFixedJokerChunkResult GetNextRareShopJokerChunk(MotelySingleJokerFixedRarityStream stream, int count);

    MotelySingleTarotStream CreateArcanaPackTarotStream(int ante, bool soulOnly);
    MotelyBoolTarotStreamResult GetNextArcanaPackHasTheSoul(MotelySingleTarotStream stream, MotelyBoosterPackSize size);
    MotelyTarotChunkResult GetNextArcanaPackContents(MotelySingleTarotStream stream, MotelyBoosterPackSize size);
    MotelyTarotStreamResult GetNextTarot(MotelySingleTarotStream stream);
    MotelyTarotChunkResult GetNextTarotChunk(MotelySingleTarotStream stream, int count);

    MotelySingleTarotStream CreateShopTarotStream(int ante);
    MotelyTarotStreamResult GetNextShopTarot(MotelySingleTarotStream stream);
    MotelyTarotChunkResult GetNextShopTarotChunk(MotelySingleTarotStream stream, int count);

    MotelySingleTarotStream CreateEmperorTarotStream(int ante);
    MotelyTarotPairResult GetNextEmperorTarots(MotelySingleTarotStream stream);

    MotelySingleTarotStream CreatePurpleSealTarotStream(int ante);
    MotelyTarotStreamResult GetNextPurpleSealTarot(MotelySingleTarotStream stream);
    MotelyTarotChunkResult GetNextPurpleSealTarotChunk(MotelySingleTarotStream stream, int count);

    MotelySinglePlanetStream CreateCelestialPackPlanetStream(int ante);
    MotelyPlanetChunkResult GetNextCelestialPackContents(MotelySinglePlanetStream stream, MotelyBoosterPackSize size);
    MotelyPlanetStreamResult GetNextPlanet(MotelySinglePlanetStream stream);
    MotelyPlanetChunkResult GetNextPlanetChunk(MotelySinglePlanetStream stream, int count);

    MotelySinglePlanetStream CreateShopPlanetStream(int ante);
    MotelyPlanetStreamResult GetNextShopPlanet(MotelySinglePlanetStream stream);
    MotelyPlanetChunkResult GetNextShopPlanetChunk(MotelySinglePlanetStream stream, int count);

    MotelySingleSpectralStream CreateSpectralPackSpectralStream(int ante, bool soulOnly);
    MotelyBoolSpectralStreamResult GetNextSpectralPackHasTheSoul(MotelySingleSpectralStream stream, MotelyBoosterPackSize size);
    MotelySpectralChunkResult GetNextSpectralPackContents(MotelySingleSpectralStream stream, MotelyBoosterPackSize size);
    MotelySpectralStreamResult GetNextSpectral(MotelySingleSpectralStream stream);
    MotelySpectralChunkResult GetNextSpectralChunk(MotelySingleSpectralStream stream, int count);

    MotelySingleSpectralStream CreateShopSpectralStream(int ante);
    MotelySpectralStreamResult GetNextShopSpectral(MotelySingleSpectralStream stream);
    MotelySpectralChunkResult GetNextShopSpectralChunk(MotelySingleSpectralStream stream, int count);

    MotelySingleSpectralStream CreateSixthSenseSpectralStream(int ante);
    MotelySpectralStreamResult GetNextSixthSenseSpectral(MotelySingleSpectralStream stream);
    MotelySpectralChunkResult GetNextSixthSenseSpectralChunk(MotelySingleSpectralStream stream, int count);

    MotelySingleSpectralStream CreateSeanceSpectralStream(int ante);
    MotelySpectralStreamResult GetNextSeanceSpectral(MotelySingleSpectralStream stream);
    MotelySpectralChunkResult GetNextSeanceSpectralChunk(MotelySingleSpectralStream stream, int count);

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
