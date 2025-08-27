using System.Diagnostics;
using System.Runtime.Intrinsics;

namespace Motely;

public ref struct MotelyVectorRunState
{
    static MotelyVectorRunState()
    {
        // Check that we can fit all the voucher state in an int
        if (MotelyEnum<MotelyVoucher>.ValueCount > 32)
            throw new UnreachableException();
    }

    public Vector256<int> VoucherStateBitfield;
    public Vector256<int> ShowmanActive;

    public void ActivateShowman()
    {
        ShowmanActive |= Vector256.Create(1);
    }

    public void ActivateVoucher(MotelyVoucher voucher)
    {
        VoucherStateBitfield |= Vector256.Create(1 << (int)voucher);
    }
    
    public void ActivateVoucherForMask(MotelyVoucher voucher, VectorMask mask) 
    {
        // Only activate voucher for lanes where mask is true
        // Create a vector with the voucher bit in each lane
        var voucherBit = Vector256.Create(1 << (int)voucher);
        
        // Create mask vector: -1 (all bits set) for true lanes, 0 for false lanes
        Span<int> maskElements = stackalloc int[8];
        for (int i = 0; i < 8; i++)
        {
            maskElements[i] = mask[i] ? -1 : 0;
        }
        var maskVector = Vector256.Create<int>(maskElements);
        
        // AND the voucher bit with the mask to only set it for true lanes
        VoucherStateBitfield |= Vector256.BitwiseAnd(voucherBit, maskVector);
    }

    public void ActivateVoucher(VectorEnum256<MotelyVoucher> voucherVector)
    {
        VoucherStateBitfield |= MotelyVectorUtils.ShiftLeft(Vector256<int>.One, voucherVector.HardwareVector);
    }

    public Vector256<int> IsVoucherActive(MotelyVoucher voucher)
    {
        return Vector256.OnesComplement(Vector256.IsZero(
            VoucherStateBitfield & Vector256.Create(1 << (int)voucher)
        ));

    }

    public Vector256<int> IsVoucherActive(VectorEnum256<MotelyVoucher> voucherVector)
    {
        return Vector256.OnesComplement(Vector256.IsZero(
            VoucherStateBitfield & MotelyVectorUtils.ShiftLeft(Vector256<int>.One, voucherVector.HardwareVector)
        ));
    }
}
