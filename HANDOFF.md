# Handoff — `feat/es-modules` schema cleanup
**Last touched:** 2026-05-11 evening (Central time).
**Branch:** `feat/es-modules` (pushed to origin).
**Repo owner:** pifreak.

> Next Claude — read `AGENTS.md` first (canon), then `memory/MEMORY.md` (cross-session lessons), then this file (resumable state).

---

## TL;DR

- Schema cleanup steps 1 + 2 + 2b are done and pushed.
- A critical PRNG-key corruption from an earlier IDE find-replace has been reverted (see commit `a9bf9aa9` — that one was the load-bearing fix of the night).
- Test suite is **419/420 passing**. The one remaining failure is the pre-existing keyword-padding count drift, and a haiku sub-agent has been dispatched to fix it (its commit may already be in `git log` by the time you read this).
- Steps 3, 4, 5, 6 of the cleanup plan are still open. **Step 3 and 4 are small and behavior-preserving — start there.** Step 5 is the big DTO eradication and has a real regression net now (`JamlCorpusRegressionTests` with 184 fixtures).

---

## Commits this session (chronological, all on `feat/es-modules`)

| Hash | Type | Summary |
|---|---|---|
| `689e938a` | feat(wasm) | `Program.Analyze` single-seed export + `analyzer.mjs` Node harness + promote `MotelyJamlyzerHighlights` to public |
| `fa7124f7` | test(jaml) | Seed `JamlCorpusRegressionTests` with 184 fixtures from `seedfinder.app/data/filters`, expand `LegacyKeys` for bot-confuser keys (`packSlots`, `tarot`, `planet`, `spectral`, `erraticRanks`) |
| `855f8d3d` | style(jaml) | Collapse stray blank lines in `JamlSearchBuilder.cs` (902→716 lines) |
| `3ae93c91` | refactor(jaml) | Move `AndFilterDesc`/`OrFilterDesc` from `Motely.Filters.Jaml` to `Motely.Filters` (drop ugly fully-qualified types in dispatcher) |
| `67ff65dd` | refactor(jaml) | **Step 2b — polymorphic clause dispatch.** New `JamlClause` abstract base + `RollClause` + `LogicClause`. Delete 4 parallel switches in `JamlSearchBuilder.cs` (CreateDesc / EstimateClauseCost / GetMaxAnte / DescribeClause). |
| `a9bf9aa9` | **fix(prng)** | **Revert IDE find-replace damage** on Spectral/Tarot/Planet identifiers across 28 C# files. The PRNG const string values at `Motely/MotelyPrngKeys.cs` HEAD were already correct — the damage was in consumer files. See commit body. |
| *pending* | fix(keywords) | Haiku agent dispatched to strip 1–3 letter keywords + refresh baked count. Local commit only, not pushed yet. |

---

## Schema cleanup plan — status

The full plan (from the original session conversation), with status:

1. ✅ **Format `JamlSearchBuilder.cs`** — done in `855f8d3d`. Pure whitespace.
2. ✅ **Polymorphic clause methods + And/Or namespace move** — done in `3ae93c91` + `67ff65dd`. The four per-clause switches in `JamlSearchBuilder.cs` are gone; `clause.CreateDesc()` / `clause.EstimatedCost` / `clause.Describe()` / `clause.MaxAnte` are virtual on the concrete clause types.
3. ⏭ **Drop `JamlClauseSet`'s 28 typed lists, keep only `OrderedClauses`.** Behavior-preserving — nothing downstream reads the typed snapshots; `JamlSearchBuilder.CreatePlan` iterates `OrderedClauses` and uses the new polymorphic dispatch. Loader (`JamlConfigLoader.AddClauseToSet`) writes to both. Just drop the typed-list writes + the field declarations on `JamlClauseSet`. Quick win.
4. ⏭ **Delete dead `JokerSource` type** in `Motely/Filters/Jaml/JamlConfig.cs` (lines 74-89 in the pre-cleanup file, the type with the `// oops :( I have a complaint!` comment). Nothing reads it.
5. ⏭ **Custom `IYamlTypeConverter` for clauses, delete `JamlClauseDto`.** Invasive. The new converter must preserve the **strict-key rejection** behavior the four `Unknown*Key_IsRejected` tests in `JamlConfigTests.cs` lock in (see `JamlConfigLoader.RawParse.cs:35-38` comment for the v13/v14 false-positive history this protects). Regression net = `JamlCorpusRegressionTests` (184 fixtures + 98 legacy-rejection cases). **Run that suite green before AND after.**
6. ⏭ **Singular → plural alias deprecation** (`joker` → `jokers`, etc.). One-deprecation-cycle alias table at the parser. Out of scope until step 5 is done.

**My recommendation for the next session:** do steps 3 and 4 first (tiny, behavior-preserving), then take a fresh head into step 5. Don't bundle step 5 with anything else.

---

## Outstanding pre-session dirty files (do NOT bundle with cleanup)

`git status` will show these still modified — they were dirty before this session started and were intentionally left untouched here. **Do not stage them as part of any cleanup commit.**

```
M  AGENTS.md                            ← pre-session edits, untouched
D  BOOTSHARP_DOCS.md                    ← pre-session deletion
M  Directory.Packages.props
M  Motely.CLI/Program.cs                ← may have mixed pre-session + revert content
M  Motely.Tests/Motely.Tests.csproj
M  Motely.slnx
M  Motely/Filters/Jaml/JamlConfigLoader.Models.cs   ← pre-session edits;
                                          its [YamlMember(Alias = "tarotCard")] etc.
                                          ARE intentional JAML schema keys, do not revert
M  Motely/Motely.csproj
M  Motely/MotelySearch.cs               ← may have mixed pre-session + revert content
M  Motely.Tests/JamlConfigTests.cs      ← deleted TryLoadFromPath_ResolvesTrimmedMixedCasePaths
                                          test in working tree (bot-broken, name doesn't
                                          match any real method) — uncommitted on purpose
M  nuget.config
?? lots of untracked: .claude/, JamlFilters/*, Seeds/, clean.ps1, etc.
```

For `Motely.CLI/Program.cs` and `Motely/MotelySearch.cs` specifically: they were dirty pre-session AND the PRNG revert may have touched them. Use `git add -p` to split the revert content from the pre-session work if you want to land the revert portion separately. Or leave for the user — the engine is functional either way.

---

## What was learned (engine-level)

- **Balatro engine doesn't enforce gameplay reachability.** That belongs to the frontend layer (LSP, jaml-ui, VS Code extension). The engine respects `earlyAntesMaxPack` as the ONLY opt-in for ante-1 slot-4/5 reachability; everything else is a permissive PRNG walk. Don't add reachability heuristics to the engine.
- **Ante range 0–39 is "fair game" for searches.** Hieroglyph/Petroglyph let you reach ante 0 in real gameplay (`game.ante = game.ante - 1`). Ante 8 is the win-boss; endless mode goes further; ante 39 hits NaN×10^Infinity float-overflow and is unwinnable. Beyond 39 is "custom" / officially bug territory.
- **JAML = Jimbo's Ante Markup Language.** Jimbo is the mascot nickname for joker #0 (the common Joker).
- **`BossClause` is correctly scalar.** Don't try to vectorize it — boss state-machine (cached array + voucher activation interplay) doesn't SIMD well. Its `SearchIndividualSeeds` scalar lambda is the right design, not a TODO. (I got this wrong once mid-session; the user corrected me.)
- **`uncommonJoker:` is supposed to be source-scoped** — only checks the uncommon-rarity PRNG streams (fast SIMD path), not the all-joker streams. A prior bot half-implemented this and the specialization is leaky. Real perf bug for a SIMD-tuning workstream, not a schema workstream.

---

## What's open / parked

- **`ExplainPlan` SIMD-strategy badge.** Idea: per-FilterDesc `FilterStrategy { Vectorized | HybridVectorized | Scalar }` exposed on `IMotelySeedFilterDesc`, printed inline in `ExplainPlan` next to cost. Useful for surfacing which clauses are slow. Discussed, not built. Defer to after step 5.
- **Per-ante early-exit on min=1 SIMD path.** If all 8 lanes have matched within one ante's loop, break out (`if (mask == AllOnes) break;`). Free perf win for the common `must:` case. Per-FilterDesc edit; touches `Filter()` bodies, so do it in a SIMD-tuning sweep with golden tests gated.
- **`JamlCorpusRegressionTests` class name still contains "corpus".** Rename to `JamlFixtureRegressionTests` (or similar — see `memory/feedback_vocabulary.md`). Don't bundle with structural work.
- **The 2 fixture files `legendary-perkeo.jaml` and `Zerkeo.jaml` are byte-identical duplicates** in `Motely.Tests/GoldenJamlFiles/`. Trim one when you're tidying.
- **DescribeClause output strings in Step 2b** use PascalCase (e.g. `"Spectral {...}"`) because the bulk revert touched my intended JAML-key form (`"spectralCard {...}"`). The original pre-Step-2b switch also used PascalCase, so the user-facing behavior matches what HEAD had before. If you want the explainer to use the JAML-key form (probably better UX), re-edit each clause's `Describe()` override.

---

## Memory pointers (cross-session lessons)

`~/.claude/projects/X--JammySeedFinder-src-MotelyJAML/memory/MEMORY.md` indexes:

- `feedback_vocabulary.md` — don't say "corpus" or other NLP/RAG jargon; concrete artifact names ("the 17 JAML files," "the test fixtures") only.
- `feedback_no_linq_in_aot.md` — no new LINQ in code reachable from `Motely.Wasm` NativeAOT-LLVM publish; use `Array.ConvertAll` + manual loops. Pre-existing LINQ in `JamlSearchBuilder.cs` is grandfathered.
- `feedback_ide_find_replace.md` — before user or you suggests bulk Ctrl+H in this repo, flag **Match Case** (default off is the trap). The PRNG-keys incident this session was caused by this.

---

## User context

- pifreak is the repo owner, also runs `seedfinder.app` (the Next.js frontend at `D:\seedfinder.app` was restored from `HEAD~1` mid-session after a wholesale deletion commit; that's a separate repo, file restoration was its own digression).
- They have long-COVID brain fog. Clarity moments are real. Tonight's clarity caught the PRNG bug nobody else had spotted in months — treat their hunches seriously even when phrased loosely.
- They prefer raw honest takes over diplomatic hedging. "Be careful" in their voice ≠ "back off"; it means "double-check before destructive action." They explicitly authorized me to commit + push during this session. Don't infer that for the next one — re-ask if you're unsure.
- They explicitly **don't** want a `BESTIE girly girl coder` voice (or any costume). The collaboration mode that worked tonight was: blunt + specific + small commits + check work before declaring done.
