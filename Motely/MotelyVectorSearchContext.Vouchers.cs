
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public struct MotelyVectorVoucherStream(int ante, MotelyVectorResampleStream resampleStream)
{
    public readonly int Ante = ante;
    public MotelyVectorResampleStream ResampleStream = resampleStream;

    public readonly MotelySingleVoucherStream CreateSingleStream(int lane)
    {
        return new(Ante, ResampleStream.CreateSingleStream(lane));
    }
}


ref partial struct MotelyVectorSearchContext
{

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyVectorVoucherStream CreateVoucherStream(int ante, bool isCached = false)
    {
        return new(ante, CreateResampleStream(MotelyPrngKeys.Voucher + ante, isCached));
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public VectorEnum256<MotelyVoucher> GetAnteFirstVoucher(int ante, bool isCached = false)
    {
        MotelyVectorPrngStream prngStream = CreatePrngStream(MotelyPrngKeys.Voucher + ante, isCached);

        VectorEnum256<MotelyVoucher> vouchers = new(GetNextRandomInt(ref prngStream, 0, MotelyEnum<MotelyVoucher>.ValueCount));
        int resampleCount = 0;

        while (true)
        {
            // All of the odd vouchers require a prerequisite
            Vector256<int> prerequisiteRequiredMask = Vector256.Equals(vouchers.HardwareVector & Vector256<int>.One, Vector256<int>.One);

            // Mask of vouchers we need to resample
            Vector256<int> resampleMask = prerequisiteRequiredMask;

            if (Vector256.EqualsAll(resampleMask, Vector256<int>.Zero))
                break;

            prngStream = CreateResamplePrngStream(MotelyPrngKeys.Voucher + ante, resampleCount, isCached);

            Vector256<int> newVouchers = GetNextRandomInt(
                ref prngStream,
                0, MotelyEnum<MotelyVoucher>.ValueCount,
                MotelyVectorUtils.ExtendIntMaskToDouble(resampleMask)
            );

            vouchers = new(Vector256.ConditionalSelect(resampleMask, newVouchers, vouchers.HardwareVector));

            ++resampleCount;
            Debug.Assert(resampleCount < 1000, "Infinite loop detected in voucher resampling");
        }
        return vouchers;
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public VectorEnum256<MotelyVoucher> GetAnteFirstVoucher(int ante, in MotelyVectorRunState voucherState, bool isCached = false)
    {
        MotelyVectorPrngStream prngStream = CreatePrngStream(MotelyPrngKeys.Voucher + ante, isCached);

        VectorEnum256<MotelyVoucher> vouchers = new(GetNextRandomInt(ref prngStream, 0, MotelyEnum<MotelyVoucher>.ValueCount));
        int resampleCount = 0;

        while (true)
        {
            Vector256<int> alreadyUnlockedMask = voucherState.IsVoucherActive(vouchers);

            // All of the odd vouchers require a prerequisite
            Vector256<int> prerequisiteRequiredMask = Vector256.Equals(vouchers.HardwareVector & Vector256<int>.One, Vector256<int>.One);
            VectorEnum256<MotelyVoucher> prerequisiteVouchers = new(vouchers.HardwareVector - Vector256<int>.One);

            Vector256<int> unlockedPrerequisiteMask = voucherState.IsVoucherActive(prerequisiteVouchers);

            Vector256<int> prerequisiteSatisfiedMask = Vector256.ConditionalSelect(prerequisiteRequiredMask, unlockedPrerequisiteMask, Vector256<int>.AllBitsSet);

            // Mask of vouchers we need to resample
            Vector256<int> resampleMask = alreadyUnlockedMask | Vector256.OnesComplement(prerequisiteSatisfiedMask);

            if (Vector256.EqualsAll(resampleMask, Vector256<int>.Zero))
                break;

            prngStream = CreateResamplePrngStream(MotelyPrngKeys.Voucher + ante, resampleCount, isCached);

            Vector256<int> newVouchers = GetNextRandomInt(
                ref prngStream,
                0, MotelyEnum<MotelyVoucher>.ValueCount,
                MotelyVectorUtils.ExtendIntMaskToDouble(resampleMask)
            );

            vouchers = new(Vector256.ConditionalSelect(resampleMask, newVouchers, vouchers.HardwareVector));

            ++resampleCount;
            Debug.Assert(resampleCount < 1000, "Infinite loop detected in voucher resampling");
        }

        return vouchers;
    }

    public VectorEnum256<MotelyVoucher> GetNextVoucher(ref MotelyVectorVoucherStream voucherStream, in MotelyVectorRunState voucherState)
    {
        VectorEnum256<MotelyVoucher> vouchers = new(GetNextRandomInt(ref voucherStream.ResampleStream.InitialPrngStream, 0, MotelyEnum<MotelyVoucher>.ValueCount));
        int resampleCount = 0;

        while (true)
        {
            Vector256<int> alreadyUnlockedMask = voucherState.IsVoucherActive(vouchers);

            // All of the odd vouchers require a prerequisite
            Vector256<int> prerequisiteRequiredMask = Vector256.Equals(vouchers.HardwareVector & Vector256<int>.One, Vector256<int>.One);
            VectorEnum256<MotelyVoucher> prerequisiteVouchers = new(vouchers.HardwareVector - Vector256<int>.One);

            Vector256<int> unlockedPrerequisiteMask = voucherState.IsVoucherActive(prerequisiteVouchers);

            Vector256<int> prerequisiteSatisfiedMask = Vector256.ConditionalSelect(prerequisiteRequiredMask, unlockedPrerequisiteMask, Vector256<int>.AllBitsSet);

            // Mask of vouchers we need to resample
            Vector256<int> resampleMask = alreadyUnlockedMask | Vector256.OnesComplement(prerequisiteSatisfiedMask);

            if (Vector256.EqualsAll(resampleMask, Vector256<int>.Zero))
                break;

            Vector256<int> newVouchers = GetNextRandomInt(
                ref GetResamplePrngStream(ref voucherStream.ResampleStream, MotelyPrngKeys.Voucher + voucherStream.Ante, resampleCount),
                0, MotelyEnum<MotelyVoucher>.ValueCount,
                MotelyVectorUtils.ExtendIntMaskToDouble(resampleMask)
            );

            vouchers = new(Vector256.ConditionalSelect(resampleMask, newVouchers, vouchers.HardwareVector));

            ++resampleCount;
            Debug.Assert(resampleCount < 1000, "Infinite loop detected in voucher resampling");
        }

        return vouchers;
    }
}