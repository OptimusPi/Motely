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

    /// <summary>
    /// Record a match whose subject is not a <see cref="MotelyItem"/> — vouchers, bosses, and tags
    /// are engine enums, not pool items. The engine passes the enum's raw underlying value in
    /// <paramref name="code"/> (a plain cast, no string work on the hot path); the analyzer casts it
    /// back to the right enum and formats the display name at the boundary. Use -1 for a coded match
    /// with no enum (e.g. a bare "The Soul"). <paramref name="slot"/> carries the roll index (voucher)
    /// or tag draw index (0 = small blind, 1 = big blind); -1 when not applicable.
    /// </summary>
    void RecordValue(MotelyMatchSource source, int ante, int slot, int code, int score);
}
