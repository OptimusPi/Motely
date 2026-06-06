using System.Runtime.CompilerServices;

namespace Motely.Filters;

public struct MotelySeedScoreTally : IMotelySeedScores
{
    public const int MAX_TALLY_COUNT = 256;

    public int Score { get; set; }
    public string Seed { get; set; }
    private int _tallyCount;
    private int[] _tallyValues = new int[MAX_TALLY_COUNT];

    public readonly byte[] Tally
    {
        get
        {
            var tally = new byte[_tallyCount];
            for (int i = 0; i < _tallyCount; i++)
                tally[i] = (byte)_tallyValues[i];
            return tally;
        }
    }

    public MotelySeedScoreTally(string seed, int score, Span<int> tallyValues)
    {
        Seed = seed;
        Score = score;
        _tallyCount = 0;
        tallyValues.CopyTo(_tallyValues);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset(string seed, int score = 0)
    {
        Seed = seed;
        Score = score;
        _tallyCount = 0;
        _tallyValues ??= new int[MAX_TALLY_COUNT];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddTally(int value)
    {
        _tallyValues[_tallyCount] = value;
        _tallyCount++;
    }

    public readonly int GetTally(int index)
    {
        if (index < 0 || index >= _tallyCount)
            return 0;
        return _tallyValues[index];
    }

    public readonly int TallyCount
    {
        get { return _tallyCount; }
    }

    public readonly ReadOnlySpan<int> TallyValuesSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _tallyValues.AsSpan(0, _tallyCount);
    }

    public readonly List<int> TallyColumns
    {
        get
        {
            var list = new List<int>(_tallyCount);
            for (int i = 0; i < _tallyCount; i++)
            {
                list.Add(_tallyValues[i]);
            }
            return list;
        }
    }

    /// <summary>
    /// CSV row payload used by interop-safe sinks/events.
    /// Format: seed,score,tally1,tally2,...
    /// </summary>
    public readonly string ToCsvRow()
    {
        if (_tallyCount <= 0)
            return $"{Seed},{Score}";

        var tallies = new string[_tallyCount];
        for (int i = 0; i < _tallyCount; i++)
            tallies[i] = _tallyValues[i]
                .ToString(System.Globalization.CultureInfo.InvariantCulture);

        return $"{Seed},{Score},{string.Join(",", tallies)}";
    }
}

// Per-plan (= per-thread) box for auto-cutoff state. Heap allocation so the scorer's
// inner lambda can capture by reference and update plain fields without Interlocked.
// Each thread owns its own instance — no sharing, no contention. Multi-threaded CLI
// tolerates per-thread divergence in LearnedCutoff (eventually converges as threads
// see high scores) in exchange for zero locking on the hot path.
//
// The cutoff is RATE-GATED: the monotonic-max clamp only engages while the raw match
// rate is high enough to pressure the (expensive, in WASM) scored-result callback. When
// matches are rare there is no interop pressure, so the clamp stays off and every match
// is reported. Engaged is driven off RawMatches (counted BEFORE the clamp), so engaging
// the clamp never lowers the signal that drives it — no oscillation, single threshold.
public sealed class AutoCutoffState
{
    public int LearnedCutoff;
    public long SeedsFiltered;

    // Rate gate (per-thread, no locking).
    public bool Engaged;
    public long RawMatches; // scored candidates seen, counted before the clamp
    public long LastGateRawMatches; // RawMatches at the last gate evaluation
    public long LastGateMs; // elapsed ms at the last gate evaluation
}
