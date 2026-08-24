namespace Motely;

public partial class MotelySingleSearchContext
{
    /// <summary>
    /// Balatro deck shuffle (<c>G.deck:shuffle('nr'..ante)</c>, state_events.lua:344).
    /// </summary>
    /// <param name="advance">
    /// How many earlier draws from this same key were already consumed. Lua's
    /// <c>pseudoseed(key)</c> mutates <c>G.GAME.pseudorandom[key]</c> on every call
    /// (misc_functions.lua:310), so each blind played in an ante takes the *next* value from the
    /// one <c>nr{ante}</c> stream. 0 = first blind played that ante; the default keeps every
    /// existing caller on the pre-advance behaviour.
    /// </param>
    public void Shuffle(string seed, Span<MotelyItem> deck, int advance = 0)
    {
        MotelySinglePrngStream stream = CreatePrngStream(seed);
        for (int i = 0; i < advance; i++)
            GetNextPrngState(ref stream);
        LuaRandom random = GetNextLuaRandom(ref stream);

        for (int i = deck.Length - 1; i > 0; i--)
        {
            int j = random.RandInt(0, i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
    }
}
