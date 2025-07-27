using System.Runtime.CompilerServices;
using Motely;

namespace Motely.Filters;

/// <summary>
/// Fully typed Ouija configuration - NO STRING COMPARISONS IN HOT PATHS!
/// This is what the filter actually uses internally after parsing the JSON config.
/// </summary>
public struct TypedOuijaConfig
{
    public int MaxSearchAnte;
    public MotelyDeck Deck;
    public MotelyStake Stake;
    public bool ScoreNaturalNegatives;
    public bool ScoreDesiredNegatives;
    
    public TypedDesire[] Needs;
    public TypedDesire[] Wants;
    
    /// <summary>
    /// Fully typed desire - no strings, pure enums for SIMD performance
    /// </summary>
    public struct TypedDesire
    {
        public DesireType Type;
        public int[] SearchAntes;
        public int Score;
        
        // Type-specific values (union-like, only one is valid based on Type)
        public MotelyJoker JokerValue;
        public MotelyPlanetCard PlanetValue;
        public MotelySpectralCard SpectralValue;
        public MotelyTarotCard TarotValue;
        public MotelyTag TagValue;
        public MotelyVoucher VoucherValue;
        
        // Edition for jokers (MotelyItemEdition.None means no edition requirement)
        public MotelyItemEdition RequiredEdition;
        
        // Cached item type for fast filtering
        public MotelyItemType ItemType;
        
        // Source options
        public bool IncludeSkipTags;
        public bool IncludeBoosterPacks;
        public bool IncludeShopStream;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchesJoker(MotelyJoker joker, MotelyItemEdition edition)
        {
            if (Type != DesireType.Joker && Type != DesireType.SoulJoker)
                return false;
                
            // Special handling for "any" joker (JokerValue = 0)
            if (JokerValue != (MotelyJoker)0 && joker != JokerValue)
                return false;
                
            // For SoulJoker type, only match legendary jokers
            if (Type == DesireType.SoulJoker)
            {
                // Check if this joker is legendary (has the legendary flag)
                var rarity = (int)joker & 0xFF00;
                if (rarity != (int)MotelyJokerRarity.Legendary)
                    return false;
            }
                
            // Check edition if required
            if (RequiredEdition != MotelyItemEdition.None && edition != RequiredEdition)
                return false;
                
            return true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchesPlanet(MotelyPlanetCard planet)
        {
            return Type == DesireType.Planet && planet == PlanetValue;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchesSpectral(MotelySpectralCard spectral)
        {
            return Type == DesireType.Spectral && spectral == SpectralValue;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchesTarot(MotelyTarotCard tarot)
        {
            return Type == DesireType.Tarot && tarot == TarotValue;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchesTag(MotelyTag tag)
        {
            return Type == DesireType.Tag && tag == TagValue;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchesVoucher(MotelyVoucher voucher)
        {
            return Type == DesireType.Voucher && voucher == VoucherValue;
        }
    }
    
    public enum DesireType : byte
    {
        Joker,
        SoulJoker,
        Planet,
        Spectral,
        Tarot,
        Tag,
        SmallBlindTag,
        BigBlindTag,
        Voucher,
        PlayingCard // Future support
    }
    
    /// <summary>
    /// Creates a typed config from the string-based OuijaConfig
    /// This is done ONCE during filter creation, never in the hot path
    /// </summary>
    public static TypedOuijaConfig FromOuijaConfig(OuijaConfig config)
    {
        // Parse deck and stake ONCE
        var deck = MotelyDeck.Red;
        var stake = MotelyStake.White;
        
        if (!string.IsNullOrEmpty(config.Deck))
            Enum.TryParse<MotelyDeck>(config.Deck, true, out deck);
            
        if (!string.IsNullOrEmpty(config.Stake))
            Enum.TryParse<MotelyStake>(config.Stake, true, out stake);
        
        return new TypedOuijaConfig
        {
            MaxSearchAnte = config.MaxSearchAnte,
            Deck = deck,
            Stake = stake,
            ScoreNaturalNegatives = config.ScoreNaturalNegatives,
            ScoreDesiredNegatives = config.ScoreDesiredNegatives,
            Needs = config.Needs?.Select(ConvertDesire).ToArray() ?? Array.Empty<TypedDesire>(),
            Wants = config.Wants?.Select(ConvertDesire).ToArray() ?? Array.Empty<TypedDesire>()
        };
    }
    
    private static TypedDesire ConvertDesire(OuijaConfig.Desire desire)
    {
        var typed = new TypedDesire
        {
            SearchAntes = desire.SearchAntes ?? Array.Empty<int>(),
            Score = desire.Score,
            RequiredEdition = MotelyItemEdition.None,
            IncludeSkipTags = desire.IncludeSkipTags,
            IncludeBoosterPacks = desire.IncludeBoosterPacks,
            IncludeShopStream = desire.IncludeShopStream
        };
        
        // Parse edition if specified
        if (!string.IsNullOrEmpty(desire.Edition) && desire.Edition != "None")
        {
            Enum.TryParse<MotelyItemEdition>(desire.Edition, true, out typed.RequiredEdition);
        }
        
        // Determine type and set value - prefer cached enums
        if (desire.JokerEnum.HasValue)
        {
            typed.Type = desire.Type?.Equals("SoulJoker", StringComparison.OrdinalIgnoreCase) == true 
                ? DesireType.SoulJoker 
                : DesireType.Joker;
            typed.JokerValue = desire.JokerEnum.Value;
            typed.ItemType = (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)typed.JokerValue);
        }
        else if (desire.Type?.Equals("Joker", StringComparison.OrdinalIgnoreCase) == true && 
                 desire.Value?.Equals("any", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Special case: "any" joker - set type but leave JokerValue as default (0)
            typed.Type = DesireType.Joker;
            typed.JokerValue = (MotelyJoker)0; // Use 0 as a marker for "any"
            typed.ItemType = (MotelyItemType)0; // Use 0 to indicate "any" joker
        }
        else if (desire.Type?.Equals("SoulJoker", StringComparison.OrdinalIgnoreCase) == true && 
                 desire.Value?.Equals("any", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Special case: "any" soul joker - set type but leave JokerValue as default (0)
            typed.Type = DesireType.SoulJoker;
            typed.JokerValue = (MotelyJoker)0; // Use 0 as a marker for "any"
            typed.ItemType = (MotelyItemType)0; // Use 0 to indicate "any" soul joker
        }
        else if (desire.PlanetEnum.HasValue)
        {
            typed.Type = DesireType.Planet;
            typed.PlanetValue = desire.PlanetEnum.Value;
            typed.ItemType = (MotelyItemType)((int)MotelyItemTypeCategory.PlanetCard | (int)typed.PlanetValue);
        }
        else if (desire.SpectralEnum.HasValue)
        {
            typed.Type = DesireType.Spectral;
            typed.SpectralValue = desire.SpectralEnum.Value;
            typed.ItemType = (MotelyItemType)((int)MotelyItemTypeCategory.SpectralCard | (int)typed.SpectralValue);
        }
        else if (desire.TarotEnum.HasValue)
        {
            typed.Type = DesireType.Tarot;
            typed.TarotValue = desire.TarotEnum.Value;
            typed.ItemType = (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)typed.TarotValue);
        }
        else if (desire.TagEnum.HasValue)
        {
            if (desire.Type?.Equals("SmallBlindTag", StringComparison.OrdinalIgnoreCase) == true)
                typed.Type = DesireType.SmallBlindTag;
            else if (desire.Type?.Equals("BigBlindTag", StringComparison.OrdinalIgnoreCase) == true)
                typed.Type = DesireType.BigBlindTag;
            else
                typed.Type = DesireType.Tag;
            typed.TagValue = desire.TagEnum.Value;
        }
        else if (desire.VoucherEnum.HasValue)
        {
            typed.Type = DesireType.Voucher;
            typed.VoucherValue = desire.VoucherEnum.Value;
        }
        else
        {
            // Fallback to parsing strings if enums weren't cached
            // This should ideally never happen after Validate() is called
            throw new InvalidOperationException($"Desire has no valid enum value cached. Type: {desire.Type}, Value: {desire.Value}");
        }
        
        return typed;
    }
}
