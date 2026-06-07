namespace Motely;

/// <summary>
/// Where a scored match was found on a seed's board. Used by the analyzer to map a scoring match
/// back onto a materialized board cell (the "glow"). Core-level so it can ride on
/// <see cref="MotelyRunState"/> without the engine taking a dependency on the JAML filter layer.
/// </summary>
public enum MotelyMatchSource
{
    Shop,
    BoosterPack,
    Tag,
    Voucher,
    Boss,
    SoulJoker,
    TagJoker,
    Consumable,
}

/// <summary>
/// Optional collector hung on <see cref="MotelyRunState.ScoopSink"/>. When present, the JAML scorer
/// reports every concrete match it finds (item + where) so the analyzer can light up the board from
/// the real scoring path — one source of truth, full clause coverage. Null on the hot search path
/// (one null-check per match, no allocation, no behavior change).
/// </summary>
/// <remarks>
/// Record takes only core types plus primitives — no <c>IJamlClause</c> — so this interface stays in
/// the engine layer. The collector tags each match with the should-clause it is currently scoring;
/// the analyzer resolves that index back to a clause label when it builds the snapshot.
/// </remarks>
public interface IMotelyScoopSink
{
    void Record(MotelyMatchSource source, int ante, int slot, int cardIndex, MotelyItem item, int score);
}
