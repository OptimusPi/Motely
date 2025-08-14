using System;
using System.Collections.Generic;

namespace Motely;

public static class SeedAnalyzer
{
    public static void Analyze(string seed, MotelyDeck deck, MotelyStake stake)
    {
        try
        {
            // Create analyzer filter
            var filterDesc = new AnalyzerFilterDesc();
            var searchSettings = new MotelySearchSettings<AnalyzerFilterDesc.AnalyzerFilter>(filterDesc)
                .WithDeck(deck)
                .WithStake(stake)
                .WithListSearch(new[] { seed })
                .WithThreadCount(1);
                
            var search = searchSettings.Start();
            
            // Wait for completion
            while (search.Status == MotelySearchStatus.Running)
            {
                System.Threading.Thread.Sleep(10);
            }
            
            search.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error analyzing seed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}

public struct AnalyzerFilterDesc : IMotelySeedFilterDesc<AnalyzerFilterDesc.AnalyzerFilter>
{
    public AnalyzerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        return new AnalyzerFilter();
    }

    public struct AnalyzerFilter : IMotelySeedFilter
    {
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            // For analyzer, we just want to check individual seeds
            return ctx.SearchIndividualSeeds(CheckSeed);
        }

        public bool CheckSeed(ref MotelySingleSearchContext ctx)
        {
            var seed = ctx.GetSeed();
            Console.WriteLine($"Seed: {seed}");
            Console.WriteLine($"Deck: {ctx.Deck}, Stake: {ctx.Stake}");
            Console.WriteLine();

            // Analyze each ante
            for (int ante = 1; ante <= 8; ante++)
            {
                Console.WriteLine($"==ANTE {ante}==");

                // Boss
                var boss = ctx.GetBossForAnte(ante);
                Console.WriteLine($"Boss: {boss}");

                // Voucher
                var voucher = ctx.GetAnteFirstVoucher(ante);
                Console.WriteLine($"Voucher: {voucher}");

                // Tags
                var tagStream = ctx.CreateTagStream(ante);
                var smallTag = ctx.GetNextTag(ref tagStream);
                var bigTag = ctx.GetNextTag(ref tagStream);
                var tags = new List<string>();
                if (smallTag != 0) tags.Add(FormatTag(smallTag));
                if (bigTag != 0) tags.Add(FormatTag(bigTag));
                Console.WriteLine($"Tags: {string.Join(", ", tags)}");

                // Shop Queue
                Console.WriteLine("Shop Queue:");
                var shopStream = ctx.CreateShopItemStream(ante);
                var shopItems = new List<string>();
                
                int maxSlots = ante == 1 ? 15 : 50;
                for (int i = 0; i < maxSlots; i++)
                {
                    var item = ctx.GetNextShopItem(ref shopStream);
                    shopItems.Add($"{i + 1}) {FormatShopItem(item)}");
                }
                
                foreach (var item in shopItems)
                {
                    Console.WriteLine(item);
                }


                // Packs
                Console.WriteLine("Packs:");
                var packStream = ctx.CreateBoosterPackStream(ante);
                int packCount = ante == 1 ? 4 : 6; // Ante 1 has 4 packs, others have 6
                
                for (int i = 0; i < packCount; i++)
                {
                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    if (pack != 0)
                    {
                        ShowPackContentsInline(ref ctx, ante, pack, i);
                    }
                }

                Console.WriteLine();
            }

            return false;
        }

        private static string FormatTag(MotelyTag tag)
        {
            // Format tag name to be more readable
            var name = tag.ToString();
            if (name.EndsWith("Tag"))
                name = name.Substring(0, name.Length - 3) + " Tag";
            return name;
        }

        private static string FormatShopItem(MotelyItem item)
        {
            var result = "";
            
            // Add edition if present
            if (item.Edition != MotelyItemEdition.None)
            {
                result += item.Edition + " ";
            }
            
            switch (item.TypeCategory)
            {
                case MotelyItemTypeCategory.Joker:
                    var joker = (MotelyJoker)(item.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                    result += FormatJokerName(joker.ToString());
                    break;
                    
                case MotelyItemTypeCategory.TarotCard:
                    var tarot = (MotelyTarotCard)(item.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                    result += FormatTarotName(tarot.ToString());
                    break;
                    
                case MotelyItemTypeCategory.PlanetCard:
                    var planet = (MotelyPlanetCard)(item.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                    result += planet.ToString();
                    break;
                    
                case MotelyItemTypeCategory.SpectralCard:
                    var spectral = (MotelySpectralCard)(item.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                    result += spectral.ToString();
                    break;
                    
                    
                default:
                    result += item.Type.ToString();
                    break;
            }
            
            return result.Trim();
        }

        private static string FormatJokerName(string name)
        {
            // Convert CamelCase to spaced words
            var result = System.Text.RegularExpressions.Regex.Replace(name, "([A-Z])", " $1").Trim();
            return result;
        }

        private static string FormatTarotName(string name)
        {
            // Handle "The" prefix tarots
            if (name.StartsWith("The"))
                return "The " + System.Text.RegularExpressions.Regex.Replace(name.Substring(3), "([A-Z])", " $1").Trim();
            return name;
        }

        private static void ShowPackContentsInline(ref MotelySingleSearchContext ctx, int ante, MotelyBoosterPack pack, int packSlot)
        {
            try
            {
                var packType = pack.GetPackType();
                var packSize = pack.GetPackSize();
                var cardCount = packType.GetCardCount(packSize);
                
                switch (pack)
                {
                    case MotelyBoosterPack.Arcana:
                        var arcanaStream = ctx.CreateArcanaPackTarotStream(ante);
                        var arcanaCards = new List<string>();
                        var arcanaContents = ctx.GetNextArcanaPackContents(ref arcanaStream, packSize);
                        for (int i = 0; i < arcanaContents.Length; i++)
                        {
                            var card = arcanaContents.GetItem(i);
                            var tarot = (MotelyTarotCard)(card.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                            arcanaCards.Add(FormatTarotName(tarot.ToString()));
                        }
                        Console.WriteLine($"Arcana Pack - {string.Join(", ", arcanaCards)}");
                        break;

                    case MotelyBoosterPack.Celestial:
                        var celestialStream = ctx.CreateCelestialPackPlanetStream(ante);
                        var celestialCards = new List<string>();
                        for (int i = 0; i < 3; i++)
                        {
                            var card = ctx.GetNextPlanet(ref celestialStream);
                            celestialCards.Add(((MotelyPlanetCard)(card.Value & 0xFF)).ToString());
                        }
                        Console.WriteLine($"Celestial Pack - {string.Join(", ", celestialCards)}");
                        break;

                    case MotelyBoosterPack.JumboCelestial:
                        var jumboCelestialStream = ctx.CreateCelestialPackPlanetStream(ante);
                        var jumboCelestialCards = new List<string>();
                        for (int i = 0; i < 5; i++)
                        {
                            var card = ctx.GetNextPlanet(ref jumboCelestialStream);
                            jumboCelestialCards.Add(((MotelyPlanetCard)(card.Value & 0xFF)).ToString());
                        }
                        Console.WriteLine($"Jumbo Celestial Pack - {string.Join(", ", jumboCelestialCards)}");
                        break;

                    case MotelyBoosterPack.Spectral:
                        var spectralStream = ctx.CreateSpectralPackSpectralStream(ante);
                        var spectralCards = new List<string>();
                        for (int i = 0; i < 2; i++)
                        {
                            var card = ctx.GetNextSpectral(ref spectralStream);
                            spectralCards.Add(((MotelySpectralCard)(card.Value & 0xFF)).ToString());
                        }
                        Console.WriteLine($"Spectral Pack - {string.Join(", ", spectralCards)}");
                        break;

                    case MotelyBoosterPack.JumboSpectral:
                        var jumboSpectralStream = ctx.CreateSpectralPackSpectralStream(ante);
                        var jumboSpectralCards = new List<string>();
                        for (int i = 0; i < 4; i++)
                        {
                            var card = ctx.GetNextSpectral(ref jumboSpectralStream);
                            jumboSpectralCards.Add(((MotelySpectralCard)(card.Value & 0xFF)).ToString());
                        }
                        Console.WriteLine($"Jumbo Spectral Pack - {string.Join(", ", jumboSpectralCards)}");
                        break;

                    case MotelyBoosterPack.Buffoon:
                        var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante, packSlot);
                        var buffoonJokers = new List<string>();
                        for (int i = 0; i < 2; i++)
                        {
                            var joker = ctx.GetNextJoker(ref buffoonStream);
                            buffoonJokers.Add(FormatJokerName(((MotelyJoker)(joker.Value & 0xFF)).ToString()));
                        }
                        Console.WriteLine($"Buffoon Pack - {string.Join(", ", buffoonJokers)}");
                        break;

                    case MotelyBoosterPack.JumboBuffoon:
                        var jumboBuffoonStream = ctx.CreateBuffoonPackJokerStream(ante, packSlot);
                        var jumboBuffoonJokers = new List<string>();
                        for (int i = 0; i < 4; i++)
                        {
                            var joker = ctx.GetNextJoker(ref jumboBuffoonStream);
                            jumboBuffoonJokers.Add(FormatJokerName(((MotelyJoker)(joker.Value & 0xFF)).ToString()));
                        }
                        Console.WriteLine($"Jumbo Buffoon Pack - {string.Join(", ", jumboBuffoonJokers)}");
                        break;

                    case MotelyBoosterPack.MegaBuffoon:
                        var megaBuffoonStream = ctx.CreateBuffoonPackJokerStream(ante, packSlot);
                        var megaBuffoonJokers = new List<string>();
                        // Mega packs show 5 jokers, player can pick 2
                        for (int i = 0; i < 5; i++)
                        {
                            var joker = ctx.GetNextJoker(ref megaBuffoonStream);
                            var jokerName = FormatJokerName(((MotelyJoker)(joker.Value & 0xFF)).ToString());
                            // Check if this joker has an edition (not guaranteed in Mega packs)
                            var edition = joker.Edition;
                            var editionStr = edition switch
                            {
                                MotelyItemEdition.Foil => "Foil",
                                MotelyItemEdition.Holographic => "Holographic",
                                MotelyItemEdition.Polychrome => "Polychrome",
                                MotelyItemEdition.Negative => "Negative",
                                _ => ""
                            };
                            megaBuffoonJokers.Add(!string.IsNullOrEmpty(editionStr) ? $"{editionStr} {jokerName}" : jokerName);
                        }
                        Console.WriteLine($"Mega Buffoon Pack (pick 2) - {string.Join(", ", megaBuffoonJokers)}");
                        break;

                    case MotelyBoosterPack.Standard:
                        var standardStream = ctx.CreateStandardPackCardStream(ante);
                        var standardCards = new List<string>();
                        for (int i = 0; i < 3; i++)
                        {
                            var card = ctx.GetNextStandardCard(ref standardStream);
                            standardCards.Add(FormatPlayingCard(card));
                        }
                        Console.WriteLine($"Standard Pack - {string.Join(", ", standardCards)}");
                        break;

                    case MotelyBoosterPack.JumboStandard:
                        var jumboStandardStream = ctx.CreateStandardPackCardStream(ante);
                        var jumboStandardCards = new List<string>();
                        for (int i = 0; i < 5; i++)
                        {
                            var card = ctx.GetNextStandardCard(ref jumboStandardStream);
                            jumboStandardCards.Add(FormatPlayingCard(card));
                        }
                        Console.WriteLine($"Standard Pack - {string.Join(", ", jumboStandardCards)}");
                        break;

                    case MotelyBoosterPack.JumboArcana:
                        var jumboArcanaStream = ctx.CreateArcanaPackTarotStream(ante);
                        var jumboArcanaCards = new List<string>();
                        for (int i = 0; i < 5; i++)
                        {
                            var card = ctx.GetNextTarot(ref jumboArcanaStream);
                            jumboArcanaCards.Add(FormatTarotName(((MotelyTarotCard)(card.Value & 0xFF)).ToString()));
                        }
                        Console.WriteLine($"Jumbo Arcana Pack - {string.Join(", ", jumboArcanaCards)}");
                        break;

                    case MotelyBoosterPack.MegaArcana:
                        var megaArcanaStream = ctx.CreateArcanaPackTarotStream(ante);
                        var megaArcanaCards = new List<string>();
                        for (int i = 0; i < 2; i++)
                        {
                            var card = ctx.GetNextTarot(ref megaArcanaStream);
                            megaArcanaCards.Add(FormatTarotName(((MotelyTarotCard)(card.Value & 0xFF)).ToString()));
                        }
                        Console.WriteLine($"Mega Arcana Pack - {string.Join(", ", megaArcanaCards)}");
                        break;

                    case MotelyBoosterPack.MegaCelestial:
                        var megaCelestialStream = ctx.CreateCelestialPackPlanetStream(ante);
                        var megaCelestialCards = new List<string>();
                        for (int i = 0; i < 2; i++)
                        {
                            var card = ctx.GetNextPlanet(ref megaCelestialStream);
                            megaCelestialCards.Add(((MotelyPlanetCard)(card.Value & 0xFF)).ToString());
                        }
                        Console.WriteLine($"Mega Celestial Pack - {string.Join(", ", megaCelestialCards)}");
                        break;

                    case MotelyBoosterPack.MegaSpectral:
                        var megaSpectralStream = ctx.CreateSpectralPackSpectralStream(ante);
                        var megaSpectralCards = new List<string>();
                        for (int i = 0; i < 2; i++)
                        {
                            var card = ctx.GetNextSpectral(ref megaSpectralStream);
                            megaSpectralCards.Add(((MotelySpectralCard)(card.Value & 0xFF)).ToString());
                        }
                        Console.WriteLine($"Mega Spectral Pack - {string.Join(", ", megaSpectralCards)}");
                        break;
                        
                    case MotelyBoosterPack.MegaStandard:
                        var megaStandardStream = ctx.CreateStandardPackCardStream(ante);
                        var megaStandardCards = new List<string>();
                        for (int i = 0; i < 2; i++)
                        {
                            var card = ctx.GetNextStandardCard(ref megaStandardStream);
                            megaStandardCards.Add(FormatPlayingCard(card));
                        }
                        Console.WriteLine($"Mega Standard Pack - {string.Join(", ", megaStandardCards)}");
                        break;
                        
                    default:
                        Console.WriteLine($"{pack} - [Contents not implemented]");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Error reading pack contents: {ex.Message}");
            }
        }

        private static string FormatPlayingCard(MotelyItem card)
        {
            var result = "";

            // Add seal if present
            if (card.Seal != MotelyItemSeal.None)
            {
                result += card.Seal.ToString().Replace("Seal", "") + " Seal ";
            }

            // Add edition if present  
            if (card.Edition != MotelyItemEdition.None)
            {
                result += card.Edition + " ";
            }

            // Add enhancement if present
            if (card.Enhancement != MotelyItemEnhancement.None)
            {
                result += card.Enhancement + " ";
            }

            // Add the card itself (e.g., "Ace of Spades")
            var playingCard = (MotelyPlayingCard)(card.Value & 0xFF);
            result += FormatCardName(playingCard);

            return result.Trim();
        }

        private static string FormatCardName(MotelyPlayingCard card)
        {
            var cardStr = card.ToString();
            // Convert C2 -> 2 of Clubs, HA -> Ace of Hearts, etc.
            if (cardStr.Length >= 2)
            {
                var suit = cardStr[0] switch
                {
                    'C' => "Clubs",
                    'D' => "Diamonds",
                    'H' => "Hearts",
                    'S' => "Spades",
                    _ => "Unknown"
                };

                var rank = cardStr.Substring(1) switch
                {
                    "2" => "2",
                    "3" => "3",
                    "4" => "4",
                    "5" => "5",
                    "6" => "6",
                    "7" => "7",
                    "8" => "8",
                    "9" => "9",
                    "10" => "10",
                    "J" => "Jack",
                    "Q" => "Queen",
                    "K" => "King",
                    "A" => "Ace",
                    _ => cardStr.Substring(1)
                };

                return $"{rank} of {suit}";
            }
            return cardStr;
        }
    }
    
}