using System.Runtime.CompilerServices;

namespace Motely;

ref partial struct MotelySeedContext
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelySingleVoucherStream CreateVoucherStream(int ante, bool isCached = false)
    {
        return new(ante, CreateResampleStream(MotelyPrngKeys.Voucher + ante, isCached));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyVoucher GetAnteFirstVoucher(int ante, bool isCached = false)
    {
        MotelySinglePrngStream prngStream = CreatePrngStream(
            MotelyPrngKeys.Voucher + ante,
            isCached
        );
        MotelyVoucher voucher = (MotelyVoucher)GetNextRandomInt(
            ref prngStream,
            0,
            MotelyEnum<MotelyVoucher>.ValueCount
        );
        int resampleCount = 0;

        while (true)
        {
            bool prerequisiteRequired = ((int)voucher & 1) == 1;

            if (!prerequisiteRequired)
            {
                break;
            }

            prngStream = CreateResamplePrngStream(
                MotelyPrngKeys.Voucher + ante,
                resampleCount,
                isCached
            );

            voucher = (MotelyVoucher)GetNextRandomInt(
                ref prngStream,
                0,
                MotelyEnum<MotelyVoucher>.ValueCount
            );

            ++resampleCount;
        }

        return voucher;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyVoucher GetAnteFirstVoucher(
        int ante,
        in MotelyRunState voucherState,
        bool isCached = false
    )
    {
        MotelySinglePrngStream prngStream = CreatePrngStream(
            MotelyPrngKeys.Voucher + ante,
            isCached
        );
        MotelyVoucher voucher = (MotelyVoucher)GetNextRandomInt(
            ref prngStream,
            0,
            MotelyEnum<MotelyVoucher>.ValueCount
        );
        int resampleCount = 0;

        while (true)
        {
            if (!voucherState.IsVoucherActive(voucher))
            {
                bool prerequisiteRequired = ((int)voucher & 1) == 1;

                if (!prerequisiteRequired)
                {
                    break;
                }

                MotelyVoucher prerequisite = voucher - 1;
                bool prerequisiteUnlocked = voucherState.IsVoucherActive(prerequisite);

                if (prerequisiteUnlocked)
                {
                    break;
                }
            }

            prngStream = CreateResamplePrngStream(
                MotelyPrngKeys.Voucher + ante,
                resampleCount,
                isCached
            );

            voucher = (MotelyVoucher)GetNextRandomInt(
                ref prngStream,
                0,
                MotelyEnum<MotelyVoucher>.ValueCount
            );

            ++resampleCount;
        }

        return voucher;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyVoucher GetNextVoucher(
        ref MotelySingleVoucherStream voucherStream,
        in MotelyRunState voucherState
    )
    {
        MotelyVoucher voucher = (MotelyVoucher)GetNextRandomInt(
            ref voucherStream.ResampleStream.InitialPrngStream,
            0,
            MotelyEnum<MotelyVoucher>.ValueCount
        );
        int resampleCount = 0;

        while (true)
        {
            if (!voucherState.IsVoucherActive(voucher))
            {
                bool prerequisiteRequired = ((int)voucher & 1) == 1;

                if (!prerequisiteRequired)
                {
                    break;
                }

                MotelyVoucher prerequisite = voucher - 1;
                bool prerequisiteUnlocked = voucherState.IsVoucherActive(prerequisite);

                if (prerequisiteUnlocked)
                {
                    break;
                }
            }

            voucher = (MotelyVoucher)GetNextRandomInt(
                ref GetResamplePrngStream(
                    ref voucherStream.ResampleStream,
                    MotelyPrngKeys.Voucher + voucherStream.Ante,
                    resampleCount
                ),
                0,
                MotelyEnum<MotelyVoucher>.ValueCount
            );

            ++resampleCount;
        }

        return voucher;
    }
}
