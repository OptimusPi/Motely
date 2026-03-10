using System.Runtime.CompilerServices;

namespace Motely.Filters;

public unsafe struct MotelySeedScoreTally : IMotelySeedScore
{
    public const int MAX_TALLY_COUNT = 1024; // Aligning with MotelyJsonSeedScoreTally which is larger for complicated scans

    public int Score { get; set; }
    
    // We only create this string when absolutely needed by the callback
    public string Seed { get; set; }
    
    private fixed int _tallyValues[MAX_TALLY_COUNT];
    private int _tallyCount;
    
    public byte[] Tally 
    {
        get 
        {
            var result = new byte[_tallyCount];
            for (int i = 0; i < _tallyCount; i++) result[i] = (byte)_tallyValues[i];
            return result;
        }
    }

    public MotelySeedScoreTally(string seed, int score)
    {
        Seed = seed;
        Score = score;
        _tallyCount = 0;
    }

    public void AddTally(int value)
    {
        if (_tallyCount < MAX_TALLY_COUNT)
        {
            _tallyValues[_tallyCount++] = value;
        }
    }

    public int GetTally(int index)
    {
        if (index < 0 || index >= _tallyCount)
            return 0;
        return _tallyValues[index];
    }

    public int TallyCount => _tallyCount;

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
