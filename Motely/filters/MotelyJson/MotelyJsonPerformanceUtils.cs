using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Motely.Filters;

/// <summary>
/// High-performance utility methods for the MotelyJson filter system.
/// Eliminates string comparisons and array searches in hot paths.
/// </summary>
public static class MotelyJsonPerformanceUtils
{
    #region Pre-computed Type Mappings (eliminates string comparisons)
    
    private static readonly Dictionary<string, MotelyFilterItemType> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["joker"] = MotelyFilterItemType.Joker,
        ["souljoker"] = MotelyFilterItemType.SoulJoker,
        ["tarot"] = MotelyFilterItemType.TarotCard,
        ["tarotcard"] = MotelyFilterItemType.TarotCard,
        ["planet"] = MotelyFilterItemType.PlanetCard,
        ["planetcard"] = MotelyFilterItemType.PlanetCard,
        ["spectral"] = MotelyFilterItemType.SpectralCard,
        ["spectralcard"] = MotelyFilterItemType.SpectralCard,
        ["tag"] = MotelyFilterItemType.SmallBlindTag,
        ["smallblindtag"] = MotelyFilterItemType.SmallBlindTag,
        ["bigblindtag"] = MotelyFilterItemType.BigBlindTag,
        ["voucher"] = MotelyFilterItemType.Voucher,
        ["playingcard"] = MotelyFilterItemType.PlayingCard,
        ["boss"] = MotelyFilterItemType.Boss
    };
    
    private static readonly Dictionary<string, MotelyJsonConfigWildcards> WildcardMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["any"] = MotelyJsonConfigWildcards.AnyJoker,
        ["anyjoker"] = MotelyJsonConfigWildcards.AnyJoker,
        ["*"] = MotelyJsonConfigWildcards.AnyJoker,
        ["anycommon"] = MotelyJsonConfigWildcards.AnyCommon,
        ["anyuncommon"] = MotelyJsonConfigWildcards.AnyUncommon,
        ["anyrare"] = MotelyJsonConfigWildcards.AnyRare,
        ["anylegendary"] = MotelyJsonConfigWildcards.AnyLegendary
    };
    
    #endregion
    
    #region Bitmask Operations (replaces ArrayContains)
    
    /// <summary>
    /// Convert an array of slot indices to a bitmask for O(1) lookups
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ArrayToBitmask(int[]? array)
    {
        if (array == null || array.Length == 0) return 0;
        
        ulong mask = 0;
        foreach (int slot in array)
        {
            if (slot >= 0 && slot < 64)
                mask |= (1UL << slot);
        }
        return mask;
    }
    
    /// <summary>
    /// Check if a slot is in the bitmask - O(1) instead of O(n)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBitSet(ulong bitmask, int slot)
    {
        return (bitmask & (1UL << slot)) != 0;
    }
    
    /// <summary>
    /// Get the highest set bit in a bitmask
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetHighestBit(ulong bitmask)
    {
        return bitmask == 0 ? -1 : 63 - System.Numerics.BitOperations.LeadingZeroCount(bitmask);
    }
    
    /// <summary>
    /// Count the number of set bits
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PopCount(ulong bitmask)
    {
        return System.Numerics.BitOperations.PopCount(bitmask);
    }
    
    #endregion
    
    #region SIMD Mask Operations (replaces per-lane loops)
    
    /// <summary>
    /// Extract mask from vector comparison result using SIMD intrinsics
    /// Replaces slow per-lane loops with single instruction
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetVectorMaskBits(Vector256<int> comparison)
    {
        if (Avx2.IsSupported)
        {
            // Use MoveMask for maximum performance
            return (uint)Avx2.MoveMask(comparison.AsByte());
        }
        else
        {
            // Fallback for non-AVX2 systems
            uint mask = 0;
            for (int i = 0; i < 8; i++)
            {
                if (comparison[i] == -1) mask |= (1u << i);
            }
            return mask;
        }
    }
    

    
    #endregion
    
    #region Type Parsing (eliminates string operations in hot paths)
    
    /// <summary>
    /// Parse item type without string allocation
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyFilterItemType ParseItemType(string? type)
    {
        if (string.IsNullOrEmpty(type)) 
            throw new ArgumentException("Type cannot be null or empty");
            
        return TypeMap.TryGetValue(type, out var itemType) 
            ? itemType 
            : throw new ArgumentException($"Unknown filter item type: {type}");
    }
    
    /// <summary>
    /// Parse wildcard without string comparisons
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (bool isWildcard, MotelyJsonConfigWildcards? wildcard) ParseWildcard(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return (false, null);
            
        return WildcardMap.TryGetValue(value, out var wildcard) 
            ? (true, wildcard) 
            : (false, null);
    }
    
    #endregion
    
    #region Pre-computed Enum Values (eliminates repeated casting)
    
    /// <summary>
    /// Pre-compute MotelyItemType values for jokers
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyItemType GetJokerItemType(MotelyJoker joker)
    {
        return (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)joker);
    }
    
    /// <summary>
    /// Pre-compute MotelyItemType values for planet cards
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyItemType GetPlanetItemType(MotelyPlanetCard planet)
    {
        return (MotelyItemType)((int)MotelyItemTypeCategory.PlanetCard | (int)planet);
    }
    
    /// <summary>
    /// Pre-compute MotelyItemType values for spectral cards
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyItemType GetSpectralItemType(MotelySpectralCard spectral)
    {
        return (MotelyItemType)((int)MotelyItemTypeCategory.SpectralCard | (int)spectral);
    }
    
    /// <summary>
    /// Pre-compute MotelyItemType values for tarot cards
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyItemType GetTarotItemType(MotelyTarotCard tarot)
    {
        return (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)tarot);
    }
    
    #endregion
}
