using System.Diagnostics;

namespace Motely;

/// <summary>JS-serializable snapshot of <see cref="MotelyRunState"/> (voucher + boss bitfields).</summary>
public record MotelyJsRunState(int VoucherBitfield, int BossBitfield)
{
    public static readonly MotelyJsRunState Default = new(0, 0);

    public MotelyRunState ToRunState()
    {
        return new MotelyRunState { VoucherBitfield = VoucherBitfield, BossBitfield = BossBitfield };
    }
}

public record MotelyBossStateResult(MotelyBossBlind Boss, MotelyJsRunState RunState);

public record MotelyVoucherStateResult(MotelyVoucher Voucher, MotelyJsRunState RunState);

public ref struct MotelyRunState
{
    private static readonly int FinisherBossBlindMask;
    private static readonly int NormalBossBlindMask;

    static MotelyRunState()
    {
        // Check that we can fit all the voucher state in an int
        if (MotelyEnum<MotelyVoucher>.ValueCount > sizeof(int) * 8)
            throw new UnreachableException();

        // Check that we can fit all the bosses state in an int
        if (MotelyEnum<MotelyBossBlind>.ValueCount > sizeof(int) * 8)
            throw new UnreachableException();

        FinisherBossBlindMask = 0;
        NormalBossBlindMask = 0;
        foreach (MotelyBossBlind bossBlind in MotelyEnum<MotelyBossBlind>.Values)
        {
            if (bossBlind.GetBossType() == MotelyBossBlindType.Finisher)
            {
                FinisherBossBlindMask |= 1 << bossBlind.GetBossIndex();
            }
            else
            {
                NormalBossBlindMask |= 1 << bossBlind.GetBossIndex();
            }
        }
    }

    public int VoucherBitfield;
    public int BossBitfield;

    private bool _showmanActive;

    // Boss caching for scoring - generated once per seed to maintain state
    public MotelyBossBlind[]? CachedBosses;

    public readonly bool ShowmanActive => _showmanActive;

    public void ActivateVoucher(MotelyVoucher voucher)
    {
        VoucherBitfield |= 1 << (int)voucher;
    }

    public readonly bool IsVoucherActive(MotelyVoucher voucher)
    {
        return (VoucherBitfield & (1 << (int)voucher)) != 0;
    }

    public void SeeBoss(MotelyBossBlind boss)
    {
        BossBitfield |= 1 << boss.GetBossIndex();
    }

    public readonly bool HasSeenBoss(MotelyBossBlind boss)
    {
        return (BossBitfield & (1 << boss.GetBossIndex())) != 0;
    }

    public void ResetFinisherBosses()
    {
        // Only allow normal boss bits to be set
        BossBitfield &= NormalBossBlindMask;
    }

    public void ResetNormalBosses()
    {
        // Only allow finisher boss bits to be set
        BossBitfield &= FinisherBossBlindMask;
    }

    public void ActivateShowman()
    {
        _showmanActive = true;
    }
}
