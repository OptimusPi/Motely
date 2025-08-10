using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Motely
{
    public class SeedAnalyzerCapture
    {
        public class AnteData
        {
            public int Ante { get; set; }
            public string Boss { get; set; } = "";
            public MotelyVoucher Voucher { get; set; }
            public List<MotelyTag> Tags { get; set; } = new();
            public List<ShopItem> ShopQueue { get; set; } = new();
            public List<PackContent> Packs { get; set; } = new();
        }

        public class ShopItem
        {
            public int Slot { get; set; }
            public MotelyItem Item { get; set; }
            public string FormattedName { get; set; } = "";
        }

        public class PackContent
        {
            public MotelyBoosterPack PackType { get; set; }
            public List<string> Contents { get; set; } = new();
        }

        public static List<AnteData> CaptureAnalysis(string seed, MotelyDeck deck, MotelyStake stake)
        {
            var results = new List<AnteData>();

            try
            {
                // Create analyzer filter
                var filterDesc = new AnalyzerCaptureFilterDesc(results);
                var searchSettings = new MotelySearchSettings<AnalyzerCaptureFilterDesc.AnalyzerCaptureFilter>(filterDesc)
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
                Console.WriteLine($"Error analyzing seed: {ex.Message}");
            }

            return results;
        }
    }

    public struct AnalyzerCaptureFilterDesc : IMotelySeedFilterDesc<AnalyzerCaptureFilterDesc.AnalyzerCaptureFilter>
    {
        private readonly List<SeedAnalyzerCapture.AnteData> _results;

        public AnalyzerCaptureFilterDesc(List<SeedAnalyzerCapture.AnteData> results)
        {
            _results = results;
        }

        public AnalyzerCaptureFilter CreateFilter(ref MotelyFilterCreationContext ctx)
        {
            return new AnalyzerCaptureFilter(_results);
        }

        public struct AnalyzerCaptureFilter : IMotelySeedFilter
        {
            private readonly List<SeedAnalyzerCapture.AnteData> _results;

            public AnalyzerCaptureFilter(List<SeedAnalyzerCapture.AnteData> results)
            {
                _results = results;
            }

            public VectorMask Filter(ref MotelyVectorSearchContext ctx)
            {
                return ctx.SearchIndividualSeeds(CheckSeed);
            }

            public bool CheckSeed(ref MotelySingleSearchContext ctx)
            {
                // Analyze each ante
                for (int ante = 1; ante <= 8; ante++)
                {
                    var anteData = new SeedAnalyzerCapture.AnteData { Ante = ante };

                    // Boss
                    var boss = ctx.GetBossForAnte(ante);
                    anteData.Boss = boss.ToString();

                    // Voucher
                    anteData.Voucher = ctx.GetAnteFirstVoucher(ante);

                    // Tags
                    var tagStream = ctx.CreateTagStream(ante);
                    var smallTag = ctx.GetNextTag(ref tagStream);
                    var bigTag = ctx.GetNextTag(ref tagStream);
                    if (smallTag != 0) anteData.Tags.Add(smallTag);
                    if (bigTag != 0) anteData.Tags.Add(bigTag);

                    // Shop Queue
                    var shopStream = ctx.CreateShopItemStream(ante);
                    int maxSlots = ante == 1 ? 15 : 50;
                    for (int i = 0; i < maxSlots; i++)
                    {
                        var item = ctx.GetNextShopItem(ref shopStream);
                        anteData.ShopQueue.Add(new SeedAnalyzerCapture.ShopItem
                        {
                            Slot = i + 1,
                            Item = item,
                            FormattedName = FormatShopItem(item)
                        });
                    }

                    // Packs
                    var packStream = ctx.CreateBoosterPackStream(ante);
                    int packCount = ante == 1 ? 4 : 6;
                    
                    for (int i = 0; i < packCount; i++)
                    {
                        var pack = ctx.GetNextBoosterPack(ref packStream);
                        if (pack != 0)
                        {
                            var packContent = new SeedAnalyzerCapture.PackContent
                            {
                                PackType = pack,
                                Contents = GetPackContents(ref ctx, ante, pack)
                            };
                            anteData.Packs.Add(packContent);
                        }
                    }

                    _results.Add(anteData);
                }

                return false;
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
                var result = System.Text.RegularExpressions.Regex.Replace(name, "([A-Z])", " $1").Trim();
                return result;
            }

            private static string FormatTarotName(string name)
            {
                if (name.StartsWith("The"))
                    return "The " + System.Text.RegularExpressions.Regex.Replace(name.Substring(3), "([A-Z])", " $1").Trim();
                return name;
            }

            private static List<string> GetPackContents(ref MotelySingleSearchContext ctx, int ante, MotelyBoosterPack pack)
            {
                var contents = new List<string>();
                
                try
                {
                    var packType = pack.GetPackType();
                    var packSize = pack.GetPackSize();
                    
                    switch (pack)
                    {
                        case MotelyBoosterPack.Arcana:
                            var arcanaStream = ctx.CreateArcanaPackTarotStream(ante);
                            var arcanaContents = ctx.GetNextArcanaPackContents(ref arcanaStream, packSize);
                            for (int i = 0; i < arcanaContents.Length; i++)
                            {
                                var card = arcanaContents.GetItem(i);
                                var tarot = (MotelyTarotCard)(card.Value & Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask);
                                contents.Add(FormatTarotName(tarot.ToString()));
                            }
                            break;

                        case MotelyBoosterPack.Celestial:
                            var celestialStream = ctx.CreateCelestialPackPlanetStream(ante);
                            for (int i = 0; i < 3; i++)
                            {
                                var card = ctx.GetNextPlanet(ref celestialStream);
                                contents.Add(((MotelyPlanetCard)(card.Value & 0xFF)).ToString());
                            }
                            break;

                        case MotelyBoosterPack.JumboCelestial:
                            var jumboCelestialStream = ctx.CreateCelestialPackPlanetStream(ante);
                            for (int i = 0; i < 5; i++)
                            {
                                var card = ctx.GetNextPlanet(ref jumboCelestialStream);
                                contents.Add(((MotelyPlanetCard)(card.Value & 0xFF)).ToString());
                            }
                            break;

                        case MotelyBoosterPack.Spectral:
                            var spectralStream = ctx.CreateSpectralPackSpectralStream(ante);
                            for (int i = 0; i < 2; i++)
                            {
                                var card = ctx.GetNextSpectral(ref spectralStream);
                                contents.Add(((MotelySpectralCard)(card.Value & 0xFF)).ToString());
                            }
                            break;

                        case MotelyBoosterPack.JumboSpectral:
                            var jumboSpectralStream = ctx.CreateSpectralPackSpectralStream(ante);
                            for (int i = 0; i < 4; i++)
                            {
                                var card = ctx.GetNextSpectral(ref jumboSpectralStream);
                                contents.Add(((MotelySpectralCard)(card.Value & 0xFF)).ToString());
                            }
                            break;

                        case MotelyBoosterPack.Buffoon:
                            var buffoonStream = ctx.CreateBuffoonPackJokerStream(ante, 0);
                            for (int i = 0; i < 2; i++)
                            {
                                var joker = ctx.GetNextJoker(ref buffoonStream);
                                contents.Add(FormatJokerName(((MotelyJoker)(joker.Value & 0xFF)).ToString()));
                            }
                            break;

                        case MotelyBoosterPack.JumboBuffoon:
                            var jumboBuffoonStream = ctx.CreateBuffoonPackJokerStream(ante, 0);
                            for (int i = 0; i < 4; i++)
                            {
                                var joker = ctx.GetNextJoker(ref jumboBuffoonStream);
                                contents.Add(FormatJokerName(((MotelyJoker)(joker.Value & 0xFF)).ToString()));
                            }
                            break;

                        case MotelyBoosterPack.MegaBuffoon:
                            var megaBuffoonStream = ctx.CreateBuffoonPackJokerStream(ante, 0);
                            for (int i = 0; i < 2; i++)
                            {
                                var joker = ctx.GetNextJoker(ref megaBuffoonStream);
                                var jokerName = FormatJokerName(((MotelyJoker)(joker.Value & 0xFF)).ToString());
                                contents.Add($"Foil {jokerName}"); // TODO: Get actual edition
                            }
                            break;

                        case MotelyBoosterPack.Standard:
                            var standardStream = ctx.CreateStandardPackCardStream(ante);
                            for (int i = 0; i < 3; i++)
                            {
                                var card = ctx.GetNextStandardCard(ref standardStream);
                                contents.Add(FormatPlayingCard(card));
                            }
                            break;

                        case MotelyBoosterPack.JumboStandard:
                            var jumboStandardStream = ctx.CreateStandardPackCardStream(ante);
                            for (int i = 0; i < 5; i++)
                            {
                                var card = ctx.GetNextStandardCard(ref jumboStandardStream);
                                contents.Add(FormatPlayingCard(card));
                            }
                            break;

                        case MotelyBoosterPack.JumboArcana:
                            var jumboArcanaStream = ctx.CreateArcanaPackTarotStream(ante);
                            for (int i = 0; i < 5; i++)
                            {
                                var card = ctx.GetNextTarot(ref jumboArcanaStream);
                                contents.Add(FormatTarotName(((MotelyTarotCard)(card.Value & 0xFF)).ToString()));
                            }
                            break;

                        case MotelyBoosterPack.MegaArcana:
                            var megaArcanaStream = ctx.CreateArcanaPackTarotStream(ante);
                            for (int i = 0; i < 2; i++)
                            {
                                var card = ctx.GetNextTarot(ref megaArcanaStream);
                                contents.Add(FormatTarotName(((MotelyTarotCard)(card.Value & 0xFF)).ToString()));
                            }
                            break;

                        case MotelyBoosterPack.MegaCelestial:
                            var megaCelestialStream = ctx.CreateCelestialPackPlanetStream(ante);
                            for (int i = 0; i < 2; i++)
                            {
                                var card = ctx.GetNextPlanet(ref megaCelestialStream);
                                contents.Add(((MotelyPlanetCard)(card.Value & 0xFF)).ToString());
                            }
                            break;

                        case MotelyBoosterPack.MegaSpectral:
                            var megaSpectralStream = ctx.CreateSpectralPackSpectralStream(ante);
                            for (int i = 0; i < 2; i++)
                            {
                                var card = ctx.GetNextSpectral(ref megaSpectralStream);
                                contents.Add(((MotelySpectralCard)(card.Value & 0xFF)).ToString());
                            }
                            break;
                            
                        case MotelyBoosterPack.MegaStandard:
                            var megaStandardStream = ctx.CreateStandardPackCardStream(ante);
                            for (int i = 0; i < 2; i++)
                            {
                                var card = ctx.GetNextStandardCard(ref megaStandardStream);
                                contents.Add(FormatPlayingCard(card));
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    contents.Add($"Error: {ex.Message}");
                }

                return contents;
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

                // Add the card itself
                var playingCard = (MotelyPlayingCard)(card.Value & 0xFF);
                result += FormatCardName(playingCard);

                return result.Trim();
            }

            private static string FormatCardName(MotelyPlayingCard card)
            {
                var cardStr = card.ToString();
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
}