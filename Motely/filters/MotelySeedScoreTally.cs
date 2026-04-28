using System.Runtime.CompilerServices;

namespace Motely.Filters;

public unsafe struct MotelySeedScoreTally : IMotelySeedScores
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
        get
        {
            return _tallyCount;
        }
    }

    public readonly ReadOnlySpan<int> TallyValuesSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return new((int*)Unsafe.AsPointer(ref _tallyValues[0]), _tallyCount);
        }
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
}

// Per-plan (= per-thread) box for auto-cutoff state. Heap allocation so the scorer's
// inner lambda can capture by reference and update plain int fields without Interlocked.
// Each thread owns its own instance — no sharing, no contention. Multi-threaded CLI
// tolerates per-thread divergence in LearnedCutoff (eventually converges as threads
// see high scores) in exchange for zero locking on the hot path.
public sealed class AutoCutoffState
{
    public int LearnedCutoff;
    public long SeedsFiltered;
    public long StartTime;

    public AutoCutoffState()
    {
        StartTime = DateTime.UtcNow.Ticks;
    }
}
