# Ouija/Motely/Genie Project Overview
*Generated from Claude conversation on January 2025*

## 🎯 Project Summary

This is a complete ecosystem for Balatro seed searching that evolved from the original C/OpenCL Ouija implementation to a modern, KISS-compliant C#/.NET solution with natural language interface.

## 🏗️ Architecture Evolution

### Original Ouija (C/OpenCL)
- GPU-accelerated seed searching
- Manual memory management
- Complex Python/Pandas UI
- Required OpenCL drivers

### Your Improvements (Motely - C#/.NET)
- **SIMD/AVX-512** vectorization instead of GPU
- **Type-safe** enum system
- **JSON configuration** with simple schema
- **Direct CSV output** - no intermediate layers
- **Rock-solid** memory management with .NET

## 📁 Project Structure

```
X:/
├── Ouija/              # Original C/OpenCL implementation
├── Motely/             # Your C#/.NET SIMD fork
│   ├── MotelyVectorSearchContext.cs    # AVX-512 vectorized search
│   ├── filters/OuijaJsonFilterDesc.cs  # JSON config processing
│   ├── JsonItemFilters/*.ouija.json    # Example configs
│   └── schema.ouija.json               # Config schema
├── BalatroSeedOracle/  # Avalonia UI application
│   └── src/            # MVVM architecture, clean separation
└── Genie/              # Natural language interface
    ├── ouija-genie/    # Cloudflare Worker (AI → JSON)
    └── the-fool/       # Backend API service

```

## 🚀 Key Innovations

### 1. **KISS JSON Configuration**
```json
{
  "MaxSearchAnte": 2,
  "Deck": "RedDeck", 
  "Stake": "WhiteStake",
  "Needs": [
    { "Type": "SoulJoker", "Value": "Perkeo", "SearchAntes": [1, 2] }
  ],
  "Wants": []
}
```
- No over-engineering
- Strings parsed to enums once at load time
- Fast enum comparisons in hot path

### 2. **Smart Vectorization Strategy**
```csharp
// Vectorizable operations (tags, vouchers, some shop items)
var shopPlanetMask = searchContext.FilterPlanetCard(ante, need.PlanetEnum.Value, MotelyPrngKeys.Shop);

// Non-vectorizable operations (soul jokers, packs)
bool foundInAnyAnte = ProcessLegendaryJokerFromSoul(ref singleCtx, need.JokerEnum.Value, ante);
```

### 3. **Direct Output Pipeline**
```csharp
// No abstraction layers, just direct CSV output
FancyConsole.WriteLine(result.ToCsvRow());
```

### 4. **Natural Language Interface (Genie)**
- **balatrogenie.app** - Live production site!
- Cloudflare Worker converts plain English to JSON configs
- Beautiful retro UI with scanlines and pixel fonts
- Examples:
  - "I want seeds with Wee Joker and good economy"
  - "Find me dice joker on Blue Deck with Gold Stake"
  - "I need Blueprint and Baron combo by ante 4"

## 💪 Performance Improvements

### From OpenCL to SIMD
- **No GPU driver issues** - runs on any modern CPU
- **AVX-512 vectorization** - Process 8 seeds simultaneously
- **Type safety** - Catch errors at compile time
- **Memory efficiency** - .NET garbage collection

### Smart Caching
```csharp
// Cache PRNG streams for requested antes
ctx.CachePseudoHash(MotelyPrngKeys.JokerCommon + "sho" + ante);
ctx.CachePseudoHash(MotelyPrngKeys.JokerUncommon + "sho" + ante);
```

## 🎨 UI/UX Philosophy

### KISS Principles Throughout
1. **No callback hell** - Direct method calls
2. **No abstraction layers** - Code does what it says
3. **Clear data flow** - Input → Process → Output
4. **User-friendly** - Natural language instead of JSON editing

### Example Anti-Pattern (Avoided)
```csharp
// BAD - Over-engineered
def handle_button_click():
    controller.handle_click()
    # which calls...
    manager.process_click()
    # which calls...
    handler.execute_click()

// GOOD - Direct and clear
def start_search():
    config = self.get_search_config()
    if self.validate_config(config):
        self.search_model.start_search(config)
```

## 🔧 Technical Details

### Enum System
- All Balatro items (jokers, tarots, etc.) as enums
- Parse once from strings, compare as integers
- Type-safe and performant

### Vector Lane Management
```csharp
public readonly bool IsLaneValid(int lane)
{
    // Handle partial vectors correctly
    if (SeedFirstCharactersLength == SeedLength)
        return lane == 0;
    return ((double*)&SeedLastCharacters[0])[lane] != '\0';
}
```

### Search Flow
1. User enters natural language (Genie)
2. AI converts to JSON config
3. Config validated and enums parsed
4. Vector search for filterable items
5. Individual search for complex items
6. Results streamed as CSV

## 🎯 Future Considerations

### Completed
- ✅ SIMD vectorization
- ✅ JSON configuration
- ✅ Natural language interface
- ✅ Live web deployment

### Potential Enhancements
- [ ] Vectorize shop generation
- [ ] Vectorize pack contents
- [ ] Add more complex filters
- [ ] Distributed search across multiple machines

## 🙏 Credits & Philosophy

This project embodies KISS (Keep It Simple, Stupid) principles:
- **Simple** JSON configs instead of complex DSLs
- **Direct** code paths without abstraction layers
- **Clear** separation of concerns
- **Fast** execution with modern CPU features

The evolution from Ouija → Motely → Genie shows how starting with a solid foundation (Ouija's algorithms) and applying modern techniques (SIMD, type safety, AI) can create something both powerful and user-friendly.

---

*"pifreak loves you!"* - And the Balatro community loves what you've built! 🃏✨
