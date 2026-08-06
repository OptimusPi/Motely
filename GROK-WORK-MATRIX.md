# WORK — Eviction matrix — ARCHIVE (detail rows)

> **Open queue:** [HARDOFF-MATRIX.md](HARDOFF-MATRIX.md) §6  
> Keep this file for G01–G36 site/line detail when opening a bite. Do not add new parallel IDs here.

**Operator:** Nat
**Auditor:** Claude — 44 agents, adversarially verified, live repros on built assemblies
**Executor:** Grok
**Law:** no softening. Errors throw loud with spans; no empty `catch`; no silent defaults; no "Completed" before completion. One task = one commit = one proof. `dotnet test Motely.Tests` green after every commit.
**Do NOT touch:** `seeds:` blocks in JamlFilters/*.jaml (curated data, the final destination — audit finding refuted, they stay); no reformat sweeps; no new projects; no `Any` token (empty-list law, see HARDOFF §5).

---

## Wave 1 — SHIP TRUTH (high: wrong results / silent data loss)

| ID | Site | Defect | Fix | Proof |
|----|------|--------|-----|-------|
| G01 | Motely.TUI/SearchWindow.cs:400 | `OnSearchComplete()` called right after non-blocking `Start()`; disposes running search, paints green "Completed" at ~1% | Delete the unconditional call; let the existing AddTimeout poll (line 391 already checks `IsCompleted`) drive completion, or await `WaitForCompletionAsync` then `Application.Invoke`. Surface Dispose failures — no `catch { }` | Long sequential search in TUI reaches full `TotalSeedsSearched`; test pins Start-is-nonblocking assumption | **done (Grok)** — unconditional complete deleted; dispose errors surfaced; `Start_IsNonBlocking_IsCompletedFalseUntilWorkersFinish` |
| G02 | Motely.DistributedWorker/Program.cs:151 | Seed-match callback parsed as CSV; engine sends bare seed → pool receives Score=0 for every scored filter | Delete CSV parse; mirror PoolWorkerHostedService.cs:94-99 — branch on `plan.ScoreTallyColumnCount`, hook `WithScoredResultCallback` | Test: scored JAML block submits nonzero scores |
| G03 | Motely.DistributedWorker/Program.cs:205 | "SAVE TO LOCAL DUCKLAKE" section is empty; failure message claims results saved locally — seeds dropped | Wire SeedLakeSink (as CLI does, Motely.CLI/Program.cs:594-599) before submit, or delete `--local-db` and make the message say results were lost | Kill pool mid-run: seeds present in local lake |
| G04 | Motely/Filters/Jaml/JamlConfigLoader.cs:335 | Second discriminator key in one clause validates then silently vanishes (`joker:` + `voucher:` → voucher dropped) | ValidateClauseKeys allows only the chosen discriminator's aliases; or FindDiscriminator throws positioned JamlSemanticException on a second one | Test: two-discriminator clause throws with span |
| G05 | Motely/Filters/Jaml/JamlConfigLoader.cs:190 | `score: 10O`, `min: banana` silently become defaults (GetInt null ≡ absent) | NodeReader: key present + unparsable → throw JamlSemanticException with ValueSpan, mirroring NodeValueReader.TryInt | Test: malformed min/max/score each throw |
| G06 | Motely/Filters/Jaml/JamlDocumentParser.cs:36 | Duplicate mapping keys last-write-win; second `must:` silently discards the first | JMap.Set / ParseMapping throws JamlSyntaxException "Duplicate key 'x' (first defined at line N)" with keySpan | Test: duplicate `must:` and `Min:`/`min:` throw |
| G07 | Motely/Filters/Jaml/AnteCards/RareJokerFilterDesc.cs:82 | Rare/Common rarity descs never read `rareShopJokers`/`commonShopJokers` sources → must-clauses match 0 seeds (Uncommon twin works) | Port Uncommon's raw-stream source handling (UncommonJokerFilterDesc.cs:62-68) to Rare+Common; then collapse the three copies into one rarity-parameterized desc | Rare/common mirror of S8CoverageClimbTests.cs:394-412 |

## Wave 2 — HONEST ERRORS (care-faker mediums)

| ID | Site | Defect | Fix | Proof |
|----|------|--------|-----|-------|
| G08 | Motely.CLI/CliSearchMode.cs:211 | Hand-rolled padding parse: invalid chars silently dropped; fully-invalid → crash (keyword) or full 35-char alphabet (aesthetic); help example `--keywords OW,OH,BOOB` crashes raw | One validation in TryApplySearchMode via MotelyGlobals.ParsePaddingChars: null/empty → `error` out-param; dropped chars → writeWarning. Wrap engine ArgumentExceptions into `error` | Tests: invalid padding errors cleanly; help example exits with message not stack trace |
| G09 | Motely/Filters/Jaml/JamlConfigLoader.cs:183 | `antes: [99, -3]` accepted; 99 hangs search, -3 NREs in Release | Validate ante range at load, throw with ValueSpan("antes") | Test: out-of-range ante throws |
| G10 | Motely/Filters/Jaml/JamlDocumentParser.cs:436 | Quoted items in arrays keep literal quotes — `seeds: ["AAAA"]` can never match | Apply scalar path's quote-stripping (lines 441-442) to flow-array and block-sequence items | Test: quoted seed/enum in array round-trips |
| G11 | Motely/Filters/Jaml/JamlDocumentParser.cs:353 | Text after `]` on closing line silently discarded | Non-whitespace after `close` → JamlSyntaxException at that line | Test: `3] 4, 5` throws |
| G12 | Motely.TUI/TuiSettings.cs:157 | SaveSettings empty-catch — settings silently never persist; SettingsWindow's error dialog is dead code | Propagate/return failure; show the existing error dialog; log to CrashLogPath | Test: read-only dir → visible error |
| G13 | Motely/MotelySearch.cs:1047 | Worker crash stack trace printed to stdout, corrupting piped CSV | Delete the Console.WriteLine (exception already flows via `_completionSource`) or route to stderr/logger | Grep: no stdout writes in worker path |
| G14 | Motely.DataLake/SeedSourceProvider.cs:159 | DuckDB ATTACH failure swallowed; user sees misleading sqlite error | Keep original as InnerException; aggregate both messages | Test: locked file error names both attempts |
| G15 | Motely.CLI/Program.cs:734 | `--collect` with fully-invalid `--padding` silently uses digit default | ParsePaddingChars null + flag present → error + exit 1 | Test pins the error |

## Wave 3 — ONE BRAIN (boundary law, A4)

| ID | Site | Defect | Fix | Proof |
|----|------|--------|-----|-------|
| G16 | Motely.CLI/Program.cs:733 | Whole `--collect` algorithm (multi-aesthetic prepass + digit default + sequential fallback) is CLI-private | Move collect intent onto IMotelySearchSettings/engine helper; CLI reduces to argv→intent | Grep gate: no second Collect algorithm outside settings apply |
| G17 | Motely.CLI/CliSearchMode.cs:218 | Keyword search hand-composes what MotelyKeywordSeedProvider already packages; no With* exists | Add `WithKeywordSearch(keywords, padding?)` on settings (normalize inside engine); route CLI + TUI through it | Both host compositions deleted; tests green |
| G18 | Motely.DistributedWorker/ProcessBlock.cs:77 | Three copies of claim→search→submit; ProcessBlockRunner is dead and diverged | Delete ProcessBlockRunner; console host runs PoolWorkerHostedService (as HelperApiHost.cs:33 does) | One loop remains; grep proves it |
| G19 | Motely.TUI/SearchWindow.cs:255 | TUI re-implements CLI search wiring with divergent validation (raw `ToCharArray()` padding) | Share one dispatch: move TryApplySearchMode into engine/DataLake; TUI consumes it | Third padding parse deleted |
| G20 | Motely.Wasm/MotelyWasmApi.cs:126 | WASM has no search-intent export; A4 CLI/WASM parity structurally unmeetable | Export one search-request DTO applying only With* over CreateSettings (per Bootsharp rebuild plan) | R1 parity: same JAML+intent → same seed, CLI vs WASM |
| G21 | Motely.slnx:12 | Motely.TUI absent from solution — CI never compiles it | Add to slnx (or comment the exclusion like Motely.Wasm's) | Root `dotnet build` compiles TUI |

## Wave 4 — LSP

| ID | Site | Defect | Fix | Proof |
|----|------|--------|-----|-------|
| G22 | Motely.Lsp.Core/JamlLanguageService.cs:29 | Diagnose returns ≤1 diagnostic, not even first-in-document | Accumulate JamlSemanticExceptions (validate root keys + each clause independently); return full list | Test: 3-error doc → 3 diagnostics, document order |
| G23 | Motely.Lsp.Core/JamlLanguageService.cs:446 | Completion dead inside `[A, B` lists; span would clobber prior items | Tokenize after last of `,` `[` whitespace; span from token start | Test: `joker: [Blueprint, Bra` → Brainstorm, correct span |
| G24 | Motely.Lsp.Core/JamlLanguageService.cs:109 | Hover: first-enum-wins mislabels `stake: Gold` as Enhancement | Resolve enum from the line's key first; global scan as fallback | Test: `stake: Gold` hovers Stake |
| G25 | Motely.Lsp.Core/JamlLanguageService.cs:458 | textEdit stops at cursor; mid-word accept yields "Brainstormueprint" | Extend range to word end, or emit InsertReplaceEdit | Test pins mid-word accept |
| G26 | Motely/Filters/Jaml/JamlDocumentParser.cs:260 | Terse-line span stamped on last continuation line | Capture line index/column before `i++`; stamp scalar Span too | Test: span points at terse line |

## Wave 5 — PERF (engine hot paths — bit-exactness is law; every change needs pinned-seed parity)

| ID | Site | Defect | Fix | Proof |
|----|------|--------|-----|-------|
| G27 | Motely/MotelySeedScoreTally.cs:22 | Tally byte[] getter wraps ints mod 256; a test enshrines it | Interface exposes int (use existing `TallyValuesSpan`); update Wasm/HelperAPI consumers; **delete the wraparound test** | Test: tally 300 arrives as 300 |
| G28 | Motely/MotelySingleSearchContext.cs:49 | Heap class allocated per seed in scalar-confirm loops | Make it a struct (callees already take `ref`) or hoist per plan | Pinned-seed parity + allocation drop |
| G29 | Motely/MotelySingleSearchContext.cs:360 | Resample stream cache dead: wrong index + unconditional reset | Index `resample - StackResampleCount`; init-once; same fix in vector twin | Parity + cache actually hits |
| G30 | Motely/MotelyVectorSearchContext.cs:477 | PRNG keys rebuilt by concat + int boxing per stream | Precompute key tables per (family, ante[, resample]) at CreateFilter | Parity + zero concat in hot path |
| G31 | Motely/MotelyVectorSearchContext.Shop.cs:38 | Default overload allocates MotelyRunState per pack/seed | Static per-deck default state (read-only use) | Parity |
| G32 | Motely/Filters/Jaml/JamlScoring.cs:1622 | LINQ Where().ToArray() legendary split per seed | Partition once at provider creation; cache on clause | Parity |
| G33 | Motely/Filters/Jaml/JamlShouldScoreDesc.cs:123 | Closure per pack + RunState + clause array per seed | Build combined array in ctor; pool per-thread RunState with Reset() | Parity |

## Wave 6 — HYGIENE

| ID | Site | Defect | Fix |
|----|------|--------|-----|
| G34 | Motely/enums/ + Motely/Enums/ | Both tracked — case-insensitive checkout collision | `git mv` the 18 files into Enums/ (matches namespace), one commit |
| G35 | JamlFilters/M.yml:19 | .yml escapes the corpus test glob; Showman rarity wrong | Rename to .jaml, fix rarity, move CSV/txt out of JamlFilters/ |
| G36 | Motely.TUI/SearchResults/Jerkeo_RedDeck_White_temp.jaml | Committed `_temp` output uses removed `AnyRare` token | `git rm`; gitignore Motely.TUI/SearchResults/ |

---

## Refuted — armistice line

`neglegSweep*.jaml seeds:` blocks: **intentional, documented, consumed** (`--source` reads them, JsonRender reads them, test pins it, headers declare the block the final destination). ~5,000 curated finds. Touching them is destruction, not cleanup.
