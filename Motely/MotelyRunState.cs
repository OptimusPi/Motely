
using System.Diagnostics;

namespace Motely;


public ref struct MotelyRunState
{
    static MotelyRunState()
    {
        // Check that we can fit all the voucher state in an int
        if (MotelyEnum<MotelyVoucher>.ValueCount > 32)
            throw new UnreachableException();
    }
    public int VoucherBitfield;
    public bool ShowmanActive;
    public MotelySingleItemSet OwnedJokers;

    public HashSet<MotelyBossBlind> UsedBosses;
    public int LastProcessedBossAnte;

    public void InitializeBossTracking()
    {
        UsedBosses ??= new HashSet<MotelyBossBlind>();
        LastProcessedBossAnte = 0;
    }
    
    public void MarkBossUsed(MotelyBossBlind boss)
    {
        UsedBosses ??= new HashSet<MotelyBossBlind>();
        UsedBosses.Add(boss);
    }
    
    public void IncrementBossAnte()
    {
        LastProcessedBossAnte++;
    }
    
    public void ClearUsedBosses(Predicate<MotelyBossBlind> predicate)
    {
        UsedBosses?.RemoveWhere(predicate);
    }

    public void ActivateVoucher(MotelyVoucher voucher)
    {
        VoucherBitfield |= 1 << (int)voucher;
    }

    public bool IsVoucherActive(MotelyVoucher voucher)
    {
        return (VoucherBitfield & (1 << (int) voucher)) != 0;
    }

    public void ActivateShowman()
    {
        ShowmanActive = true;
    }
    
    public void AddOwnedJoker(MotelyItem joker)
    {
        // Check if we haven't exceeded the max capacity
        if (OwnedJokers.Length < MotelySingleItemSet.MaxLength)
        {
            OwnedJokers.Append(joker);
        }
        // If at max capacity, we just won't track more jokers (edge case)
    }
}
