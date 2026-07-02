namespace Motely;

// State-threaded (value in, value out) twins of the byref stream APIs, so a host-authored
// Jimmolate predicate (e.g. JS via Bootsharp, where byref cannot cross) can drive the same
// derivations the C# filters do. Each call replays its PRNG stream from the seed's start,
// so hot C# filters should keep using the byref APIs; a predicate probing a few antes per
// seed won't notice.
public partial class MotelySingleSearchContext
{
    /// <summary>A fresh per-seed run state snapshot: no vouchers active, no bosses seen.</summary>
    public MotelyJsRunState NewRunState() => MotelyJsRunState.Default;

    /// <summary>Activate (buy) a voucher on the snapshot, returning the updated state.</summary>
    public MotelyJsRunState ActivateVoucherWithState(MotelyVoucher voucher, MotelyJsRunState state) =>
        state.WithVoucherActive(voucher);

    /// <summary>
    /// The boss for <paramref name="ante"/> given the bosses already seen in
    /// <paramref name="state"/>, threading the updated state back. Equivalent to walking a
    /// boss stream to that ante with the byref API: query antes in ascending order, passing
    /// each returned state into the next call.
    /// </summary>
    public MotelyBossStateResult GetBossForAnteWithState(int ante, MotelyJsRunState state)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ante, 1);

        var runState = state.ToRunState();
        var stream = CreateBossStream();

        // The boss PRNG consumes exactly one draw per ante; burn the prior antes' draws.
        for (int prior = 1; prior < ante; prior++)
            GetNextPrngState(ref stream.PrngStream);

        var boss = GetBossForAnte(ref stream, ante, ref runState);
        return new(boss, MotelyJsRunState.FromRunState(in runState));
    }

    /// <summary>
    /// The first voucher in <paramref name="ante"/>'s shop given the vouchers already active
    /// in <paramref name="state"/> (an active prerequisite unlocks its upgrade in the pool).
    /// The state is returned unchanged — seeing a voucher is not buying it; put it into play
    /// with <see cref="ActivateVoucherWithState"/>.
    /// </summary>
    public MotelyVoucherStateResult GetAnteFirstVoucherWithState(int ante, MotelyJsRunState state)
    {
        var runState = state.ToRunState();
        return new(GetAnteFirstVoucher(ante, in runState), state);
    }
}
