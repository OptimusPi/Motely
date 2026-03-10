using System.Runtime.CompilerServices;

namespace Motely.Filters;

public unsafe struct MotelySeedScoreTally : IMotelySeedScore
{
    public const int MAX_TALLY_COUNT = 256;

    public int Score { get; set; }
    public string Seed { get; set; }
    private int _tallyCount;
    private int[] _tallyValues;

    public byte[] Tally => TallyColumns.Select(x => (byte)x).ToArray();

    public MotelySeedScoreTally(string seed, int score)
    {
        Seed = seed;
        Score = score;
        _tallyCount = 0;
        _tallyValues = new int[MAX_TALLY_COUNT];
    }

    public void AddTally(int value)
    {
        _tallyValues[_tallyCount] = value;
        _tallyCount++;
    }

    public int GetTally(int index)
    {
        if (index < 0 || index >= _tallyCount)
            return 0;
        return _tallyValues[index];
    }

    public int TallyCount => _tallyCount;

    public ReadOnlySpan<int> TallyValuesSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new ReadOnlySpan<int>((int*)Unsafe.AsPointer(ref _tallyValues[0]), _tallyCount);
    }

    public List<int> TallyColumns
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

public class SharedScoreState
{
    public int LearnedCutoff;
    public long SeedsFiltered;
    public long StartTime;

    public SharedScoreState()
    {
        StartTime = DateTime.UtcNow.Ticks;
    }
}
