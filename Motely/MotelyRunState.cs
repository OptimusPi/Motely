
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
    
    // Track which pack slots have had their souls consumed (bit per pack slot, 8 bytes for 8 antes)
    public ulong ConsumedSoulPackSlots;

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
        if (!OwnedJokers.Contains(joker))
        {
            OwnedJokers.Append(joker);
        }
    }
    
    public bool CanObtainJoker(MotelyItem joker)
    {
        // Can always obtain if Showman is active
        if (ShowmanActive) return true;
        
        // Otherwise, can't obtain duplicates
        return !OwnedJokers.Contains(joker.Type);
    }
    
    public bool IsSoulPackConsumed(int ante, int packSlot)
    {
        // Each ante gets 8 bits (supports up to 8 pack slots per ante)
        int bitIndex = (ante - 1) * 8 + packSlot;
        if (bitIndex >= 64) return false; // Safety check
        return (ConsumedSoulPackSlots & (1UL << bitIndex)) != 0;
    }
    
    public void MarkSoulPackConsumed(int ante, int packSlot)
    {
        // Each ante gets 8 bits (supports up to 8 pack slots per ante)
        int bitIndex = (ante - 1) * 8 + packSlot;
        if (bitIndex < 64) // Safety check
        {
            ConsumedSoulPackSlots |= (1UL << bitIndex);
        }
    }
}
