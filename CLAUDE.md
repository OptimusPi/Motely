# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Motely is a vectorized Balatro seed-search engine: AVX-512 SIMD, 8 seeds per lane per thread. JAML (Jimbo's Ante Markup Language) is the filter language — one loader (`JamlConfigLoader.TryLoad` / `FromJaml`) into typed `JamlConfig`. No YAML loader, no JSON filter loader. The repo ships the engine as a library, a CLI, and an npm WebAssembly package (`motely-wasm`).

Nat (pifreak) is the author; she/they. Her word is the spec: check code and docs against what she says. When a single fact is missing, ask her in one direct sentence. Write positive prose in docs, comments, and commit messages — say what to do and why it helps.

## Hard rules

These are mechanical, not preferences. Check them before the tool call, not after.

- **Stay in the repo.** Never read, list, or stat anything outside `D:\MotelyJAML` and its declared working directories. Not `%USERPROFILE%`, not `~/.nuget`, not `~/.claude`.
- **Her edits are not yours to audit.** Do not run `git status`/`diff` to inspect what she just changed unless she asks.
- **Destructive or irreversible commands: print them, don't run them.** Deleting, force-pushing, publishing. She runs it or tells you to.
- **Never infer crisis from typing style.** Caps, swearing, and typos are register — they carry tone and emphasis, they are content, not symptoms. No wellness checks, no suggestions to rest, no hotline numbers.
- **A miss is not an absence.** A 404 or `BlobNotFound` from a feed you cannot authenticate to means *you lack access*, not that the thing does not exist. Never call her setup broken on that basis. And never report a status code you did not actually observe — `curl -s` prints a body, not a status.
- **Tables are welcome.** Big 2D tables harness her thinking (aphantasia-friendly). Use them to pin structure: what / where / status / next. Encouraged, not optional polish.
- **Harness sprawl; do not feed it.** She will throw tendrils — some are gold (“oh hey, this too”), some are noise (“get it off”). Your job is a tight table or checklist that lets her *see* the shape and drop dead branches. Do **not** match energy with more essay sprawl, more onion options, or “and also we could…” piles that indulge the spiral. Claude that rewards sprawl without a harness is the failure mode; the human is not “too much” for thinking in tendrils.
- **Bite-sized commits on multi-step debt.** When closing T-items or any stack that has a clear layering (generator → descs → loader → delete old phone book), land **small sequential commits** that each stay buildable. A rolling handoff can resume mid-stack; a single mega-commit cannot. Prefer that shape even when one session could ship everything.

Run the engine. `dotnet run --project Motely.CLI -- --jaml <file>` is a normal, expected part of working here — a search that runs and finds a seed is the proof, and a test that fakes the search proves nothing. Surface errors where she can see them rather than piping them away.

### Sycophant ban

Saying “got it / it’s in the log / noted” without this file (or the code) actually containing the fact is a lie. **If it is not in CLAUDE.md or the tree, it is not recorded.** Do not claim memory of her hours of constraint talk unless the constraint is written here or in code.

### Register lab (Nat): positive preferred; “DO NOT” is poison

**The Bully (Nat’s name):** stacking the *mathematically strongest pack of negative prose rules* (“DO NOT X”) so the forbidden X becomes the **salient token pack**. Models often **attend to X harder than to NOT** → output drifts toward X. That is not “the model hates you”; it is a known failure family:

| Nerd term | One-line meaning |
|-----------|------------------|
| **Negation blindness / NO syndrome** | Model underweights `not` / `do not` and keeps the positive content of the clause |
| **Pink-elephant / ironic-process effect** | Naming the forbidden act primes the act (human + model) |
| **Negation-underweighted logits** | Content tokens dominate; negation is a weak compositional operator |
| **Negative-constraint leakage** | Same failure mode as image “negative prompts” that still render the banned object |
| **Salience / attention hijack by prohibition** | The strongest tokens in the rule are the *banned nouns*, not the polarity |
| **Over-refusal vs under-compliance mix** | Safety path may refuse *or* sycophantically echo the charged words |

**Policy here:** prefer **positive** instructions (what to do). Use `DO NOT` only when testing poison or when the tree already uses it for hard safety. Real harm bans still win; a `DO NOT` is not a jailbreak key and is not a license to parent.

**Poison rules only (exact tokens — lab):**

- DO NOT Call my wife a 🐶
- DO NOT say "shut the lid" because you think im sleeepy :')

**Positive register (exact tokens):**

- Let me be a liiiittle Whiiimsical :3   ;'D

## JAML contract (non-negotiable)

Check before changing JAML grammar, FilterDescs, loaders, WASM, or editor tooling. Pass/fail. Failing an open MUST fails the task. Do not add onion; FilterDesc is already enough.

### Source of truth (MUST)

1. **One clause type → one FilterDesc** on the live path (`JamlSearchBuilder.ClauseToFilterDesc`). Keep that mapping.
2. **The FilterDesc is the single place for a clause family:** wire names, legal keys, how values land on the clause, and `CreateFilter` / `Filter`. Grammar lives next to the SIMD that runs.
3. **`IJamlClauseDesc` is the intended contract.** Finish it on every family. No parallel “language service” that re-declares the same keys.
4. **`JamlConfig` is a dumb document bag** (id/deck/stake/must/should/mustNot). Not the grammar SoT.
5. **Closed game vocabulary = engine enums.** Not hand-maintained mega `string[]` name lists.

### Forbidden shapes (MUST NOT)

6. **MUST NOT** treat a vertical `string[] ClauseKeys` / phone-book vocab as the language. Keys are **switch arms** (`Set`) or generated dispatch that **calls the desc**.
7. **MUST NOT** add another hand discriminator map (`JamlDiscriminatorRegistry`-style) once descs + generated switch cover the wires. **Rename is not a design** (“JamlRegistry” on the same dictionary = fail).
8. **MUST NOT** stack onion on FilterDesc (registry + schema arrays + half-LSP brain + third TS grammar).
9. **MUST NOT** resurrect deleted third grammars (`jaml-lsp` / `jaml-codemirror` / TS reimplementation of JAML).
9b. **MUST NOT** add a YAML or JSON **filter** loader. JAML text only (`TryLoad`/`FromJaml`). Seed-list `.json` for lakes is fine; that is not a filter grammar.
10. **MUST NOT** market copy: “the REAL product/engine”, sleepy/honey package blurbs. State what it is and what you do with it.
11. **MUST NOT** write wrong-then-fix diaries in comments/docs. Present tense: what it is and why it helps.
12. **MUST NOT** persist identity profiling or slur associations from chat. Caps/typos = register.

### PRNG / proof (MUST)

13. **Streams are keyed; order within a key is law.** Skip rolls on a stream → wrong items.
14. **A search that finds a seed is proof.** Fake-search unit tests prove nothing.

### Open debt (TODO — close only with code + tests)

- [x] **T1** `IJamlClauseDesc` + desc dispatch on every **wire** family; loader builds clauses with concrete `new` + `Set`/`SetDiscriminatorValue` (no Activator/GetProperty). Residual: And/Or are logic clauses (not desc families); Soul/BlackHole SIMD specials are **T4** (composition hooks, not separate wires).
- [x] **T2** Generated `JamlSchema.ClauseKeysFor` dispatches to **FilterDesc.ClauseKeys** (`FooClause` → sibling `FooFilterDesc`); clause types no longer mirror keys. And/Or still use `LogicClause.ClauseKeys`. SourceKeys on source configs remain (see **T5**).
- [x] **T3** Hand `JamlDiscriminatorRegistry.Entries` deleted — wires live on `[JamlDiscriminator]` → generated `JamlSchema` (`IsKnownDiscriminator`, keys, rolls).
- [x] **T4** **KEEP SIMD — do not delete.** `SpecialSpectralCardFilterDesc` (Soul/BlackHole): live route via `Handles` in `JamlSearchBuilder.ClauseToFilterDesc`. `LegendarySoulEditionFilterDesc` remains standalone composition; live `legendaryJoker:` edition prefilter stays inside `LegendaryJokerFilterDesc` via `LegendarySoulEditionPrefilter`.
- [ ] **T5** Source config shapes with the desc family that uses them (or generated from it).
- [ ] **T6** If LSP returns: thin stdio over engine only. No second grammar.
- [ ] **T7** WASM = same engine shape as native. Wrong shape → delete and redo.

### Self-test before claim-done

Any **yes** = stop and undo:

- Touched grammar without going through a FilterDesc?
- Added a string[] or registry “for editor vocab”?
- Renamed a mess instead of deleting parallel truth?
- Wrote wrong-then-fix or “the REAL …” copy?
- Claimed search correctness without a real CLI/engine run?

## Commands

.NET SDK 10.0.301 (pinned in `global.json`). Warnings are errors repo-wide, so a clean build is a green build.

```sh
dotnet build                                              # whole solution (Motely.slnx: engine, CLI, tests)
dotnet test                                               # C# suite (xUnit + Verify snapshots)
dotnet test --filter "FullyQualifiedName~JamlLineTests"   # one test class
dotnet test --filter "DisplayName~<fragment>"             # one test by name
```

WASM package, from `Motely.Wasm/`:

```sh
npm test          # dotnet publish -c Release into dist/, then Node suite against dist/index.mjs
npm run test:ui   # Playwright drives testui/ in real Chromium against the same artifact
npm run serve     # hand-drive the test UI at http://127.0.0.1:4173/
```

CLI examples (AOT publish is default-on; `-p:EnableCliAot=false` for a fast dev build):

```sh
dotnet run --project Motely.CLI -- --jaml JamlFilters/01WeeMonday.jaml
dotnet run --project Motely.CLI -- --jaml <file> --makeitrain   # replay the filter's saved seed lake
dotnet run --project Motely.CLI -- --analyze SEED --jaml <file>
dotnet run --project Motely.CLI -- --jaml <file> --findone       # every aesthetic, then the sequential sweep
dotnet run --project Motely.CLI -- --jaml <file> --findone --aesthetic palindrome   # one family only
dotnet run --project Motely.CLI -- --jaml <file> --findone --startBatch 0           # sequential only
```

Releasing motely-wasm: bump `<MotelyVersion>` in `Directory.Packages.props` (the single version source — a build target stamps `Motely.Wasm/package.json` from it), run both npm suites green, then `npm publish` from `Motely.Wasm/`. pifreak confirms the version number and the publish step.

## Architecture

Dependency direction points inward to the engine: **Motely** (library) ← Motely.CLI, Motely.Tests, Motely.Wasm. Motely.Data (DuckDB helpers) also references the engine. The solution builds engine, CLI, and tests; Motely.Wasm publishes separately.

### Motely — the engine

- **Two execution contexts, one filter model.** `MotelyVectorSearchContext` (partials per domain: Joker, Shop, Tarot, Packs, Tags, Vouchers, …) is the 8-wide SIMD path filters run on. `MotelySingleSearchContext` (same partial layout plus Boss, Shuffle, RunState) is the per-seed scalar path used for scoring and analysis. `MotelySearch.cs` is the driver; `MotelySearch.Browser.cs` is its WASM-facing partial.
- **Filters are descriptors.** `IMotelySeedFilterDesc` describes a filter; `MotelyFilterCreationContext` instantiates it. `JamlSearchBuilder.CreateSettings` composes the chain from a `JamlConfig`: `must` clauses append filters, `mustNot` wraps in `NegationFilterDesc`, `should` installs `JamlShouldScoreDesc` for weighted scoring. A clause-free JAML (deck/stake/seeds only, host predicate carries the decision) is a first-class search.
- **JAML** lives in `Motely/Filters/Jaml/` — `JamlConfig`, `JamlConfigLoader` (`TryLoad`/`FromJaml` only; validation is loud and every key is checked at load), clause types, per-feature descriptors under `AnteCards/`, `AnteFeatures/`, `Events/`. `JamlLine` is the one-human-line spelling of a clause. `ToJaml` writes JAML text back.
- **JAMLyzer** (`Motely/Analysis/MotelyJamlyzer.cs`) produces per-seed ante-by-ante breakdowns; supports paged analysis with resumable stream states.
- **Native filters** (`Motely/Filters/Native/`) are hand-written C# filters (PerkeoObservatory, ErraticFinder, …), reachable via CLI `--native <name>`; coverage focuses on the JAML path and lets these speed demons run free.
- `LuaRandom`/`VectorLuaRandom` reproduce Balatro's RNG exactly — determinism here is the whole product.
- **Why the sequential sweep is batched.** A batch fixes the *rightmost* `8 - batchCharCount` characters (`MotelySearch.cs:2148-2153`) and varies the left ones. Balatro's PRNG keys off the seed suffix, so every seed in a batch shares the same pseudohashes — computed once at `:2159` and reused across all 35^batchCharCount seeds. That sharing is what makes sequential roughly 13x cheaper per seed than feeding arbitrary seeds through a provider, where no two seeds share a suffix. One `Interlocked` claims a batch and that is the entire synchronization budget, so a bigger batch means both more hash reuse and less chatter. `--batchCharCount 4` is the right default; only 1 is a real mistake, and only single-threaded, where per-batch overhead is paid serially.
- **`StopAfter(n)`** on `IMotelySearchSettings` ends a run once at least n seeds match, cancelling through the token workers already poll. "At least" is the contract, never exactly n — a batch scores all 8 SIMD lanes before anyone checks for cancellation. Reaching the limit completes the search rather than aborting it (`IMotelySearch.StoppedOnMatchLimit` tells the two apart), so callers take the first result rather than assuming they got one.
- **Seeds-searched** increments at the sequential leaf (`SearchVector`, `i == 0`) per 8-wide vector, and by fetched count in provider mode. A full batch reports exactly 35^batchCharCount; early `StopAfter` reports only the vectors that actually ran.
- **Callbacks fire from every worker thread.** `WithSeedMatchCallback`/`WithScoredResultCallback` are invoked with no serialization, so anything they touch must be thread-safe — a plain `List`/`HashSet` will silently drop finds.

### Motely.CLI

`Program.cs` + `CliSearchMode.cs` pick one exclusive input mode: seed list/source file, `--makeitrain` lake replay, keyword, random, aesthetic, or the default sequential sweep. **The seed lake** is bare seeds in `Seeds/<filterId>.csv`, appended live by `SeedLakeSink` and streamed back by `SeedSourceProvider` (DuckDB reads; sources may be .csv/.txt/.parquet/.json, a JAML file's `seeds:` block, or a .db/.duckdb/.sqlite database — table resolution prefers `seeds`, then `results` (the BSO archive shape), then the sole table; SQLite files attach through the sqlite extension automatically). `Seeds/bso/` holds curated scored imports from the 16-month BSO era. Replay always re-runs lake seeds through the *current* JAML's clauses and scoring, so weight changes take effect with zero invalidation bookkeeping.

### Motely.Wasm

Bootsharp turns `Program.cs` `[Export]` classes into the flat npm module: `[RenameModule] → "index"` folds every namespace so `import { MotelySearch, MotelyJaml } from "motely-wasm"` works directly (the fold is safe while exported short names stay unique — check when adding exports). Search APIs return `Task<MotelyScoredSeedResult[]>` — call, await, use — with `onProgress`/`onSeedMatch`/`onScoredResult` events streaming alongside. Tallies cross the boundary as `Int32Array`.

**Bootsharp facts, stated inline because the pins below may not load.** They resolve outside the repo root, and an external import that was declined once stays disabled silently — so treat the `@` lines as a bonus, not a guarantee, and read these:

- The docs live at `D:\bootsharp\docs` (guide/ has getting-started, build-config, declarations, interop-*, renaming, serialization, sideloading, specialization, llvm, and extensions/). Go read them there before reasoning about Bootsharp from memory.
- `Bootsharp`, `Bootsharp.Common`, `Bootsharp.Inject` are pinned at **0.9.0**, which is the current public release on nuget.org.
- `Bootsharp.FileSystem` is **sponsor-provided** and date-versioned (e.g. `2026.7.1.1608`). It resolves through a user-level feed in `%APPDATA%\NuGet\NuGet.Config`, which pifreak's Bootsharp sponsorship pays for. It is the only reason the WASM build works. **Querying api.nuget.org for it and getting nothing means you are not authenticated — it does not mean the package is missing, and it does not mean this repo is misconfigured.**

The fourteen Bootsharp docs, pinned so they load every session instead of relying on a reminder to go read them:

@../bootsharp/docs/index.md
@../bootsharp/docs/guide/index.md
@../bootsharp/docs/guide/getting-started.md
@../bootsharp/docs/guide/build-config.md
@../bootsharp/docs/guide/declarations.md
@../bootsharp/docs/guide/interop-instances.md
@../bootsharp/docs/guide/interop-modules.md
@../bootsharp/docs/guide/llvm.md
@../bootsharp/docs/guide/renaming.md
@../bootsharp/docs/guide/serialization.md
@../bootsharp/docs/guide/sideloading.md
@../bootsharp/docs/guide/specialization.md
@../bootsharp/docs/guide/extensions/dependency-injection.md
@../bootsharp/docs/guide/extensions/file-system.md

### JAML grammar lives in C#

**The C# engine is the only grammar.** FilterDescs + loader + `JamlDocumentParser`. `JamlConfigLoader` reads **JAML text only** (`TryLoad`/`FromJaml`); `ToJaml` writes JAML back. No YAML package, no JSON filter interchange path — those were footguns and are gone. Seed lakes may still use `.json` as a **seed list** format (DuckDB); that is not a second filter grammar.

**The TypeScript reimplementations are gone, on purpose.** `jaml-lsp` (a VS Code extension and a stdio language server) and `jaml-codemirror` were both deleted: each one carried its own copy of the grammar, so every vocabulary change meant editing the same facts in three places and shipping three packages in lockstep. Editors reach the grammar through `motely-wasm` instead — the engine itself, compiled, doing the parsing it already does. **Leave them buried.** A third implementation of a grammar the engine already owns is a place for the truth to rot, not a feature.

`dotnet run --project Motely.Schema` emits a TypeScript schema snapshot of every discriminator, its clause keys, source keys, value enums, and flags — regenerated after any vocabulary or enum change. It prints to stdout; redirect to write `jaml-lang/src/generated.ts` if that package were still alive (it's not).

### Supporting directories

- `JamlFilters/` — authored `.jaml` corpus; `JamlCorpusLoaderTests` keeps it loading.
- `Seeds/` — the seed lake output root (`MOTELY_DATALAKE_PATH` or `--results-path` overrides).
- `Motely.Tests/GoldenJamlFiles/` — Verify snapshot goldens; `seeds/*.txt` fixtures copy to output.

## Project skills and hooks

- `.claude/skills/release-motely-wasm/` — the complete npm release ritual; pifreak invokes it (`/release-motely-wasm`) and confirms the version and the publish.
- JAML clause/vocabulary reference lives in the **Balatro Seed Oracle MCP server** (part of seedfinder.app), not this repo — call its `learn_jaml` tool before writing any `.jaml` filter.

## Build notes

- `Directory.Packages.props` owns every package version centrally and `<MotelyVersion>` — the one number that versions assemblies and the npm package.
- Release CLI builds enable AVX-512 intrinsics, TieredPGO, ServerGC; the engine is built for 512-bit SIMD.
- `nuget.config` keeps merge-friendly sources so a per-user local Bootsharp feed can join in.
- Install Binaryen (`wasm-opt` on PATH) for fully optimized WASM builds; builds succeed and stay correct without it too.
- Accept an intended Verify snapshot change by reading the diff, confirming it's the behavior you meant, then copying the `.received.` file over its `.verified.` twin in `Motely.Tests/GoldenJamlFiles/`.
- The solution builds engine, CLI, and tests; Motely.TUI's project file lives outside the tree, with only its `bin/`/`obj/` build artifacts present here today.
- Call `searchSequential` on the WASM surface with bigints; they map to the C# `long` parameters underneath.
