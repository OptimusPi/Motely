using System;
using System.Collections.Generic;
using System.Linq;

namespace Motely.Filters
{
    /// <summary>
    /// Validates MotelyJsonConfig to catch errors early before searching
    /// </summary>
    public static class MotelyJsonConfigValidator
    {
        public static void ValidateConfig(MotelyJsonConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
                
            var errors = new List<string>();
            var warnings = new List<string>();
            
            // Parse stake for sticker validation
            MotelyStake stake = MotelyStake.White;
            if (!string.IsNullOrEmpty(config.Stake))
            {
                Enum.TryParse<MotelyStake>(config.Stake, true, out stake);
            }
            
            // Validate all filter items
            ValidateFilterItems(config.Must, "must", errors, warnings, stake);
            ValidateFilterItems(config.Should, "should", errors, warnings, stake);
            ValidateFilterItems(config.MustNot, "mustNot", errors, warnings, stake);
            
            // Validate deck
            if (!string.IsNullOrEmpty(config.Deck) && !Enum.TryParse<MotelyDeck>(config.Deck, true, out _))
            {
                var validDecks = string.Join(", ", Enum.GetNames(typeof(MotelyDeck)));
                errors.Add($"Invalid deck: '{config.Deck}'. Valid decks are: {validDecks}");
            }
            
            // Validate stake
            if (!string.IsNullOrEmpty(config.Stake) && !Enum.TryParse<MotelyStake>(config.Stake, true, out _))
            {
                var validStakes = string.Join(", ", Enum.GetNames(typeof(MotelyStake)));
                errors.Add($"Invalid stake: '{config.Stake}'. Valid stakes are: {validStakes}");
            }
            
            // If there are warnings, print them
            if (warnings.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("WARNINGS:");
                foreach (var warning in warnings)
                {
                    Console.WriteLine($"  ⚠️  {warning}");
                }
                Console.ResetColor();
            }
            
            // If there are errors, throw exception with all of them
            if (errors.Count > 0)
            {
                throw new ArgumentException($"INVALID CONFIGURATION:\n{string.Join("\n", errors)}");
            }
        }
        
        private static void ValidateFilterItems(List<MotelyJsonConfig.MotleyJsonFilterClause> items, string section, List<string> errors, List<string> warnings, MotelyStake stake)
        {
            if (items == null) return;
            
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var prefix = $"{section}[{i}]";
                
                // Validate type
                if (string.IsNullOrEmpty(item.Type))
                {
                    errors.Add($"{prefix}: Missing 'type' field");
                    continue;
                }
                
                // Case insensitive parsing handles all casing - no validation needed
                
                // Validate value based on type
                switch (item.Type.ToLower(System.Globalization.CultureInfo.CurrentCulture))
                {
                    case "joker":
                        // Allow wildcards: "any", "*", "AnyJoker", "AnyCommon", "AnyUncommon", "AnyRare", "AnyLegendary"
                        bool isJokerWildcard = string.IsNullOrEmpty(item.Value) || 
                                          item.Value.Equals("any", StringComparison.OrdinalIgnoreCase) ||
                                          item.Value.Equals("*", StringComparison.OrdinalIgnoreCase) ||
                                          item.Value.Equals("AnyJoker", StringComparison.OrdinalIgnoreCase) ||
                                          item.Value.Equals("AnyCommon", StringComparison.OrdinalIgnoreCase) ||
                                          item.Value.Equals("AnyUncommon", StringComparison.OrdinalIgnoreCase) ||
                                          item.Value.Equals("AnyRare", StringComparison.OrdinalIgnoreCase) ||
                                          item.Value.Equals("AnyLegendary", StringComparison.OrdinalIgnoreCase);
                        
                        if (!isJokerWildcard && !string.IsNullOrEmpty(item.Value) && !Enum.TryParse<MotelyJoker>(item.Value, true, out _))
                        {
                            var validJokers = string.Join(", ", Enum.GetNames(typeof(MotelyJoker)));
                            errors.Add($"{prefix}: Invalid joker '{item.Value}'. Valid jokers are: {validJokers}\nWildcards: any, *, AnyJoker, AnyCommon, AnyUncommon, AnyRare, AnyLegendary");
                        }
                        break;
                        
                    case "souljoker":
                        // Allow wildcards: "any", "*", "AnyJoker", "AnyCommon", "AnyUncommon", "AnyRare"
                        // Note: "AnyLegendary" is not allowed for soul jokers since all soul jokers are legendary by definition
                        bool isSoulJokerWildcard = string.IsNullOrEmpty(item.Value) || 
                                          item.Value.Equals("any", StringComparison.OrdinalIgnoreCase) ||
                                          item.Value.Equals("*", StringComparison.OrdinalIgnoreCase) ||
                                          item.Value.Equals("AnyJoker", StringComparison.OrdinalIgnoreCase) ||
                                          item.Value.Equals("AnyCommon", StringComparison.OrdinalIgnoreCase) ||
                                          item.Value.Equals("AnyUncommon", StringComparison.OrdinalIgnoreCase) ||
                                          item.Value.Equals("AnyRare", StringComparison.OrdinalIgnoreCase);
                        
                        // Special case: provide helpful error for "AnyLegendary" on soul jokers
                        if (item.Value != null && item.Value.Equals("AnyLegendary", StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add($"{prefix}: 'AnyLegendary' is not valid for soul jokers because all soul jokers are legendary by definition. Use 'any' instead.");
                        }
                        else if (!isSoulJokerWildcard && !string.IsNullOrEmpty(item.Value) && !Enum.TryParse<MotelyJoker>(item.Value, true, out _))
                        {
                            var validJokers = string.Join(", ", Enum.GetNames(typeof(MotelyJoker)));
                            errors.Add($"{prefix}: Invalid soul joker '{item.Value}'. Valid jokers are: {validJokers}\nWildcards: any, *, AnyJoker, AnyCommon, AnyUncommon, AnyRare");
                        }
                        
                        // Validate stickers using enum-based logic for performance
                        if (item.Stickers != null && item.Stickers.Count > 0)
                        {
                            foreach (var sticker in item.Stickers)
                            {
                                if (sticker != null)
                                {
                                    // Parse sticker to enum for efficient validation
                                    if (Enum.TryParse<MotelyJokerSticker>(sticker, true, out var stickerEnum))
                                    {
                                        switch (stickerEnum)
                                        {
                                            case MotelyJokerSticker.Eternal:
                                            case MotelyJokerSticker.Perishable:
                                                if (stake < MotelyStake.Black)
                                                {
                                                    warnings.Add($"{prefix}: Searching for '{sticker}' sticker will find NO RESULTS on {stake} Stake! Eternal/Perishable stickers only appear on Black Stake or higher.");
                                                }
                                                break;
                                            case MotelyJokerSticker.Rental:
                                                if (stake < MotelyStake.Gold)
                                                {
                                                    warnings.Add($"{prefix}: Searching for '{sticker}' sticker will find NO RESULTS on {stake} Stake! Rental stickers only appear on Gold Stake.");
                                                }
                                                break;
                                            case MotelyJokerSticker.None:
                                                // None is valid but doesn't require stake validation
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        var validStickers = string.Join(", ", Enum.GetNames(typeof(MotelyJokerSticker)).Where(s => s != "None").Select(s => s.ToLower(System.Globalization.CultureInfo.CurrentCulture)));
                                        errors.Add($"{prefix}: Invalid sticker '{sticker}'. Valid stickers are: {validStickers}");
                                    }
                                }
                            }
                        }
                        // Note: Legendary joker validation is no longer needed here as the auto-conversion
                        // in MotelyJsonConfig.cs automatically converts legendary jokers to souljoker type
                        
                        // Soul (legendary) jokers only produced via The Soul card in Arcana/Spectral packs – never shops.
                        if (item.Type.Equals("souljoker", StringComparison.OrdinalIgnoreCase))
                        {
                            if (item.Sources != null && item.Sources.ShopSlots != null && item.Sources.ShopSlots.Length > 0)
                            {
                                errors.Add($"{prefix}: souljoker '{item.Value ?? "(any)"}' cannot specify shopSlots; legendary jokers never appear in shops. Remove 'shopSlots'.");
                            }
                        }

                        // Enforce explicit source specification ONLY for non-soul jokers (user demanded explicit slots for shop/pack joker searches).
                        // Soul jokers may safely omit sources to mean: "any pack slot capable of producing The Soul" (original semantics).
                        if (section == "must" && !item.Type.Equals("souljoker", StringComparison.OrdinalIgnoreCase))
                        {
                            bool hasShop = item.Sources?.ShopSlots != null && item.Sources.ShopSlots.Length > 0;
                            bool hasPack = item.Sources?.PackSlots != null && item.Sources.PackSlots.Length > 0;
                            if (!hasShop && !hasPack)
                            {
                                errors.Add($"{prefix}: joker '{item.Value ?? "(any)"}' MUST clause missing explicit sources. Add 'sources.shopSlots' and/or 'sources.packSlots'.");
                            }
                        }
                        break;
                        
                    case "tarot":
                    case "tarotcard":
                        if (!string.IsNullOrEmpty(item.Value) && !Enum.TryParse<MotelyTarotCard>(item.Value, true, out _))
                        {
                            var validTarots = string.Join(", ", Enum.GetNames(typeof(MotelyTarotCard)));
                            errors.Add($"{prefix}: Invalid tarot '{item.Value}'. Valid tarots are: {validTarots}");
                        }
                        break;
                        
                    case "planet":
                    case "planetcard":
                        if (!string.IsNullOrEmpty(item.Value) && !Enum.TryParse<MotelyPlanetCard>(item.Value, true, out _))
                        {
                            var validPlanets = string.Join(", ", Enum.GetNames(typeof(MotelyPlanetCard)));
                            errors.Add($"{prefix}: Invalid planet '{item.Value}'. Valid planets are: {validPlanets}");
                        }
                        break;
                        
                    case "spectral":
                    case "spectralcard":
                        if (!string.IsNullOrEmpty(item.Value) && 
                            !item.Value.Equals("any", StringComparison.OrdinalIgnoreCase) && 
                            !item.Value.Equals("*", StringComparison.OrdinalIgnoreCase) &&
                            !Enum.TryParse<MotelySpectralCard>(item.Value, true, out _))
                        {
                            var validSpectrals = string.Join(", ", Enum.GetNames(typeof(MotelySpectralCard)));
                            errors.Add($"{prefix}: Invalid spectral '{item.Value}'. Valid spectrals are: {validSpectrals}\nUse 'any', '*', or leave empty to match any spectral.");
                        }
                        break;
                        
                    case "tag":
                    case "smallblindtag":
                    case "bigblindtag":
                        if (!string.IsNullOrEmpty(item.Value) && !Enum.TryParse<MotelyTag>(item.Value, true, out _))
                        {
                            var validTags = string.Join(", ", Enum.GetNames(typeof(MotelyTag)));
                            errors.Add($"{prefix}: Invalid tag '{item.Value}'. Valid tags are: {validTags}");
                        }
                        
                        // Validate that shopSlots and packSlots are not specified for tag filters
                        if (item.Sources != null)
                        {
                            if (item.Sources.ShopSlots != null && item.Sources.ShopSlots.Length > 0)
                            {
                                warnings.Add($"{prefix}: 'shopSlots' specified for {item.Type} filter but tags don't use shop slots. This property will be ignored.");
                            }
                            if (item.Sources.PackSlots != null && item.Sources.PackSlots.Length > 0)
                            {
                                warnings.Add($"{prefix}: 'packSlots' specified for {item.Type} filter but tags don't use pack slots. This property will be ignored.");
                            }
                        }
                        break;
                        
                    case "voucher":
                        if (!string.IsNullOrEmpty(item.Value) && !Enum.TryParse<MotelyVoucher>(item.Value, true, out _))
                        {
                            var validVouchers = string.Join(", ", Enum.GetNames(typeof(MotelyVoucher)));
                            errors.Add($"{prefix}: Invalid voucher '{item.Value}'. Valid vouchers are: {validVouchers}");
                        }
                        break;
                        
                    case "playingcard":
                        // Validate suit if specified
                        if (!string.IsNullOrEmpty(item.Suit) && !Enum.TryParse<MotelyPlayingCardSuit>(item.Suit, true, out _))
                        {
                            var validSuits = string.Join(", ", Enum.GetNames(typeof(MotelyPlayingCardSuit)));
                            errors.Add($"{prefix}: Invalid suit '{item.Suit}'. Valid suits are: {validSuits}");
                        }
                        
                        // Validate rank if specified
                        if (!string.IsNullOrEmpty(item.Rank) && !Enum.TryParse<MotelyPlayingCardRank>(item.Rank, true, out _))
                        {
                            var validRanks = string.Join(", ", Enum.GetNames(typeof(MotelyPlayingCardRank)));
                            errors.Add($"{prefix}: Invalid rank '{item.Rank}'. Valid ranks are: {validRanks}");
                        }
                        break;
                        
                    case "boss":
                    case "bossblind":
                        if (!string.IsNullOrEmpty(item.Value) && !Enum.TryParse<MotelyBossBlind>(item.Value, true, out _))
                        {
                            var validBosses = string.Join(", ", Enum.GetNames(typeof(MotelyBossBlind)));
                            errors.Add($"{prefix}: Invalid boss blind '{item.Value}'. Valid boss blinds are: {validBosses}");
                        }
                        break;
                        
                    default:
                        errors.Add($"{prefix}: Unknown type '{item.Type}'");
                        break;
                }
                
                // Validate edition if specified
                if (!string.IsNullOrEmpty(item.Edition))
                {
                    bool typeSupportsEdition = item.Type.Equals("joker", StringComparison.OrdinalIgnoreCase) ||
                                               item.Type.Equals("souljoker", StringComparison.OrdinalIgnoreCase) ||
                                               item.Type.Equals("playingcard", StringComparison.OrdinalIgnoreCase) ||
                                               item.Type.Equals("tarotcard", StringComparison.OrdinalIgnoreCase) ||
                                               item.Type.Equals("spectralcard", StringComparison.OrdinalIgnoreCase) ||
                                               item.Type.Equals("planetcard", StringComparison.OrdinalIgnoreCase);
                    if (!typeSupportsEdition)
                    {
                        errors.Add($"{prefix}: Edition specified ('{item.Edition}') but type '{item.Type}' does not support editions (remove 'edition').");
                    }
                    else if (!Enum.TryParse<MotelyItemEdition>(item.Edition, true, out _))
                    {
                        var validEditions = string.Join(", ", Enum.GetNames(typeof(MotelyItemEdition)));
                        errors.Add($"{prefix}: Invalid edition '{item.Edition}'. Valid editions are: {validEditions}");
                    }
                }
                
                // Validate antes
                // Semantics: null/missing = all antes (handled by EffectiveAntes); explicit empty array is invalid.
                if (item.Antes != null)
                {
                    if (item.Antes.Length == 0)
                    {
                        errors.Add($"{prefix}: Empty 'Antes' array (remove it to mean all antes, or specify values)");
                    }
                    else
                    {
                        foreach (var ante in item.Antes)
                        {
                            if (ante < 0 || ante > 39)
                            {
                                errors.Add($"{prefix}: Invalid ante {ante}. Must be between 0 and 39.");
                            }
                        }
                    }
                }
                
                // Validate score for should items
                if (section == "should" && item.Score <= 0)
                {
                    errors.Add($"{prefix}: Score must be positive for 'should' items (got {item.Score})");
                }
            }
        }
    }
}