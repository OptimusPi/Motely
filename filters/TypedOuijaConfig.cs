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
        public int DesireByAnte;
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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchesJoker(MotelyJoker joker, MotelyItemEdition edition)
        {
            if (Type != DesireType.Joker && Type != DesireType.SoulJoker)
                return false;
                
            if (joker != JokerValue)
                return false;
                
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
        var deck = MotelyDeck.RedDeck;
        var stake = MotelyStake.WhiteStake;
        
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
            DesireByAnte = desire.DesireByAnte,
            SearchAntes = desire.SearchAntes ?? Array.Empty<int>(),
            Score = desire.Score,
            RequiredEdition = MotelyItemEdition.None
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
