using System.Runtime.CompilerServices;

namespace Motely.Filters;

/// <summary>
/// Optimized bitmask for shop slots (max 1024 slots = 1024 bits = 16 ulongs)
/// Replaces wasteful bool[1024] arrays (1024 bytes -> 128 bytes)
/// </summary>
public readonly struct MotelyShopSlotMask
{
    private readonly ulong _bits0; // Slots 0-63
    private readonly ulong _bits1; // Slots 64-127
    private readonly ulong _bits2; // Slots 128-191
    private readonly ulong _bits3; // Slots 192-255
    private readonly ulong _bits4; // Slots 256-319
    private readonly ulong _bits5; // Slots 320-383
    private readonly ulong _bits6; // Slots 384-447
    private readonly ulong _bits7; // Slots 448-511
    private readonly ulong _bits8; // Slots 512-575
    private readonly ulong _bits9; // Slots 576-639
    private readonly ulong _bits10; // Slots 640-703
    private readonly ulong _bits11; // Slots 704-767
    private readonly ulong _bits12; // Slots 768-831
    private readonly ulong _bits13; // Slots 832-895
    private readonly ulong _bits14; // Slots 896-959
    private readonly ulong _bits15; // Slots 960-1023

    public MotelyShopSlotMask(
        ulong bits0 = 0,
        ulong bits1 = 0,
        ulong bits2 = 0,
        ulong bits3 = 0,
        ulong bits4 = 0,
        ulong bits5 = 0,
        ulong bits6 = 0,
        ulong bits7 = 0,
        ulong bits8 = 0,
        ulong bits9 = 0,
        ulong bits10 = 0,
        ulong bits11 = 0,
        ulong bits12 = 0,
        ulong bits13 = 0,
        ulong bits14 = 0,
        ulong bits15 = 0
    )
    {
        _bits0 = bits0;
        _bits1 = bits1;
        _bits2 = bits2;
        _bits3 = bits3;
        _bits4 = bits4;
        _bits5 = bits5;
        _bits6 = bits6;
        _bits7 = bits7;
        _bits8 = bits8;
        _bits9 = bits9;
        _bits10 = bits10;
        _bits11 = bits11;
        _bits12 = bits12;
        _bits13 = bits13;
        _bits14 = bits14;
        _bits15 = bits15;
    }

    /// <summary>
    /// Create from int array of shop slots
    /// </summary>
    public static MotelyShopSlotMask FromSlots(int[] slots)
    {
        ulong[] bits = new ulong[16];
        foreach (var slot in slots)
        {
            if (slot >= 0 && slot < 1024)
            {
                int chunk = slot / 64;
                int bit = slot % 64;
                bits[chunk] |= 1UL << bit;
            }
        }
        return new MotelyShopSlotMask(
            bits[0],
            bits[1],
            bits[2],
            bits[3],
            bits[4],
            bits[5],
            bits[6],
            bits[7],
            bits[8],
            bits[9],
            bits[10],
            bits[11],
            bits[12],
            bits[13],
            bits[14],
            bits[15]
        );
    }

    /// <summary>
    /// Create from bool array (for migration)
    /// </summary>
    public static MotelyShopSlotMask FromBoolArray(bool[] slots)
    {
        ulong[] bits = new ulong[16];
        for (int i = 0; i < slots.Length && i < 1024; i++)
        {
            if (slots[i])
            {
                int chunk = i / 64;
                int bit = i % 64;
                bits[chunk] |= 1UL << bit;
            }
        }
        return new MotelyShopSlotMask(
            bits[0],
            bits[1],
            bits[2],
            bits[3],
            bits[4],
            bits[5],
            bits[6],
            bits[7],
            bits[8],
            bits[9],
            bits[10],
            bits[11],
            bits[12],
            bits[13],
            bits[14],
            bits[15]
        );
    }

    /// <summary>
    /// Create from min/max range (inclusive min, exclusive max)
    /// </summary>
    public static MotelyShopSlotMask FromRange(int minSlot, int maxSlot)
    {
        ulong[] bits = new ulong[16];
        for (int slot = minSlot; slot < maxSlot && slot < 1024; slot++)
        {
            int chunk = slot / 64;
            int bit = slot % 64;
            bits[chunk] |= 1UL << bit;
        }
        return new MotelyShopSlotMask(
            bits[0],
            bits[1],
            bits[2],
            bits[3],
            bits[4],
            bits[5],
            bits[6],
            bits[7],
            bits[8],
            bits[9],
            bits[10],
            bits[11],
            bits[12],
            bits[13],
            bits[14],
            bits[15]
        );
    }

    /// <summary>
    /// Check if slot is wanted
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasSlot(int slot)
    {
        if (slot < 0 || slot >= 1024)
            return false;

        int chunk = slot / 64;
        int bit = slot % 64;
        ulong chunkBits = chunk switch
        {
            0 => _bits0,
            1 => _bits1,
            2 => _bits2,
            3 => _bits3,
            4 => _bits4,
            5 => _bits5,
            6 => _bits6,
            7 => _bits7,
            8 => _bits8,
            9 => _bits9,
            10 => _bits10,
            11 => _bits11,
            12 => _bits12,
            13 => _bits13,
            14 => _bits14,
            15 => _bits15,
            _ => 0,
        };
        return (chunkBits & (1UL << bit)) != 0;
    }

    /// <summary>
    /// Get max slot index (highest set bit)
    /// </summary>
    public int MaxSlot
    {
        get
        {
            for (int chunk = 15; chunk >= 0; chunk--)
            {
                ulong chunkBits = chunk switch
                {
                    0 => _bits0,
                    1 => _bits1,
                    2 => _bits2,
                    3 => _bits3,
                    4 => _bits4,
                    5 => _bits5,
                    6 => _bits6,
                    7 => _bits7,
                    8 => _bits8,
                    9 => _bits9,
                    10 => _bits10,
                    11 => _bits11,
                    12 => _bits12,
                    13 => _bits13,
                    14 => _bits14,
                    15 => _bits15,
                    _ => 0,
                };
                if (chunkBits != 0)
                {
                    int leadingZeros = System.Numerics.BitOperations.LeadingZeroCount(chunkBits);
                    return (chunk * 64) + (63 - leadingZeros);
                }
            }
            return -1;
        }
    }

    /// <summary>
    /// Check if mask is empty
    /// </summary>
    public bool IsEmpty =>
        _bits0 == 0
        && _bits1 == 0
        && _bits2 == 0
        && _bits3 == 0
        && _bits4 == 0
        && _bits5 == 0
        && _bits6 == 0
        && _bits7 == 0
        && _bits8 == 0
        && _bits9 == 0
        && _bits10 == 0
        && _bits11 == 0
        && _bits12 == 0
        && _bits13 == 0
        && _bits14 == 0
        && _bits15 == 0;
}
