# MotelyJAML — Claude Code Context

## What This Is

Motely is the Balatro seed search engine. It analyzes seeds for jokers, vouchers, tarots, spectrals, bosses, tags, and standard cards across antes 1-8. JAML (Jimbo's Ante Markup Language) is the YAML-based filter language that defines search criteria.

**pifreak** (npm: pifreak, GitHub: OptimusPi) is the sole maintainer. This is a git submodule of `X:\JammySeedFinder`.

## Project Structure

| Project | Purpose | Target |
|---|---|---|
| `Motely` | Core engine — SIMD seed search, JAML parser, analysis | `net10.0` + `net10.0-browser` |
| `Motely.CLI` | Command-line searcher (AOT-compiled) | `net10.0` |
| `Motely.TUI` | Terminal.Gui interactive UI | `net10.0` |
| `Motely.DB` | DuckDB/DuckLake result storage, seed sources/sinks | `net10.0` |
| `Motely.Wasm` | Browser/JS WASM build via Bootsharp | `net10.0` (wasm-experimental) |
| `Motely.Tests` | xUnit tests, golden files in `GoldenJamlFiles/` | `net10.0` |
| `Motely.HelperAPI` | ASP.NET helper API | `net10.0` |
| `Motely.DistributedWorker` | Distributed search worker | `net10.0` |
| `Motely.DB.Browser` | Browser-side DuckDB interop (separate from native) | — |

## Critical Rules

- **Motely is the source of truth.** If a consumer references a non-existent Motely API, the consumer is wrong. Never add fake APIs to Motely.
- **No glue layers for WASM.** Export Motely itself. Never build wrapper facades that duplicate logic.
- **No Mono. No Blazor.** `Motely.Wasm` uses `dotnet workload wasm-experimental` + Bootsharp (v0.8.0-alpha.74, `D:\bootsharp`). Single .mjs ESM file. Node/Deno/Bun/browser compatible. No P/Invoke, no pthreads, single-threaded.
- **Consent before coding.** When uncertain, read the code, ask ONE question, do NOT speculatively edit.
- **All Balatro/JAML UI components live in jaml-ui.** Never build themed React components in consumer apps.

## Build

```sh
dotnet build                              # full solution (debug)
dotnet build -c Release                   # release (enables AOT for CLI)
dotnet test Motely.Tests                   # run tests
dotnet build Motely.Wasm                   # wasm build (requires wasm-experimental workload)
```

## DuckDB / DuckLake

`Motely.DB` uses DuckDB.NET (v1.5.2) with the DuckLake extension. Results are stored as Parquet files in `./Seeds/ducklake/`.

- `MotelyLake.GetSink(filterId, tallyCount)` — get a sink for any consumer (CLI, TUI, future Avalonia)
- `settings.WithSeedSink(sink)` — extension method to wire sink into search settings
- `--sink` (CLI flag, no argument) — auto-writes to ducklake keyed by JAML filter name
- DuckDB is desktop-only. WASM/browser does not use DuckDB.

## JAML — The Filter Language

**THE PARSER IS THE SPEC.** The source of truth is `Motely/Filters/Jaml/JamlConfig.cs` and `JamlConfigLoader.RawParse.cs`. If this document contradicts the parser, the parser wins.

### Top-Level Keys

`id`, `name`, `author`, `dateCreated`, `description`, `deck`, `stake`, `defaults`, `must`, `should`, `mustNot`, `aesthetics`, `hashtags`, `seeds`

At least one of `must`, `should`, or `mustNot` is required.

### Defaults Block

```yaml
defaults:
  antes: [1, 2, 3]
  boosterPacks: [0, 1, 2]
  shopItems: [0, 1]
  score: 5
```

### Clause Structure

Each clause has EXACTLY ONE discriminator key + optional shared properties.

### Discriminator Keys

**Jokers** (all support `any` wildcard via `EnumOrAny`):

- `joker` / `jokers` — any rarity
- `commonJoker` / `commonJokers` — common only
- `uncommonJoker` / `uncommonJokers` — uncommon only
- `rareJoker` / `rareJokers` — rare only
- `mixedJoker` / `mixedJokers` — alias for joker (any rarity)
- `soulJoker` / `legendaryJoker` — legendary jokers

**Items:**

- `voucher` / `vouchers`
- `tarot` / `tarotCard`
- `spectral` / `spectralCard`
- `planet` / `planetCard`

**Blinds/Tags:**

- `boss`
- `tag` — defaults to small blind position
- `smallBlindTag`
- `bigBlindTag`

**Standard cards:**

- `standardCard` — value is a card shorthand: `C2`..`CA`, `D2`..`DA`, `H2`..`HA`, `S2`..`SA` (suit initial + rank). NOT "King", NOT "Ace", NOT display names.
- `erraticRank` — match rank in erratic deck
- `erraticSuit` — match suit in erratic deck
- `erraticCard` — match exact card in erratic deck
- `startingDraw` — match starting hand card

**Events:**

- `event` — explicit event type

### Shared Clause Properties

```yaml
antes: [1, 2, 3]           # which antes to check (default: all 8)
score: 10                   # points in should section (default: 1)
min: 2                      # minimum matches required (default: 1)
edition: Polychrome         # Base|Foil|Holographic|Polychrome|Negative
enhancement: Steel          # Bonus|Mult|Wild|Glass|Steel|Stone|Gold|Lucky
seal: Red                   # Gold|Red|Blue|Purple
stickers: [Eternal]         # [Eternal, Perishable, Rental]
rank: K                     # 2-9, 10/T, J, Q, K, A (for card clauses)
suit: H                     # C/D/H/S or Clubs/Diamonds/Hearts/Spades
```

### Wildcard

Joker discriminators accept `any` (case-insensitive) as a wildcard:

```yaml
must:
  - commonJoker: any
    edition: Negative
    antes: [1]
```

`standardCard` does NOT use `EnumOrAny`. Writing `standardCard: any` causes a parse-failure fallback where rank and suit are both null (matches any card). Combined with `rank:` this can filter by rank only, but this is accidental behavior — not an intentional API.

### Enum Values (PascalCase, no spaces, no punctuation)

**Decks:** Red, Blue, Yellow, Green, Black, Magic, Nebula, Ghost, Abandoned, Checkered, Zodiac, Painted, Anaglyph, Plasma, Erratic

**Stakes:** White, Red, Green, Black, Blue, Purple, Orange, Gold

**Joker names use PascalCase with no spaces:**
LuckyCat (not "Lucky Cat"), MrBones (not "Mr. Bones"), OopsAll6s (not "Oops! All 6s"), RideTheBus, HitTheRoad, WalkieTalkie, SmileyFace, HalfJoker, DriversLicense, Cloud9, ToTheMoon

**Tarots use "The" prefix:**
TheFool, TheMagician, TheHighPriestess, TheEmpress, TheEmperor, TheHierophant, TheLovers, TheChariot, Justice, TheHermit, TheWheelOfFortune, Strength, TheHangedMan, Death, Temperance, TheDevil, TheTower, TheStar, TheMoon, TheSun, Judgement, TheWorld

**Spectrals:** Familiar, Grim, Incantation, Talisman, Aura, Wraith, Sigil, Ouija, Ectoplasm, Immolate, Ankh, DejaVu, Hex, Trance, Medium, Cryptid, TheSoul, BlackHole

**Vouchers:** Overstock, OverstockPlus, ClearanceSale, Liquidation, Hone, GlowUp, RerollSurplus, RerollGlut, CrystalBall, OmenGlobe, Telescope, Observatory, Grabber, NachoTong, Wasteful, Recyclomancy, TarotMerchant, TarotTycoon, PlanetMerchant, PlanetTycoon, SeedMoney, MoneyTree, Blank, Antimatter, MagicTrick, Illusion, Hieroglyph, Petroglyph, DirectorsCut, Retcon, PaintBrush, Palette

**Tags:** UncommonTag, RareTag, NegativeTag, FoilTag, HolographicTag, PolychromeTag, InvestmentTag, VoucherTag, BossTag, StandardTag, CharmTag, MeteorTag, BuffoonTag, HandyTag, GarbageTag, EtherealTag, CouponTag, DoubleTag, JuggleTag, D6Tag, TopupTag, SpeedTag, OrbitalTag, EconomyTag

**Bosses:** TheArm, TheClub, TheEye, TheFish, TheFlint, TheGoad, TheHead, TheHook, TheHouse, TheManacle, TheMark, TheMouth, TheNeedle, TheOx, ThePillar, ThePlant, ThePsychic, TheSerpent, TheTooth, TheWall, TheWater, TheWheel, TheWindow, AmberAcorn, CeruleanBell, CrimsonHeart, VerdantLeaf, VioletVessel

**Planets:** Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus, Neptune, Pluto, PlanetX, Ceres, Eris

### What NOT To Do In JAML

These are real mistakes from previous AI sessions:

- `standardCard: King` — WRONG. Use `HK`, `SK`, `CK`, `DK`
- `standardCard: BlueSeal` — WRONG. BlueSeal is not a card
- `soulJoker: Triboulet` — VALID (alias for legendaryJoker), but prefer `legendaryJoker`
- `mixedJoker: Any` — VALID (alias for joker), but prefer `joker`
- Wrong rarity: ALWAYS verify against the enums in `Motely/Enums/MotelyJokers.cs`. Example: Photograph IS common (`MotelyJokerCommon`), Astronomer and Satellite ARE uncommon (`MotelyJokerUncommon`). Don't guess rarity — read the enum.

## Version

Current: v14.0.3 (defined in `Directory.Packages.props` as `MotelyVersion`)
