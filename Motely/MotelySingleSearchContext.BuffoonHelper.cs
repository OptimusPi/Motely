using System;

namespace Motely;

ref partial struct MotelySingleSearchContext
{
    // Build a buffoon pack's joker contents (single stream per ante). packIndex ignored (legacy).
    public MotelySingleItemSet GetNextBuffoonPackContents(int ante, int /*ignored*/ packIndex, int packCardCount)
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
