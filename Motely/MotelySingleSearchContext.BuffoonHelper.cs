using System;

namespace Motely;

ref partial struct MotelySingleSearchContext
{
    public MotelySingleItemSet GetNextBuffoonPackContents(int ante, int packCardCount)
    {
        var jokerStream = CreateBuffoonPackJokerStream(ante);
        MotelySingleItemSet set = MotelySingleItemSet.Empty;
        for (int i = 0; i < packCardCount && i < MotelySingleItemSet.MaxLength; i++)
        {
            set.Append(GetNextJoker(ref jokerStream));
        }
        return set;
    }
}
