using System.Runtime.CompilerServices;

namespace Motely;

/// <summary>
/// Shop generation streams - maintains all PRNG state for a complete shop
/// </summary>
public ref struct MotelySingleShopStream
{
    public int Ante;
    public MotelySinglePrngStream CardTypePrng;
    public MotelySinglePrngStream RarityPrng;
    public MotelySinglePrngStream CommonPrng;
    public MotelySinglePrngStream UncommonPrng;
    public MotelySinglePrngStream RarePrng;
    public MotelySinglePrngStream EditionPrng;
    public MotelySinglePrngStream PlanetPrng;
    public MotelySinglePrngStream TarotPrng;
    // TODO: Add sticker streams when ready
}

/// <summary>
/// Complete shop state for an ante
/// </summary>
public struct ShopState
{
    public const int ShopSlots = 6;
    public const int ShopSlotsAnteOne = 4; 

    [InlineArray(ShopSlots)]
    public struct ShopItems
    {
        public ShopItem Item;
    }
    
    public ShopItems Items;
    
    public struct ShopItem
    {
        public ShopItemType Type;
        public MotelyItem Item;
        
        // Type-specific data
        public MotelyJoker Joker;
        public MotelyJokerRarity JokerRarity;
        public MotelyPlanetCard Planet;
        public MotelyTarotCard Tarot;
        public MotelyItemEdition Edition;
        // TODO: Stickers
        
        public enum ShopItemType : byte
        {
            Empty,
            Joker,
            Planet,
            Tarot
        }
    }
}

unsafe ref partial struct MotelySingleSearchContext
{
    #if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    #endif
    public MotelySingleShopStream CreateShopStream(int ante)
    {
        const string source = "sho"; // Shop source constant
        
        return new MotelySingleShopStream
        {
            Ante = ante,
            CardTypePrng = CreatePrngStream(MotelyPrngKeys.CardType + ante),
            RarityPrng = CreatePrngStream(MotelyPrngKeys.JokerRarity + ante + source),
            CommonPrng = CreatePrngStream(MotelyPrngKeys.JokerCommon + source + ante),
            UncommonPrng = CreatePrngStream(MotelyPrngKeys.JokerUncommon + source + ante),
            RarePrng = CreatePrngStream(MotelyPrngKeys.JokerRare + source + ante),
            EditionPrng = CreatePrngStream(MotelyPrngKeys.JokerEdition + source + ante),
            PlanetPrng = CreatePrngStream(MotelyPrngKeys.Planet + MotelyPrngKeys.Shop + ante),
            TarotPrng = CreatePrngStream(MotelyPrngKeys.Tarot + MotelyPrngKeys.Shop + ante)
        };
    }
    
    #if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    #endif
    public ShopState GenerateFullShop(int ante)
    {
        var stream = CreateShopStream(ante);
        return GenerateFullShop(ref stream);
    }
    
    #if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    #endif
    public ShopState GenerateFullShop(ref MotelySingleShopStream stream)
    {
        ShopState shop = new();
        
        const double totalRate = 28; // 20 joker + 4 tarot + 4 planet

        for (int slot = 0; slot < ShopState.ShopSlots; slot++)
        {
            ref var item = ref shop.Items[slot];

            double cardTypeRoll = GetNextRandom(ref stream.CardTypePrng) * totalRate;

            if (cardTypeRoll < 20)
            {
                // Joker
                item.Type = ShopState.ShopItem.ShopItemType.Joker;
                GenerateShopJoker(ref stream, ref item);
            }
            else if (cardTypeRoll < 24)
            {
                // Tarot
                item.Type = ShopState.ShopItem.ShopItemType.Tarot;
                GenerateShopTarot(ref stream, ref item);
            }
            else
            {
                // Planet
                item.Type = ShopState.ShopItem.ShopItemType.Planet;
                GenerateShopPlanet(ref stream, ref item);
            }
            // TODO only generate Spectral Cards if deck is Ghost Deck
        }
        
        return shop;
    }
    
    #if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    #endif
    private void GenerateShopJoker(ref MotelySingleShopStream stream, ref ShopState.ShopItem item)
    {
        // Generate rarity
        double rarityRoll = GetNextRandom(ref stream.RarityPrng);
        MotelyJokerRarity rarity;
        if (rarityRoll > 0.95)
            rarity = MotelyJokerRarity.Rare;
        else if (rarityRoll > 0.7)
            rarity = MotelyJokerRarity.Uncommon;
        else
            rarity = MotelyJokerRarity.Common;
        
        item.JokerRarity = rarity;
        
        // Generate specific joker
        switch (rarity)
        {
            case MotelyJokerRarity.Common:
                var commonChoice = GetNextRandomElement(ref stream.CommonPrng, MotelyEnum<MotelyJokerCommon>.Values);
                item.Joker = (MotelyJoker)((int)MotelyJokerRarity.Common + (int)commonChoice);
                break;
                
            case MotelyJokerRarity.Uncommon:
                var uncommonChoice = GetNextRandomElement(ref stream.UncommonPrng, MotelyEnum<MotelyJokerUncommon>.Values);
                item.Joker = (MotelyJoker)((int)MotelyJokerRarity.Uncommon + (int)uncommonChoice);
                break;
                
            case MotelyJokerRarity.Rare:
                var rareChoice = GetNextRandomElement(ref stream.RarePrng, MotelyEnum<MotelyJokerRare>.Values);
                item.Joker = (MotelyJoker)((int)MotelyJokerRarity.Rare + (int)rareChoice);
                break;
        }
        
        // Generate edition
        double editionRoll = GetNextRandom(ref stream.EditionPrng);
        if (editionRoll > 0.997)
            item.Edition = MotelyItemEdition.Negative;
        else if (editionRoll > 0.994)
            item.Edition = MotelyItemEdition.Polychrome;
        else if (editionRoll > 0.98)
            item.Edition = MotelyItemEdition.Holographic;
        else if (editionRoll > 0.96)
            item.Edition = MotelyItemEdition.Foil;
        else
            item.Edition = MotelyItemEdition.None;
        
        // Set the MotelyItem
        item.Item = new MotelyItem(item.Joker, item.Edition);
        
        // TODO: Generate stickers when logic is ready
    }
    
    #if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    #endif
    private void GenerateShopPlanet(ref MotelySingleShopStream stream, ref ShopState.ShopItem item)
    {
        var planet = GetNextRandomElement(ref stream.PlanetPrng, MotelyEnum<MotelyPlanetCard>.Values);
        item.Planet = planet;
        item.Edition = MotelyItemEdition.None;
        var planetType = (MotelyItemType)((int)MotelyItemTypeCategory.PlanetCard | (int)planet);
        item.Item = planetType;  // Use implicit conversion
    }
    
    #if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    #endif
    private void GenerateShopTarot(ref MotelySingleShopStream stream, ref ShopState.ShopItem item)
    {
        var tarot = GetNextRandomElement(ref stream.TarotPrng, MotelyEnum<MotelyTarotCard>.Values);
        item.Tarot = tarot;
        item.Edition = MotelyItemEdition.None;
        var tarotType = (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)tarot);
        item.Item = tarotType;  // Use implicit conversion
    }
}
