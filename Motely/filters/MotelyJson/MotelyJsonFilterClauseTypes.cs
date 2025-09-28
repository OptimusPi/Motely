using System;
using System.Collections.Generic;
using System.Linq;

namespace Motely.Filters;

/// <summary>
/// Base class for all JSON filter clauses
/// </summary>
public abstract class MotelyJsonFilterClause
{
    public MotelyItemEdition? EditionEnum { get; init; }
    
    
    /// <summary>
    /// Calculate min and max antes from a collection of clauses using their arrays
    /// </summary>
    public static (int minAnte, int maxAnte) CalculateAnteRange<T>(IEnumerable<T> clauses) 
        where T : MotelyJsonFilterClause
    {
        int minAnte = int.MaxValue;
        int maxAnte = int.MinValue;
        
        foreach (var clause in clauses)
        {
            // Get ante array from derived class
            bool[]? wantedAntes = clause switch
            {
                MotelyJsonJokerFilterClause j => j.WantedAntes,
                MotelyJsonSoulJokerFilterClause s => s.WantedAntes,
                MotelyJsonVoucherFilterClause v => v.WantedAntes,
                MotelyJsonTarotFilterClause t => t.WantedAntes,
                MotelyJsonSpectralFilterClause sp => sp.WantedAntes,
                MotelyJsonPlanetFilterClause p => p.WantedAntes,
                _ => null
            };
            
            if (wantedAntes != null)
            {
                // Find min and max wanted antes - simple and clear
                for (int ante = 0; ante < wantedAntes.Length; ante++)
                {
                    if (wantedAntes[ante])
                    {
                        if (ante < minAnte) minAnte = ante;
                        if (ante > maxAnte) maxAnte = ante;
                    }
                }
            }
        }
        
        // Handle empty case
        if (minAnte == int.MaxValue)
        {
            minAnte = 1;
            maxAnte = 1;
        }
        
        return (minAnte, maxAnte);
    }
}

/// <summary>
/// Specific clause type for Joker filters
/// </summary>
public class MotelyJsonJokerFilterClause : MotelyJsonFilterClause
{
    public MotelyJoker? JokerType { get; init; }
    public List<MotelyJoker>? JokerTypes { get; init; }
    public new MotelyItemEdition? EditionEnum { get; init; }  // ADDED: Preserve edition requirements (new keyword to hide base class member)
    public List<MotelyJokerSticker>? StickerEnums { get; init; }  // ADDED: Preserve sticker requirements
    public bool IsWildcard { get; init; }
    public MotelyJsonConfigWildcards? WildcardEnum { get; init; }
    public MotelyJsonConfig.SourcesConfig? Sources { get; init; }
    public bool[] WantedAntes { get; init; } = new bool[40];
    public bool[] WantedShopSlots { get; init; } = new bool[64];
    public bool[] WantedPackSlots { get; init; } = new bool[6];
    
    /// <summary>
    /// Create from generic JSON config clause
    /// </summary>
    public static MotelyJsonJokerFilterClause FromJsonClause(MotelyJsonConfig.MotleyJsonFilterClause jsonClause)
    {
        bool[] wantedAntes = new bool[40];
        var effectiveAntes = jsonClause.EffectiveAntes;
        foreach (var ante in effectiveAntes)
        {
            if (ante >= 0 && ante < 40)
                wantedAntes[ante] = true;
        }
        
        // Create shop slots bool array - check both new format (Sources) and old format (direct)
        bool[] wantedShopSlots = new bool[64];
        // First check new format
        if (jsonClause.Sources?.ShopSlots != null)
        {
            foreach (var slot in jsonClause.Sources.ShopSlots)
            {
                if (slot >= 0 && slot < 64) wantedShopSlots[slot] = true;
            }
        }
        // Fall back to old format if no sources
        else if (jsonClause.ShopSlots != null)
        {
            foreach (var slot in jsonClause.ShopSlots)
            {
                if (slot >= 0 && slot < 64) wantedShopSlots[slot] = true;
            }
        }
        
        bool[] wantedPackSlots = new bool[6];
        // First check new format
        if (jsonClause.Sources?.PackSlots != null)
        {
            foreach (var slot in jsonClause.Sources.PackSlots)
            {
                if (slot >= 0 && slot < 6) wantedPackSlots[slot] = true;
            }
        }
        // Fall back to old format if no sources
        else if (jsonClause.PackSlots != null)
        {
            foreach (var slot in jsonClause.PackSlots)
            {
                if (slot >= 0 && slot < 6) wantedPackSlots[slot] = true;
            }
        }
        
        return new MotelyJsonJokerFilterClause
        {
            JokerType = jsonClause.JokerEnum,
            JokerTypes = jsonClause.JokerEnums?.Count > 0 ? jsonClause.JokerEnums : null,
            IsWildcard = jsonClause.IsWildcard,
            WildcardEnum = jsonClause.WildcardEnum,
            Sources = jsonClause.Sources,
            EditionEnum = jsonClause.EditionEnum,
            StickerEnums = jsonClause.StickerEnums,
            WantedAntes = wantedAntes,
            WantedShopSlots = wantedShopSlots,
            WantedPackSlots = wantedPackSlots
        };
    }
    
    /// <summary>
    /// Convert a list of generic clauses to joker-specific clauses
    /// </summary>
    public static List<MotelyJsonJokerFilterClause> ConvertClauses(List<MotelyJsonConfig.MotleyJsonFilterClause> genericClauses)
    {
        return genericClauses
            .Where(c => c.ItemTypeEnum == MotelyFilterItemType.Joker)
            .Select(FromJsonClause)
            .ToList();
    }
}

/// <summary>
/// Specific clause type for SoulJoker filters
/// </summary>
public class MotelyJsonSoulJokerFilterClause : MotelyJsonFilterClause
{
    public MotelyJoker? JokerType { get; init; }
    public MotelyItemType? JokerItemType { get; init; }  // Precomputed MotelyItemType for comparison
    public bool IsWildcard { get; init; }
    public bool[] WantedAntes { get; init; } = new bool[40];
    public bool[] WantedPackSlots { get; init; } = new bool[6];  // Track which pack slots to check
    public bool RequireMega { get; init; }  // Extracted from Sources for optimization
    public bool Satisfied { get; set; }  // Track if this clause has been satisfied
    
    // Parameterless constructor for init syntax
    public MotelyJsonSoulJokerFilterClause() { }
    
    // Helper constructor for tests - takes List<int> for antes and pack slots
    public MotelyJsonSoulJokerFilterClause(MotelyJoker? jokerType, List<int> antes, List<int> packSlots, bool requireMega = false)
    {
        JokerType = jokerType;
        JokerItemType = jokerType.HasValue ? (MotelyItemType)jokerType.Value : null;
        IsWildcard = !jokerType.HasValue;
        RequireMega = requireMega;
        
        // Convert antes list to bool array
        WantedAntes = new bool[40];
        foreach (var ante in antes)
        {
            if (ante >= 0 && ante < 40)
                WantedAntes[ante] = true;
        }
        
        // Convert pack slots list to bool array
        WantedPackSlots = new bool[6];
        foreach (var slot in packSlots)
        {
            if (slot >= 0 && slot < 6)
                WantedPackSlots[slot] = true;
        }
    }
    
    public static MotelyJsonSoulJokerFilterClause FromJsonClause(MotelyJsonConfig.MotleyJsonFilterClause jsonClause)
    {
        bool[] wantedAntes = new bool[40];
        var effectiveAntes = jsonClause.EffectiveAntes ?? Array.Empty<int>();
        foreach (var ante in effectiveAntes)
        {
            if (ante >= 0 && ante < 40)
                wantedAntes[ante] = true;
        }
        
        // Build pack slot array
        bool[] wantedPackSlots = new bool[6];
        if (jsonClause.Sources?.PackSlots != null)
        {
            foreach (var slot in jsonClause.Sources.PackSlots)
            {
                if (slot >= 0 && slot < 6) wantedPackSlots[slot] = true;
            }
        }
        
        var clause = new MotelyJsonSoulJokerFilterClause
        {
            JokerType = jsonClause.JokerEnum,
            JokerItemType = jsonClause.JokerEnum.HasValue ? (MotelyItemType)jsonClause.JokerEnum.Value : null,
            IsWildcard = jsonClause.IsWildcard,
            EditionEnum = jsonClause.EditionEnum,
            WantedAntes = wantedAntes,
            WantedPackSlots = wantedPackSlots,
            RequireMega = jsonClause.Sources?.RequireMega ?? false,
            Satisfied = false
        };
        
        return clause;
    }
    
    public static List<MotelyJsonSoulJokerFilterClause> ConvertClauses(List<MotelyJsonConfig.MotleyJsonFilterClause> genericClauses)
    {
        return genericClauses
            .Where(c => c.ItemTypeEnum == MotelyFilterItemType.SoulJoker)
            .Select(FromJsonClause)
            .ToList();
    }
}

/// <summary>
/// Specific clause type for Tarot filters
/// </summary>
public class MotelyJsonTarotFilterClause : MotelyJsonFilterClause
{
    public MotelyTarotCard? TarotType { get; init; }
    public List<MotelyTarotCard>? TarotTypes { get; init; }
    public bool IsWildcard { get; init; }
    public MotelyJsonConfig.SourcesConfig? Sources { get; init; }
    public bool[] WantedAntes { get; init; } = new bool[40];
    public bool[] WantedPackSlots { get; init; } = new bool[6];
    public bool[] WantedShopSlots { get; init; } = new bool[64];
    
    public static MotelyJsonTarotFilterClause FromJsonClause(MotelyJsonConfig.MotleyJsonFilterClause jsonClause)
    {
        bool[] wantedAntes = new bool[40];
        var effectiveAntes = jsonClause.EffectiveAntes ?? Array.Empty<int>();
        foreach (var ante in effectiveAntes)
        {
            if (ante >= 0 && ante < 40)
                wantedAntes[ante] = true;
        }
        
        // Create shop slots bool array - check both new format (Sources) and old format (direct)
        bool[] wantedShopSlots = new bool[64];
        // First check new format
        if (jsonClause.Sources?.ShopSlots != null)
        {
            foreach (var slot in jsonClause.Sources.ShopSlots)
            {
                if (slot >= 0 && slot < 64) wantedShopSlots[slot] = true;
            }
        }
        // Fall back to old format if no sources
        else if (jsonClause.ShopSlots != null)
        {
            foreach (var slot in jsonClause.ShopSlots)
            {
                if (slot >= 0 && slot < 64) wantedShopSlots[slot] = true;
            }
        }
        
        bool[] wantedPackSlots = new bool[6];
        // First check new format
        if (jsonClause.Sources?.PackSlots != null)
        {
            foreach (var slot in jsonClause.Sources.PackSlots)
            {
                if (slot >= 0 && slot < 6) wantedPackSlots[slot] = true;
            }
        }
        // Fall back to old format if no sources
        else if (jsonClause.PackSlots != null)
        {
            foreach (var slot in jsonClause.PackSlots)
            {
                if (slot >= 0 && slot < 6) wantedPackSlots[slot] = true;
            }
        }
        
        return new MotelyJsonTarotFilterClause
        {
            TarotType = jsonClause.TarotEnum,
            TarotTypes = jsonClause.TarotEnums?.Count > 0 ? jsonClause.TarotEnums : null,
            IsWildcard = jsonClause.IsWildcard,
            Sources = jsonClause.Sources,
            EditionEnum = jsonClause.EditionEnum,
            WantedAntes = wantedAntes,
            WantedPackSlots = wantedPackSlots,
            WantedShopSlots = wantedShopSlots
        };
    }
    
    public static List<MotelyJsonTarotFilterClause> ConvertClauses(List<MotelyJsonConfig.MotleyJsonFilterClause> genericClauses)
    {
        return genericClauses
            .Where(c => c.ItemTypeEnum == MotelyFilterItemType.TarotCard)
            .Select(FromJsonClause)
            .ToList();
    }
}

/// <summary>
/// Specific clause type for Voucher filters
/// </summary>
public class MotelyJsonVoucherFilterClause : MotelyJsonFilterClause
{
    public MotelyVoucher VoucherType { get; init; }
    public List<MotelyVoucher>? VoucherTypes { get; init; }
    public bool[] WantedAntes { get; init; } = new bool[40];
    
    public static MotelyJsonVoucherFilterClause FromJsonClause(MotelyJsonConfig.MotleyJsonFilterClause jsonClause)
    {
        bool[] wantedAntes = new bool[40];
        var effectiveAntes = jsonClause.EffectiveAntes ?? Array.Empty<int>();
        foreach (var ante in effectiveAntes)
        {
            if (ante >= 0 && ante < 40)
                wantedAntes[ante] = true;
        }
        
        return new MotelyJsonVoucherFilterClause
        {
            VoucherType = jsonClause.VoucherEnum ?? MotelyVoucher.Overstock,
            VoucherTypes = jsonClause.VoucherEnums?.Count > 0 ? jsonClause.VoucherEnums : null,
            WantedAntes = wantedAntes
        };
    }
    
    public static List<MotelyJsonVoucherFilterClause> ConvertClauses(List<MotelyJsonConfig.MotleyJsonFilterClause> genericClauses)
    {
        return genericClauses
            .Where(c => c.ItemTypeEnum == MotelyFilterItemType.Voucher)
            .Select(FromJsonClause)
            .ToList();
    }
}

/// <summary>
/// Specific clause type for Spectral filters
/// </summary>
public class MotelyJsonSpectralFilterClause : MotelyJsonFilterClause
{
    public MotelySpectralCard? SpectralType { get; init; }
    public List<MotelySpectralCard>? SpectralTypes { get; init; }
    public bool IsWildcard { get; init; }
    public MotelyJsonConfig.SourcesConfig? Sources { get; init; }
    public bool[] WantedAntes { get; init; } = new bool[40];
    public bool[] WantedShopSlots { get; init; } = new bool[64];
    public bool[] WantedPackSlots { get; init; } = new bool[6];
    
    public static MotelyJsonSpectralFilterClause FromJsonClause(MotelyJsonConfig.MotleyJsonFilterClause jsonClause)
    {
        bool[] wantedAntes = new bool[40];
        var effectiveAntes = jsonClause.EffectiveAntes ?? Array.Empty<int>();
        foreach (var ante in effectiveAntes)
        {
            if (ante >= 0 && ante < 40)
                wantedAntes[ante] = true;
        }
        
        // Create shop slots bool array - check both new format (Sources) and old format (direct)
        bool[] wantedShopSlots = new bool[64];
        // First check new format
        if (jsonClause.Sources?.ShopSlots != null)
        {
            foreach (var slot in jsonClause.Sources.ShopSlots)
            {
                if (slot >= 0 && slot < 64) wantedShopSlots[slot] = true;
            }
        }
        // Fall back to old format if no sources
        else if (jsonClause.ShopSlots != null)
        {
            foreach (var slot in jsonClause.ShopSlots)
            {
                if (slot >= 0 && slot < 64) wantedShopSlots[slot] = true;
            }
        }
        
        bool[] wantedPackSlots = new bool[6];
        // First check new format
        if (jsonClause.Sources?.PackSlots != null)
        {
            foreach (var slot in jsonClause.Sources.PackSlots)
            {
                if (slot >= 0 && slot < 6) wantedPackSlots[slot] = true;
            }
        }
        // Fall back to old format if no sources
        else if (jsonClause.PackSlots != null)
        {
            foreach (var slot in jsonClause.PackSlots)
            {
                if (slot >= 0 && slot < 6) wantedPackSlots[slot] = true;
            }
        }
        
        return new MotelyJsonSpectralFilterClause
        {
            SpectralType = jsonClause.SpectralEnum,
            SpectralTypes = jsonClause.SpectralEnums?.Count > 0 ? jsonClause.SpectralEnums : null,
            IsWildcard = jsonClause.IsWildcard,
            Sources = jsonClause.Sources,
            EditionEnum = jsonClause.EditionEnum,
            WantedAntes = wantedAntes,
            WantedShopSlots = wantedShopSlots,
            WantedPackSlots = wantedPackSlots
        };
    }
    
    public static List<MotelyJsonSpectralFilterClause> ConvertClauses(List<MotelyJsonConfig.MotleyJsonFilterClause> genericClauses)
    {
        return genericClauses
            .Where(c => c.ItemTypeEnum == MotelyFilterItemType.SpectralCard)
            .Select(FromJsonClause)
            .ToList();
    }
}

/// <summary>
/// Specific clause type for Planet filters
/// </summary>
public class MotelyJsonPlanetFilterClause : MotelyJsonFilterClause
{
    public MotelyPlanetCard? PlanetType { get; init; }
    public List<MotelyPlanetCard>? PlanetTypes { get; init; }
    public bool IsWildcard { get; init; }
    public MotelyJsonConfig.SourcesConfig? Sources { get; init; }
    public bool[] WantedAntes { get; init; } = new bool[40];
    public bool[] WantedShopSlots { get; init; } = new bool[64];
    public bool[] WantedPackSlots { get; init; } = new bool[6];
    
    public static MotelyJsonPlanetFilterClause FromJsonClause(MotelyJsonConfig.MotleyJsonFilterClause jsonClause)
    {
        bool[] wantedAntes = new bool[40];
        var effectiveAntes = jsonClause.EffectiveAntes ?? Array.Empty<int>();
        foreach (var ante in effectiveAntes)
        {
            if (ante >= 0 && ante < 40)
                wantedAntes[ante] = true;
        }
        
        // Create shop slots bool array - check both new format (Sources) and old format (direct)
        bool[] wantedShopSlots = new bool[64];
        // First check new format
        if (jsonClause.Sources?.ShopSlots != null)
        {
            foreach (var slot in jsonClause.Sources.ShopSlots)
            {
                if (slot >= 0 && slot < 64) wantedShopSlots[slot] = true;
            }
        }
        // Fall back to old format if no sources
        else if (jsonClause.ShopSlots != null)
        {
            foreach (var slot in jsonClause.ShopSlots)
            {
                if (slot >= 0 && slot < 64) wantedShopSlots[slot] = true;
            }
        }
        
        bool[] wantedPackSlots = new bool[6];
        // First check new format
        if (jsonClause.Sources?.PackSlots != null)
        {
            foreach (var slot in jsonClause.Sources.PackSlots)
            {
                if (slot >= 0 && slot < 6) wantedPackSlots[slot] = true;
            }
        }
        // Fall back to old format if no sources
        else if (jsonClause.PackSlots != null)
        {
            foreach (var slot in jsonClause.PackSlots)
            {
                if (slot >= 0 && slot < 6) wantedPackSlots[slot] = true;
            }
        }
        
        return new MotelyJsonPlanetFilterClause
        {
            PlanetType = jsonClause.PlanetEnum,
            PlanetTypes = jsonClause.PlanetEnums?.Count > 0 ? jsonClause.PlanetEnums : null,
            IsWildcard = jsonClause.IsWildcard,
            Sources = jsonClause.Sources,
            EditionEnum = jsonClause.EditionEnum,
            WantedAntes = wantedAntes,
            WantedShopSlots = wantedShopSlots,
            WantedPackSlots = wantedPackSlots
        };
    }
    
    public static List<MotelyJsonPlanetFilterClause> ConvertClauses(List<MotelyJsonConfig.MotleyJsonFilterClause> genericClauses)
    {
        return genericClauses
            .Where(c => c.ItemTypeEnum == MotelyFilterItemType.PlanetCard)
            .Select(FromJsonClause)
            .ToList();
    }
}