
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
}
