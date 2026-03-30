namespace Motely;

public readonly unsafe ref partial struct MotelySingleSearchContext
{
    #region Misprint

    public MotelySinglePrngStream CreateMisprintPrngStream(bool isCached = false) =>
        CreatePrngStream(MotelyPrngKeys.JokerMisprint, isCached);

    public int GetNextMisprintMult(ref MotelySinglePrngStream misprintStream) =>
        GetNextRandomInt(
            ref misprintStream,
            MotelyGlobals.JokerMisprintMin,
            MotelyGlobals.JokerMisprintMax + 1
        );
    #endregion

    #region Lucky Cards

    public MotelySinglePrngStream CreateLuckyCardMoneyStream(bool isCached = false) =>
        CreatePrngStream(MotelyPrngKeys.CardLuckyMoney, isCached);

    public bool GetNextLuckyMoney(ref MotelySinglePrngStream moneyStream, double baseLuck = 1) =>
        GetNextRandom(ref moneyStream) < baseLuck / MotelyGlobals.EnhancementLuckyMoneyChance;

    public MotelySinglePrngStream CreateLuckyCardMultStream(bool isCached = false) =>
        CreatePrngStream(MotelyPrngKeys.CardLuckyMult, isCached);

    public bool GetNextLuckyMult(ref MotelySinglePrngStream multStream, double baseLuck = 1) =>
        GetNextRandom(ref multStream) < baseLuck / MotelyGlobals.EnhancementLuckyMultChance;

    #endregion

    #region Wheel of Fortune
    public MotelySinglePrngStream CreateWheelOfFortuneStream(bool isCached = false) =>
        CreatePrngStream(MotelyPrngKeys.TarotWheelOfFortune, isCached);

    public MotelyItemEdition GetNextWheelOfFortune(
        ref MotelySinglePrngStream wheelStream,
        double baseLuck = 1
    )
    {
        if (GetNextRandom(ref wheelStream) >= baseLuck / MotelyGlobals.TarrotWheelChance)
            return MotelyItemEdition.None;

        // The game picks which joker to apply the effect to, but we don't implement that
        GetNextPrngState(ref wheelStream);

        double editionPoll = GetNextRandom(ref wheelStream);

        if (editionPoll > 1 - 0.006 * 25)
            return MotelyItemEdition.Polychrome;

        if (editionPoll > 1 - 0.02 * 25)
            return MotelyItemEdition.Holographic;

        return MotelyItemEdition.Foil;
    }

    #endregion

    #region Banannanas
    public MotelySinglePrngStream CreateCavendishPrngStream(bool isCached = false) =>
        CreatePrngStream(MotelyPrngKeys.JokerCavendish, isCached);

    public bool GetNextCavendishExtinct(
        ref MotelySinglePrngStream cavendishStream,
        double baseLuck = 1
    ) => GetNextRandom(ref cavendishStream) < baseLuck / MotelyGlobals.JokerCavendishChance;

    public MotelySinglePrngStream CreateGrosMichelPrngStream(bool isCached = false) =>
        CreatePrngStream(MotelyPrngKeys.JokerGrosMichel, isCached);

    public bool GetNextGrosMichelExtinct(
        ref MotelySinglePrngStream grosMichelStream,
        double baseLuck = 1
    ) => GetNextRandom(ref grosMichelStream) < baseLuck / MotelyGlobals.JokerGrosMichelChance;

    #endregion

    #region Erratic

    public MotelySinglePrngStream CreateErraticDeckPrngStream(bool isCached = false) =>
        CreatePrngStream(MotelyPrngKeys.DeckErratic, isCached);

    public MotelyItem GetNextErraticDeckCard(ref MotelySinglePrngStream erraticDeckStream) =>
        new(GetNextRandomElement(ref erraticDeckStream, MotelyEnum<MotelyPlayingCard>.Values));

    #endregion
}
