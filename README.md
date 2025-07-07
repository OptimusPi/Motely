# Motely - Dynamic Balatro Seed Search

A high-performance SIMD CPU-based Balatro seed searcher with dynamic JSON configuration support.

## 🚀 Quick Start

### Basic Usage
```bash
# Search with your config
Motely.exe --config test.ouija.json

# Search with custom parameters
Motely.exe --config enhanced_test.ouija.json --threads 8 --startBatch 0 --endBatch 1000

# Quick test (fewer batches)
Motely.exe --config test.ouija.json --endBatch 100
```

### Command Line Options
- `--config <path>`: Path to Ouija JSON config file (default: test.ouija.json)
- `--threads <count>`: Number of search threads (default: CPU core count)
- `--startBatch <index>`: Starting batch index (default: 0)
- `--endBatch <index>`: Ending batch index, -1 for unlimited (default: 1000)
- `--batchSize <chars>`: Batch character count, 2-4 recommended (default: 3)

## 📋 Supported Filter Types

### NEEDS (All must match - early exit on failure)
- **SmallBlindTag**: First tag of the ante
- **BigBlindTag**: Second tag of the ante
- **Joker**: Regular joker from shop
- **PlayingCard**: Standard playing card (rank/suit/enhancement)
- **SpectralCard**: Spectral card
- **TarotCard**: Tarot card
- **PlanetCard**: Planet card

### WANTS (Scored and counted)
- **SmallBlindTag**: Tag scoring
- **BigBlindTag**: Tag scoring
- **Joker**: Joker with optional edition
- **PlayingCard**: Playing card with rank/suit/enhancement
- **SpectralCard**: Spectral card
- **TarotCard**: Tarot card
- **PlanetCard**: Planet card

> **Note:** Only the following values are valid for `type`: `Joker`, `PlayingCard`, `SpectralCard`, `TarotCard`, `PlanetCard`. Any other value (e.g., `Joker`, `Standard_Card`, `Baron`) will be ignored and reported as invalid.

## 🔧 Configuration Format

### Basic Example (test.ouija.json)
```json
{
  "name": "basic_test",
  "description": "Basic negative tag + hanging chad search",
  "filter_config": {
    "numNeeds": 1,
    "numWants": 1,
    "Needs": [
      {
        "type": "SmallBlindTag",
        "value": "NegativeTag",
        "searchAntes": [2, 3, 4, 5, 6, 7, 8]
      }
    ],
    "Wants": [
      {
        "type": "Joker",
        "value": "HangingChad",
        "edition": "None",
        "searchAntes": [1, 2, 3, 4, 5, 6]
      }
    ],
    "deck": "AnaglyphDeck",
    "stake": "BlackStake"
  }
}
```

### Advanced Example (enhanced_test.ouija.json)
```json
{
  "filter_config": {
    "Needs": [
      {
        "type": "SmallBlindTag",
        "value": "NegativeTag", 
        "searchAntes": [2, 3, 4]
      },
      {
        "type": "Joker",
        "value": "HangingChad",
        "searchAntes": [1]
      }
    ],
    "Wants": [
      {
        "type": "Joker",
        "value": "Perkeo",
        "edition": "Negative",
        "searchAntes": [1, 2, 3]
      },
      {
        "type": "Joker", 
        "value": "Canio",
        "searchAntes": [2, 3]
      },
      {
        "type": "Standard_Card",
        "rank": "A",
        "suit": "Spades", 
        "enchantment": "Gold",
        "searchAntes": [1, 2]
      }
    ]
  }
}
```

## 🎯 Filter Properties

### Tag Filters
- `type`: "SmallBlindTag" or "BigBlindTag"
- `value`: Tag name (e.g., "NegativeTag", "RareTag", "FoilTag")
- `searchAntes`: Array of antes to check (e.g., [1, 2, 3])

### Joker Filters  
- `type`: "Joker" or "Joker"
- `value`: Joker name (e.g., "HangingChad", "Perkeo", "Canio")
- `edition`: Edition type ("None", "Negative", "Foil", "Holographic", "Polychrome")
- `searchAntes`: Array of antes to check

### Card Filters
- `type`: "Standard_Card"
- `rank`: Card rank ("A", "2"-"10", "J", "Q", "K")
- `suit`: Card suit ("Spades", "Hearts", "Diamonds", "Clubs")
- `enchantment`: Enhancement ("Bonus", "Mult", "Wild", "Glass", "Steel", "Stone", "Gold", "Lucky")
- `searchAntes`: Array of antes to check

## 🎪 Available Values

### Tags
`UncommonTag`, `RareTag`, `NegativeTag`, `FoilTag`, `HolographicTag`, `PolychromeTag`, `InvestmentTag`, `VoucherTag`, `BossTag`, `StandardTag`, `CharmTag`, `MeteorTag`, `BuffoonTag`, `HandyTag`, `GarbageTag`, `EtherealTag`, `CouponTag`, `DoubleTag`, `JuggleTag`, `D6Tag`, `TopupTag`, `SpeedTag`, `OrbitalTag`, `EconomyTag`

### Jokers
All Balatro jokers are supported. Full list:

- **Common**: `Joker`, `GreedyJoker`, `LustyJoker`, `WrathfulJoker`, `GluttonousJoker`, `JollyJoker`, `ZanyJoker`, `MadJoker`, `CrazyJoker`, `DrollJoker`, `SlyJoker`, `WilyJoker`, `CleverJoker`, `DeviousJoker`, `CraftyJoker`, `HalfJoker`, `CreditCard`, `Banner`, `MysticSummit`, `_8Ball`, `Misprint`, `RaisedFist`, `ChaosTheClown`, `ScaryFace`, `AbstractJoker`, `DelayedGratification`, `GrosMichel`, `EvenSteven`, `OddTodd`, `Scholar`, `BusinessCard`, `Supernova`, `RideTheBus`, `Egg`, `Runner`, `IceCream`, `Splash`, `BlueJoker`, `FacelessJoker`, `GreenJoker`, `Superposition`, `ToDoList`, `Cavendish`, `RedCard`, `SquareJoker`, `RiffRaff`, `Photograph`, `ReservedParking`, `MailInRebate`, `Hallucination`, `FortuneTeller`, `Juggler`, `Drunkard`, `GoldenJoker`, `Popcorn`, `WalkieTalkie`, `SmileyFace`, `GoldenTicket`, `Swashbuckler`, `HangingChad`, `ShootTheMoon`

- **Uncommon**: `JokerStencil`, `FourFingers`, `Mime`, `CeremonialDagger`, `MarbleJoker`, `LoyaltyCard`, `Dusk`, `Fibonacci`, `SteelJoker`, `Hack`, `Pareidolia`, `SpaceJoker`, `Burglar`, `Blackboard`, `SixthSense`, `Constellation`, `Hiker`, `CardSharp`, `Madness`, `Seance`, `Vampire`, `Shortcut`, `Hologram`, `Cloud9`, `Rocket`, `MidasMask`, `Luchador`, `GiftCard`, `TurtleBean`, `Erosion`, `ToTheMoon`, `StoneJoker`, `LuckyCat`, `Bull`, `DietCola`, `TradingCard`, `FlashCard`, `SpareTrousers`, `Ramen`, `Seltzer`, `Castle`, `MrBones`, `Acrobat`, `SockAndBuskin`, `Troubadour`, `Certificate`, `SmearedJoker`, `Throwback`, `RoughGem`, `Bloodstone`, `Arrowhead`, `OnyxAgate`, `GlassJoker`, `Showman`, `FlowerPot`, `MerryAndy`, `OopsAll6s`, `TheIdol`, `SeeingDouble`, `Matador`, `Stuntman`, `Satellite`, `Cartomancer`, `Astronomer`, `BurntJoker`, `Bootstraps`

- **Rare**: `DNA`, `Vagabond`, `Baron`, `Obelisk`, `BaseballCard`, `AncientJoker`, `Campfire`, `Blueprint`, `WeeJoker`, `HitTheRoad`, `TheDuo`, `TheTrio`, `TheFamily`, `TheOrder`, `TheTribe`, `Stuntman`, `InvisibleJoker`, `Brainstorm`, `DriversLicense`, `BurntJoker`

- **Legendary**: `Canio`, `Triboulet`, `Yorick`, `Chicot`, `Perkeo`

### Editions
`None`, `Foil`, `Holographic`, `Polychrome`, `Negative`

### Stickers
`Perishable`, `Eternal`, `Rental`

## 📊 Output Format

Results are printed as CSV:
```
Seed,TotalScore,WeeJoker,HangingChad,Perkeo
ABCD1234,3,1,1,1
EFGH5678,2,1,0,1
```

## 🔍 Validation & Testing
```csharp
OuijaValidator.ValidateConfig("test.ouija.json");
```

### Quick Test Search  
```csharp
OuijaValidator.TestSearch("test.ouija.json", maxBatches: 10);
```

## ⚡ Performance Tips

1. **Batch Size**: Use 3-4 character batches for best performance
2. **Thread Count**: Set to your CPU core count or slightly higher
3. **NEEDS vs WANTS**: Put strict requirements in NEEDS for early exit
4. **Ante Ranges**: Minimize ante ranges to reduce computation

## 🐛 Troubleshooting

### Config Not Found
- Check file path and extension (.ouija.json)
- Try placing in `ouija_configs/` directory
- Use absolute path if needed

### No Results
- Reduce filter strictness
- Check ante ranges (1-8 are valid)
- Validate joker/tag names match exactly

### Performance Issues
- Reduce batch count for testing
- Lower thread count if system becomes unresponsive
- Check if SSD vs HDD affects I/O

## 🏗️ Architecture Notes

- **SIMD CPU**: Outperforms GPU due to LuaRandom complexity
- **Vector Processing**: 8 seeds processed simultaneously with early exit
- **Dynamic Loading**: No recompilation needed for new filters
- **Memory Efficient**: Optimized hash stream caching

The SIMD CPU approach beats GPU because Balatro's LuaRandom and hash stream setup have significant overhead that CPUs handle much better than GPUs.
