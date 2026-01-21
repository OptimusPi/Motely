using System.Runtime.CompilerServices;

namespace Motely.Filters;

/// <summary>
/// Optimized bitmask for antes (max 40 antes = 40 bits, fits in single ulong)
/// Replaces wasteful bool[40] arrays
/// </summary>
public readonly struct MotelyAnteMask
{
    private readonly ulong _bits; // 64 bits, supports up to 64 antes (we only need 40)

    public MotelyAnteMask(ulong bits)
    {
        _bits = bits;
    }

    /// <summary>
    /// Create from int array of antes
    /// </summary>
    public static MotelyAnteMask FromAntes(int[] antes)
    {
        ulong bits = 0;
        foreach (var ante in antes)
        {
            if (ante >= 0 && ante < 64)
            {
                bits |= 1UL << ante;
            }
        }
        return new MotelyAnteMask(bits);
    }

    /// <summary>
    /// Create from bool array (for migration)
    /// </summary>
    public static MotelyAnteMask FromBoolArray(bool[] antes)
    {
        ulong bits = 0;
        for (int i = 0; i < antes.Length && i < 64; i++)
        {
            if (antes[i])
            {
                bits |= 1UL << i;
            }
        }
        return new MotelyAnteMask(bits);
    }

    /// <summary>
    /// Check if ante is wanted
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAnte(int ante)
    {
        if (ante < 0 || ante >= 64)
            return false;
        return (_bits & (1UL << ante)) != 0;
    }

    /// <summary>
    /// Get min ante (lowest set bit)
    /// </summary>
    public int MinAnte
    {
        get
        {
            if (_bits == 0)
                return int.MaxValue;
            return System.Numerics.BitOperations.TrailingZeroCount(_bits);
        }
    }

    /// <summary>
    /// Get max ante (highest set bit)
    /// </summary>
    public int MaxAnte
    {
        get
        {
            if (_bits == 0)
                return int.MinValue;
            return 63 - System.Numerics.BitOperations.LeadingZeroCount(_bits);
        }
    }

    /// <summary>
    /// Check if mask is empty
    /// </summary>
    public bool IsEmpty => _bits == 0;

    /// <summary>
    /// Combine multiple masks with OR
    /// </summary>
    public static MotelyAnteMask Combine(params MotelyAnteMask[] masks)
    {
        ulong combined = 0;
        foreach (var mask in masks)
        {
            combined |= mask._bits;
        }
        return new MotelyAnteMask(combined);
    }

    public ulong Bits => _bits;
}
