using Motely.Analysis;

namespace Motely;

public interface IMotelyShopItemPager
{
    int GetNext();
    IReadOnlyList<int> GetNextChunk(int count);
}

public sealed class MotelyShopPager : IMotelyShopItemPager
{
    private readonly MotelySeedRouterDesc _ctx;
    private MotelySingleShopItemStream _stream;

    public MotelyShopPager(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _ctx = new(seed, deck, stake);
        _stream = _ctx.Instance().CreateShopItemStream(ante);
    }

    public int GetNext() => _ctx.Instance().GetNextShopItem(ref _stream).Value;

    public IReadOnlyList<int> GetNextChunk(int count)
    {
        var ctx = _ctx.Instance();
        var result = new int[count];
        for (int i = 0; i < count; i++)
            result[i] = ctx.GetNextShopItem(ref _stream).Value;
        return result;
    }
}

public sealed class MotelyJokerPager : IMotelyShopItemPager
{
    private readonly MotelySeedRouterDesc _ctx;
    private MotelySingleJokerStream _stream;

    public MotelyJokerPager(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _ctx = new(seed, deck, stake);
        _stream = _ctx.Instance().CreateShopJokerStream(ante);
    }

    public int GetNext() => _ctx.Instance().GetNextJoker(ref _stream).Value;

    public IReadOnlyList<int> GetNextChunk(int count)
    {
        var ctx = _ctx.Instance();
        var result = new int[count];
        for (int i = 0; i < count; i++)
            result[i] = ctx.GetNextJoker(ref _stream).Value;
        return result;
    }
}

public sealed class MotelyTarotPager : IMotelyShopItemPager
{
    private readonly MotelySeedRouterDesc _ctx;
    private MotelySingleTarotStream _stream;

    public MotelyTarotPager(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _ctx = new(seed, deck, stake);
        _stream = _ctx.Instance().CreateShopTarotStream(ante);
    }

    public int GetNext() => _ctx.Instance().GetNextTarot(ref _stream).Value;

    public IReadOnlyList<int> GetNextChunk(int count)
    {
        var ctx = _ctx.Instance();
        var result = new int[count];
        for (int i = 0; i < count; i++)
            result[i] = ctx.GetNextTarot(ref _stream).Value;
        return result;
    }
}

public sealed class MotelyPlanetPager : IMotelyShopItemPager
{
    private readonly MotelySeedRouterDesc _ctx;
    private MotelySinglePlanetStream _stream;

    public MotelyPlanetPager(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _ctx = new(seed, deck, stake);
        _stream = _ctx.Instance().CreateShopPlanetStream(ante);
    }

    public int GetNext() => _ctx.Instance().GetNextPlanet(ref _stream).Value;

    public IReadOnlyList<int> GetNextChunk(int count)
    {
        var ctx = _ctx.Instance();
        var result = new int[count];
        for (int i = 0; i < count; i++)
            result[i] = ctx.GetNextPlanet(ref _stream).Value;
        return result;
    }
}

public sealed class MotelySpectralPager : IMotelyShopItemPager
{
    private readonly MotelySeedRouterDesc _ctx;
    private MotelySingleSpectralStream _stream;

    public MotelySpectralPager(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _ctx = new(seed, deck, stake);
        _stream = _ctx.Instance().CreateShopSpectralStream(ante);
    }

    public int GetNext() => _ctx.Instance().GetNextSpectral(ref _stream).Value;

    public IReadOnlyList<int> GetNextChunk(int count)
    {
        var ctx = _ctx.Instance();
        var result = new int[count];
        for (int i = 0; i < count; i++)
            result[i] = ctx.GetNextSpectral(ref _stream).Value;
        return result;
    }
}

public sealed class MotelyLegendaryJokerPager : IMotelyShopItemPager
{
    private readonly MotelySeedRouterDesc _ctx;
    private MotelySingleJokerFixedRarityStream _stream;

    public MotelyLegendaryJokerPager(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _ctx = new(seed, deck, stake);
        _stream = _ctx.Instance().CreateLegendaryJokerStream(ante);
    }

    public int GetNext() => _ctx.Instance().GetNextJoker(ref _stream).Value;

    public IReadOnlyList<int> GetNextChunk(int count)
    {
        var ctx = _ctx.Instance();
        var result = new int[count];
        for (int i = 0; i < count; i++)
            result[i] = ctx.GetNextJoker(ref _stream).Value;
        return result;
    }
}

public sealed class MotelyRareTagJokerPager : IMotelyShopItemPager
{
    private readonly MotelySeedRouterDesc _ctx;
    private MotelySingleJokerFixedRarityStream _stream;

    public MotelyRareTagJokerPager(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _ctx = new(seed, deck, stake);
        _stream = _ctx.Instance().CreateRareTagJokerStream(ante);
    }

    public int GetNext() => _ctx.Instance().GetNextJoker(ref _stream).Value;

    public IReadOnlyList<int> GetNextChunk(int count)
    {
        var ctx = _ctx.Instance();
        var result = new int[count];
        for (int i = 0; i < count; i++)
            result[i] = ctx.GetNextJoker(ref _stream).Value;
        return result;
    }
}

public sealed class MotelyTagPager : IMotelyShopItemPager
{
    private readonly MotelySeedRouterDesc _ctx;
    private MotelySingleTagStream _stream;

    public MotelyTagPager(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _ctx = new(seed, deck, stake);
        _stream = _ctx.Instance().CreateTagStream(ante);
    }

    // Returns raw MotelyTag int — not a MotelyItem packed value.
    public int GetNext() => (int)_ctx.Instance().GetNextTag(ref _stream);

    public IReadOnlyList<int> GetNextChunk(int count)
    {
        var ctx = _ctx.Instance();
        var result = new int[count];
        for (int i = 0; i < count; i++)
            result[i] = (int)ctx.GetNextTag(ref _stream);
        return result;
    }
}

public sealed class MotelyVoucherPager : IMotelyShopItemPager
{
    private readonly MotelySeedRouterDesc _ctx;
    private MotelySingleVoucherStream _stream;

    public MotelyVoucherPager(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        _ctx = new(seed, deck, stake);
        _stream = _ctx.Instance().CreateVoucherStream(ante);
    }

    // Uses an empty run state — returns vouchers as if none have been purchased.
    // Odd-indexed vouchers (those requiring a prerequisite) are skipped by the engine.
    public int GetNext()
    {
        var state = new MotelyRunState();
        return (int)_ctx.Instance().GetNextVoucher(ref _stream, in state);
    }

    public IReadOnlyList<int> GetNextChunk(int count)
    {
        var ctx = _ctx.Instance();
        var result = new int[count];
        for (int i = 0; i < count; i++)
        {
            var state = new MotelyRunState();
            result[i] = (int)ctx.GetNextVoucher(ref _stream, in state);
        }
        return result;
    }
}
