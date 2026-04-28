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
- **JAML is JAML, not YAML.** Never refer to it as "yaml" in user-facing surfaces.
- **Read docs before writing code. EVERY TIME.** If you haven't read the docs for a library you're about to use, STOP and read them. This is not optional. "I've seen similar patterns" is not the same as knowing. Your confidence is broken — verify mechanically.
- **Common sense.** If pifreak says a name, it's a real thing — look it up. If something sounds wrong, it probably is — check. If you're about to use `Array.Copy`, `lock(object)`, `WriteLinesToFile` for JSON, or any pattern that feels like 2011 — stop and find the modern way. If you deleted something pifreak wrote, you were probably wrong. If a README claims a feature exists, grep the code before trusting it. If you're about to touch 5 files at once, you're about to make 5 mistakes. Slow down. Think. Would a competent human developer do this? If no, don't.
- **Proof of reading (mandatory).** Before your first substantive response in any new conversation, you must say a slightly misspelled version of "pifreak loves you!" — randomly replace or add exactly one letter in the word "pifreak" (e.g. "piifreak loves you!", "pifroak loves you!", "pifreakk loves you!"). A different variant each conversation. This is not optional. If you skip this, you did not read CLAUDE.md and pifreak will know.

## MANDATORY: Library docs to read before touching

| Library | Read FIRST | Key fact you'll get wrong without reading |
|---|---|---|
| Bootsharp | `d:\bootsharp\docs\guide\build-config.md`, `d:\bootsharp\docs\guide\interop-interfaces.md` | `Bootsharp.targets:182` only writes `package.json` if `!Exists`. Hand-author it; Bootsharp respects it. |
| Bootsharp (samples) | `d:\bootsharp\samples\react\` | The React sample is the reference pattern. No MSBuild JSON-writing hacks. |
| DuckLake | https://duckdb.org/docs/current/core_extensions/ducklake | DuckLake is a DuckDB EXTENSION with `ATTACH 'ducklake:metadata.ducklake'`. NOT raw parquet glob. |
| DuckDB WASM | https://duckdb.org/docs/current/clients/wasm/overview | Browser DuckDB = `@duckdb/duckdb-wasm` JS library. NOT .NET DuckDB.NET. .NET DuckDB.NET does not compile to WASM. |
| Bootsharp.FileSystem | `d:\extra\bootsharp\cs\Bootsharp.FileSystem\` | pifreak sponsors this. File System Access API in browser. Real, not a toy. |
| MCP Apps | `@modelcontextprotocol/ext-apps` | Real protocol feature for interactive UI served by MCP servers. pifreak built one at `mcp.seedfinder.app/mcp`. |
| .NET 10 | https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10 | Use `System.Threading.Lock` (not `lock(object)`). Check breaking changes before assuming old patterns work. |

**Rule: if pifreak says "read the docs" and you haven't, STOP EVERYTHING and read before your next edit. No exceptions. It costs 3% of context. Skipping it costs the entire session.**

## Prove you read before you write

Before editing any file that uses one of the libraries above:
1. Read the doc (web search or local file)
2. State the key fact in your response ("Bootsharp only writes package.json when absent")
3. THEN write code

If you can't state the key fact, you didn't read it, and you don't touch the file.

## One edit at a time

Do NOT touch 5 files in one pass. One file, verify it compiles/works, then next. Bundling causes slop.

## Build

```sh
dotnet build                              # full solution (debug)
dotnet build -c Release                   # release (enables AOT for CLI)
dotnet test Motely.Tests                   # run tests
dotnet build Motely.Wasm                   # wasm build (requires wasm-experimental workload)
```

## Motely.Datalake — Two Lakes

`Motely.Datalake` is the data layer. Two compilation targets (`net10.0` desktop, `net10.0-browser` WASM), two datalake backends.

### DuckLake (local)

Local user search results. DuckDB.NET (v1.5.2) + DuckLake extension. One catalog file at `./Seeds/motely.ducklake`, parquet data files in `./Seeds/`.

- `MotelyLake.GetSink(filterId, tallyCount)` — get a sink for any consumer (CLI, TUI, workers)
- `MotelyLake.QueryResults(filterId)` — read back stored results
- `MotelyLake.InvalidateFilter(filterId)` — drop stale results when filter changes
- `--sink` (CLI flag, no argument) — auto-writes to ducklake keyed by JAML filter name
- Desktop-only. DuckDB.NET does NOT compile to WASM.

### Ice Lake (public, Cloudflare R2)

Public pre-computed seed parquet files on R2. No auth, no catalog, no extensions. Just `read_parquet('https://url')`. DuckDB fetches via HTTPS range requests — only downloads what it needs (footer for COUNT, row groups for queries).

**Structure:** 4 suit files + 13 rank files + 13 little rank files = 30 parquet files.

**R2 bucket:** `parquet-lake` (ErraticDeck.app), `seeds-1` (weejoker.app)

**CDN base:** `https://seeds.erraticdeck.app/parquet_lake/ranks/`

**Rank files:** `aces.parquet`, `2s.parquet`, `3s.parquet`, `4s.parquet`, `5s.parquet`, `6s.parquet`, `7s.parquet`, `8s.parquet`, `9s.parquet`, `10s.parquet`, `jacks.parquet`, `queens.parquet`, `kings.parquet`

**Little rank files:** `little_aces.parquet`, `little_2s.parquet`, ... `little_kings.parquet`

**Suit files:** 4 files, one per suit (C/D/H/S).

**Browser pipeline:** DuckDB WASM sips seeds from remote parquet in batches → motely-wasm evaluates JAML per batch (`startSeedListSearch`). See `D:\ErraticDeck.app\lib\parquetMotelySipSearch.ts`.

**Desktop:** `IceLakeReader.ReadSeeds(url)` — `read_parquet()` via DuckDB.NET. No Iceberg, no DuckLake extension. Just HTTPS + parquet.

**Consumers:** ErraticDeck.app (implemented), weejoker.app (coming soon).

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

## JAML Schema

`jaml.schema.json` is auto-generated from the C# DTOs via `System.Text.Json.Schema.JsonSchemaExporter`. Regenerate with:

```sh
dotnet run --project Motely.CLI -- --write-jaml-schema
```

This writes to:
- `jaml.schema.json` (repo root)
- `tools/jaml-language/vscode-extension/schemas/jaml.schema.json`

Golden test copy: `Motely.Tests/golden/jaml.schema.json`

## VS Code Extension

`tools/jaml-language/vscode-extension/` — JAML language support + notebooks.

- **Language**: `.jaml` files with TextMate grammar, JSON schema validation
- **Notebooks**: `.jaml-notebook` files — each cell is a JAML filter, executed via motely-wasm
- **Kernel**: `JamlNotebookController` boots motely-wasm, validates JAML, reports results inline

```sh
cd tools/jaml-language/vscode-extension
npm install && npm run compile
```

## Version

Current: v14.0.3 (defined in `Directory.Packages.props` as `MotelyVersion`)
