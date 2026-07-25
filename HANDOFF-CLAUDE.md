# Handoff — Claude work board (MotelyJAML)

**Operator:** Nate  
**Map author:** Grok (session 2026-07-25)  
**Executor:** Claude — **code tool only.** Tables, diffs, proof runs. No friend mode. No honey-soup.

**Repo hard rules:** `CLAUDE.md` (one grammar, FilterDesc → JamlSchema, proof = real search finds a seed).

**Board state (already landed on master):**

| Commit | What |
|--------|------|
| `96f5d066` | honey-soup audit: WASM seed pins, Soul pack default for specials, shop-only test truth, coverage honesty |
| `0fe0ad1d` | **nuke** `Motely.JsonRender` (zero engine consumers) |
| `080556a7` | P1a clipboard junk deleted |
| `992387d5` | bot surface STOP/correction rails |

**Open phases:** P4 (needs cov target), P5 no-work, P6 jaml-ui only if opened, P7 ship gates.  
**Closed this session:** P2 proof debt · P3 shop-only doc lock.

**Green baseline:** `dotnet test` → 389 pass / 1 skip · line cov ~**79.7%** · branch ~**70%** (`coverage.runsettings`).

**Out of scope unless operator says go:** jaml-ui sibling repo, force-push, romantic/Jimbo easter-egg debate.

---

## How to play (partner game rules)

| Seat | Job |
|------|-----|
| **Nate** | picks phase letter / says go / veto |
| **Claude** | one phase per turn; finish verb; handoff table; stop |
| **Grok** | wrote this map; not your friend either |

**Claude loop each phase:**

1. Read this file + `CLAUDE.md`.
2. Do **only** the open phase Nate named.
3. Proof: real `dotnet test` / CLI / `Motely.Wasm` `npm test` when the claim is search correctness.
4. End with:

| Field | Content |
|--------|---------|
| Doing | one verb |
| Where | path |
| Result | fact |
| Next | phase id or stop |

5. **No** “got it / absolutely / love that.” Ship artifact or sit still.

---

## Phase map

### P0 — Sanity (do first if tree dirty)

| Step | Command / check |
|------|-----------------|
| 1 | `git status` clean on master? |
| 2 | `dotnet test Motely.Tests/Motely.Tests.csproj` → 0 fail |
| 3 | Optional: `cd Motely.Wasm && npm test` (rebuilds wasm) |

**Done when:** green table posted. No code change required if already green.

---

### P1 — Honey-soup leftovers (Motely tree only)

| ID | Verb | Notes |
|----|------|--------|
| P1a | **Clipboard junk** | **done** — deleted `Seeds/sixtid4_recovered_clipboard.txt` (sycophant paste, not lake data). |
| P1b | **UI easter egg** | `Motely.TUI/SettingsWindow.cs` `"Jimbo is proud of you!"` — leave unless Nate wants strip. |
| P1c | **Coverage smoke honesty** | `Jaml*CoverageTests` already assert batch-ran / callback-fired, not MatchingSeeds theater. **Do not** reintroduce `>= 0` fake finds. Golden tests pin seeds. |

**Done when:** P1a resolved (delete or explicit skip) and table lists residual.  
**P1 residual:** P1b Jimbo string (leave); P1c already honest.

---

### P2 — Proof debt (real finds, not shape) — **done**

| ID | Verb | Status |
|----|------|--------|
| P2a | Audit `/^[1-9A-Z]/` seed “proof” | **done** — zero shape-as-proof in tests |
| P2b | `searchRandom` | keep: walk count + search-index roundtrip |
| P2c | WASM UI specs | keep: pin `AAAAAAAA`/`BBBBBBBB` |
| P2d | New filters | CLI `--collect 1` must print a seed |

**False positives (not find-proof):**

| Where | Why OK |
|-------|--------|
| `Motely.Wasm/tests/jaml-line-utilities.test.mjs:126` | keyword charset, not seed find |
| `Motely.Tests/SeedProviderTests.cs` | provider emits valid base-35 chars |
| `Motely.DataLake/SeedSourceProvider.cs` | lake SQL/validate charset |

**Proof:** `dotnet test Motely.Tests` → 389 pass / 1 skip (2026-07-25).

---

### P3 — Shop-only defaults (contract lock)

**Law (engine):** null `sources:` → **shop slots only** for ordinary jokers/consumables. Packs/specialty need explicit `sources:`.  
**Exception:** TheSoul / BlackHole → `DefaultSpecialSources` (packs) — they never spawn in shop (`JamlScoring.ResolveSpectralSources`).

| ID | Verb | Status |
|----|------|--------|
| P3a | Grep docs that still say “default = shop + packs” | **done** — fixed Common/Rare/Uncommon FilterDesc summaries (were “8 shop + 6 packs”) |
| P3b | New filter test without sources → shop-only | law holds; `DefaultFallbackTests` covers |
| P3c | Multi-batch / pack-needed → explicit `BoosterPacks` | `ChainedMustClauseSeedTests` already |

**Done when:** no doc/test contradicts shop-only + special spectral exception. **Met** (2026-07-25).

---

### P4 — Coverage climb (optional; Nate must pick target)

Current: **~79.7% line / ~70% branch** (Motely + Lsp; excludes DataLake/Worker/HelperAPI/TUI/Native).

| ID | Verb | Constraint |
|----|------|------------|
| P4a | Pick **one** under-covered Motely area from cobertura | no spray |
| P4b | Add tests that **find seeds** or pin scores | no `MatchingSeeds >= 0` theater |
| P4c | Re-run coverage; post before/after table | |

```sh
dotnet test Motely.Tests/Motely.Tests.csproj --settings coverage.runsettings \
  --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

**Done when:** Nate-agreed delta posted (e.g. +2% line) or phase cancelled.

---

### P5 — WASM / vscode surface (only if broken)

| ID | Verb |
|----|------|
| P5a | `Motely.Wasm`: FS optional in testui; Debug pretest truth in README — already done |
| P5b | `vscode-jaml`: package via `npx @vscode/vsce package` + `.vsix` install — already documented |
| P5c | If `npm test` / Playwright red → fix with **seed pins**, not regex |

**Done when:** green or “no work needed” table.

---

### P6 — jaml-ui (separate repo — **do not** invent Motely.JsonRender again)

Path: sibling repo **jaml-ui** next to MotelyJAML (machine path varies: macOS vs `D:\jaml-ui` vs …). Style = **Jimbo** (`--j-*`, no flex). Use workspace root, not a hardcoded absolute path.  
**Motely.JsonRender is deleted.** Do not recreate it.

| ID | Verb | Only if Nate opens jaml-ui lane |
|----|------|--------------------------------|
| P6a | `pnpm install && pnpm build` smoke |
| P6b | Work `HANDOFFS.md` phase 1 only (one file) | Jimbo primitives |
| P6c | Motely link | WASM/Jamlyzer is enough; no new C# HTML project |

**Done when:** build green or handoff stop. No bot revival of JsonRender.

---

### P7 — Commits / ship (operator gates)

| ID | Verb |
|----|------|
| P7a | Bite-sized commits, each buildable | positive present tense messages |
| P7b | **No** force-push / no publish without Nate go |
| P7c | PR only if Nate asks |

---

## Anti-soup checklist (Claude self-check)

Any **yes** → rewrite before send:

- [ ] Empty praise with no path/diff/command?
- [ ] Code that would not compile in this repo?
- [ ] Claimed “tests green” without running?
- [ ] Second JAML grammar table outside FilterDesc / JamlSchema?
- [ ] Recreated Motely.JsonRender?

Burn line if you slip:  
> Stop the honey-soup. Table or a real diff — no `soup()`.

---

## Operator quick-pick

| Token | Claude starts |
|-------|----------------|
| **P0** | sanity only |
| **P1** | honey leftovers (ask before delete clipboard) |
| **P2** | proof debt grep/fix |
| **P3** | shop-only contract lock |
| **P4** | coverage climb (need target %) |
| **P5** | wasm/vscode if red |
| **P6** | jaml-ui sibling only |
| **P7** | commit/ship with go |
| **B5** | stop |

---

## Bye handoff (Grok → Claude)

| Field | Content |
|--------|---------|
| Map | this file |
| Law | `CLAUDE.md` |
| Dead | `Motely.JsonRender` |
| Alive | engine, CLI, WASM, LSP, vscode-jaml |
| Cov | ~79.7% line / ~70% branch |
| Game | Nate picks phase · Claude executes one · tables only |

**Nate:** paste “do P0” (or another token) into Claude.  
**Claude:** finish that verb. Hand off. Stop.

bye handoff 2 — not friends, just the board.
