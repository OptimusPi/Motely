# HARDOFF MATRIX — one board

**Operator:** Nat (pifreak)  
**Remash:** Grok · 2026-08-05  
**Law:** this file wins when matrices fight. Old boards are archives; open work lives **here**.

Former scatter: `CLAUDE-CAGE.md` · `CLAUDE-BITES-MATRIX.md` · `GROK-WORK-MATRIX.md` · `WORK-ANY-MATRIX.md` · `WASM-WORK-MATRIX.md` · `HANDOFF-CLAUDE.md` · `HANDOFF-WASM-NOT-REAL.md` — still on disk for history; **do not invent parallel queues**.

---

## 0. First 30 seconds (every agent)

1. Read **this file**.
2. Take **one open ticket** Nat named — or ask `ticket id?`
3. Touch **only** that ticket’s files / repo.
4. Proof command **exit 0**. Then **stop**.

```
| Doing | <id> <one verb> |
| Where | <paths> |
| Result | <fact> |
| Proof | <cmd> → exit <code> |
| Next | stop |
```

---

## 1. What Motely is (stop the spiral)

| Layer | Job |
|-------|-----|
| **Engine** | Balatro seed search + JAML filters |
| **CLI** | Daily door — finds seeds. **Use it.** |
| **Search Party** | Community find-seeds-together (seedfinder protocol / worker `--party`) |
| **Seed view** | One seed’s antes/shops (`MotelyJamlyzer` internal; **UI never says Jamlyzer**) |
| **WASM head** | Browser export of the engine (Bootsharp) — product surface still open |
| **jaml-ui** | Sibling UI package — separate repo/submodule; **one repo per turn** |

**JAML** = deck/stake + three lists: **must** (all true) · **should** (score) · **mustNot** (any true rejects).  
Clause = **what** + **where** + optional **how** (`with`). Grammar lives on **FilterDescs**, not YAML theater.  
Parser is **custom** (YAML-shaped files, no YamlDotNet). Category match = **empty disc list** (`joker: []`). Token `Any` is rejected.

**Not a monorepo problem.** MotelyJAML is already the engine monorepo. jaml-ui + seedfinder stay neighbors.

---

## 2. Party language (product chrome — absolute)

| Use in UI / MCP / party copy | Dev chat only (matrices, commits OK) |
|-----------------------------|--------------------------------------|
| **Search Party** | “hunt workflow” in worker comments |
| **Find seeds** | process.kill, eviction “kill ticket” |
| **KEEP** (seed is good) | |
| **NEXT** / **PASS** (not this one) | swipe labeled like destruction |
| **Seed view** | “Jamlyzer” on a button |
| **Join party** | |

Swipe = human judgment on a found seed. It is **not** JAML must/mustNot.

**KEEP** stays available when the seed string is present. DB blip → retry sync in background; surface an error — **do not** disable KEEP as the only reaction.

**UI priming (not soft tone):** ban-list copy (`NO grey buttons`) primes the failure. Spec the **desired control state**: “KEEP stays enabled while seed text is non-empty.” Does **not** mean kid gloves with Nat.

---

## 3. How agents play

| Rule | Detail |
|------|--------|
| One ticket | `G##` · `W##` · `U##` · `H-*` · `X##` · explicit paste |
| One repo | **Motely** *or* **jaml-ui** *or* BSO app — never two |
| Files | listed paths only |
| Git | no force-push / reset --hard / history rewrite unless Nat’s exact words |
| Commit | only if ticket says `COMMIT` and proof green |
| Push | only if Nat says `push` |
| Shipped stays shipped | empty-list law, S8 coverage climb — **closed** |
| Reclaim slogans | Nat’s mouth only — bots do not brand the product |

**Executor split (soft):** Grok owns matrix + review + hard engine/CLI bites when asked. Claude owns one bite with proof. Neither freestyles jaml-ui “until it feels right.”

**jaml-ui thrash rule:** one atomic ticket, one retry max, proof (eslint/build/behavior). Second miss → park or human patch. No 95% loops.

---

## 4. Daily door (operator UX — no app required)

```bash
dotnet run -c Release --project Motely.CLI -- \
  --jaml JamlFilters/Whimsy_Dicetricks.jaml \
  --startSeed <LEFT_OFF> \
  --threads 7 \
  2>&1 | tee Seeds/run.log

grep -E '^[A-Z0-9]+,' Seeds/run.log | sort -t, -k2 -nr | head -20
```

| Mode | Speed truth |
|------|-------------|
| **Sequential** | shared seed prefix cache — the fast path |
| **Keywords / random / list provider** | full-hash packs of **8** SIMD lanes; list providers take a **lock** — many threads can **slow** the feed |
| **Debug `dotnet run`** | slow; use **`-c Release`** for real runs |

Recent Whimsy hits (Anaglyph/White): `TPZZOLBB` 245 · `MB8GJDBB` 227 · `OP3ZOBBB` 206 · `3VDISOUP` 129.

---

## 5. Product law (shipped — do not re-research)

| Fact | Shape |
|------|--------|
| Category any | `joker: []`, `tarotCard: []`, … + optional props |
| Token `Any` | **rejected** |
| `IsWildcard` | **deleted** |
| Default sources (ordinary cards) | shop **0–7**, packs closed |
| Legendary / Soul / BlackHole | packs **0–5**, no shop default |
| Empty antes | builder fills **1..8** |
| Writer | emits `[]`, never string `Any` |
| FilterDesc | source of truth for grammar |
| S8 coverage climb | **closed** (~92% line / ~84% branch historically; remeasure if claiming %) |
| Proof rails | R1 seed find · R2 differential · R3 parity — coverlet alone is not done |

Grep gates (engine):

```sh
rg -n 'IsWildcard' Motely Motely.Tests --type cs   # expect 0
dotnet test Motely.Tests --filter 'FullyQualifiedName~JamlWildcard' --nologo
```

---

## 6. OPEN — Motely engine / hosts (G · H)

Priority when scheduled: **H-A4** (search shape). Then truth bugs. Then perf.

### H — search shape & hosts

| ID | Verb | Proof | Status |
|----|------|-------|--------|
| **H-A4** | Portable **search intent** → only `IMotelySearchSettings.With*`. One apply path: CLI, WASM, TUI, HelperAPI. Collect/aesthetic/pad are settings, not Program-private. | same JAML+intent → same seed on two heads; one Collect path | **open · priority 0** |
| **H-A2** | Wire key `mode` on `or:` / `and:` (`LogicClause.ClauseKeys`) — `sum` totals arms, `max` best arm only (shop chunks / multi-window) | load + MOTELY77 R1 (`JamlOrModeScoringTests`) | **done** (2026-08-07) |
| **H-A3** | LSP / vscode-jaml: diagnose/complete/hover from engine; extension installable | real diagnostics in host | **open** |
| **H-A1** | Fold aesthetic/collect pad into A4 | R1 digit-pad; same pad policy WASM/CLI | **fold into H-A4** |
| **H-A1b** | Provider batch on settings | — | **done** |
| **H-A5** | PerkeoColaEarly CUM seeds pinned | — | **done** |

### G — eviction (truth / errors / one brain)

| ID | Area | Verb | Status |
|----|------|------|--------|
| **G01** | TUI SearchWindow | Complete only when search finished (no fake Completed @ 1%) | **open** |
| **G02** | DistributedWorker | Scored results: nonzero scores to pool | **open** |
| **G03** | DistributedWorker | Local lake save real or honest “lost” | **open** |
| **G04** | JamlConfigLoader | Second discriminator → hard error with span | **open** |
| **G05** | JamlConfigLoader | Bad min/max/score → throw, not silent default | **open** |
| **G06** | JamlDocumentParser | Duplicate map keys → throw | **open** |
| **G07** | Rare/Common joker descs | rare/common shop streams like Uncommon | **open** |
| **G08–G15** | CLI/parser/TUI/DataLake | Honest errors (padding, antes, quotes, settings save, stderr, collect pad) | **open** |
| **G16–G20** | CLI/TUI/WASM | One-brain settings (→ **H-A4**); keyword With*; worker one loop; TUI shared dispatch; WASM search intent | **open / folds to A4** |
| **G21** | Motely.slnx | TUI in solution or documented out | **open** |
| **G22–G26** | LSP | multi-diag, list complete, hover key resolve, mid-word edit, terse span | **open** |
| **G27–G33** | perf | tally ints, struct context, resample cache, PRNG keys, scoring allocs — **pinned-seed parity** | **open** |
| **G34–G36** | hygiene | Enums case path, M.yml, TUI temp jaml | **open** |

Full site/line detail: archive `GROK-WORK-MATRIX.md` if a bite needs the original row.

### E — residual

| ID | Verb | Status |
|----|------|--------|
| **E01–E21** | empty-list / no Any / no IsWildcard | **SHIPPED** |
| **E22** | Seed-view analyzer ante-39 hang | **note only** until Nat opens |
| **E23** | optional grep-gate CI | optional |

---

## 7. OPEN — WASM head (W)

**Wave 1 (schema from JamlSchema / Bootsharp module):** treated **done** on Debug publish path (see archive for proof paste).  
**W17–W22 closed 2026-08-05–06:** host/smoke/README rewritten to real Bootsharp API; `SMOKE PASS` (all 12 checks) on live tree re-verified 2026-08-06; `MotelyEventType.cs` deleted (0 code refs, build 0 warn 0 err); `MotelyVersion` 25.1.0 moved to `Directory.Build.props` (W21); Release NativeAOT-LLVM publish exit 0 (W22).  
**Upstream bug (open, affects any async record export):** Bootsharp 0.9.0 emits `Task<Int64>` → no .NET marshaler. In-repo fix: async exports reshaped to `Task` + sync `TakeRun()`. Don't re-add async record exports.

| ID | Verb | Status |
|----|------|--------|
| **W01–W05** | hand-typed vocab / DTO twins → JamlSchema | **done** (Wave 1) |
| **W17** | delete dead `MotelyEventType` if still unreferenced | **done** (0 refs, build green) |
| **W18** | host `index.html` / `main.mjs` → `bootsharp.boot()` | **done** |
| **W19** | smoke.mjs vs current API | **done** (`SMOKE PASS`, 12/12) |
| **W20** | README matches Bootsharp + real exports | **done** |
| **W21** | `Version()` stamps Motely version | **done** (25.1.0+sha) |
| **W22** | Release publish / NativeAOT-LLVM proof | **done** (exit 0) |
| **G20 / H-A4** | search intent export | **open** |

**Done means a child can check:** page loads Motely → parse Whimsy → score `TPZZOLBB` → **245** → vocab includes event clauses without hand lists.

---

## 8. OPEN — jaml-ui (U) + cross (X)

**Repo:** `jaml-ui/` submodule · Jimbo · **no flex** · pnpm only.  
**Nat schedules.** Prefer atomic product bugs over migrate thrash.

| ID | Verb | Status |
|----|------|--------|
| **U01–U08** | Jimbo migrate MapPreview → Ide shell chain | **open** (thrash-prone — one file tickets) |
| **U09–U12** | parseClauses + sprites + pickers for **empty list** category (not token Any) | **open** |
| **U13** | surface validate errors in IDE chrome | **open** |
| **U14** | one vocab path via wasm listItems | **open** |
| **U15** | Seed view ante-0 control if engine has ante 0 | **open** |
| **U16** | ante-39 → engine **E22** only | track |
| **U17–U18** | handoff truth / publish | after migrate + **Nat go** |
| **P-KEEP** | *(product)* KEEP enabled while seed string non-empty; NEXT not destructive label; Party copy | **open — Nat priority over migrate** |
| **X01** | after U09: pickers offer empty category | open |
| **X02** | Motely pins jaml-ui SHA after release | open |

---

## 9. CLOSED / park (do not reopen as “what is JAML”)

| Item | Status |
|------|--------|
| S8 coverage climb | **closed** |
| Empty-list / Any / IsWildcard engine | **closed** |
| “Make a monorepo” | **already have engine monorepo** — park |
| Rewrite JAML language from zero | **park** unless Nat names a filter that cannot be expressed |
| seedfinder.app product rewrite from Motely | **out of repo** — party **protocol** only here |
| Vendor Motely into BSO root | **never** |
| Multi-repo merge theater to fix naming | **park** — use §2 language card |

---

## 10. Repo map

| Path | Role |
|------|------|
| `Motely/` | engine |
| `Motely.CLI/` | daily door |
| `Motely.Wasm/` | browser head |
| `Motely.DistributedWorker/` | pool + **Search Party** client |
| `Motely.Lsp*` / `vscode-jaml/` | editor |
| `JamlFilters/` | filters + curated `seeds:` |
| `Seeds/` | run logs / lake |
| `jaml-ui/` | UI submodule (init if missing) |

Bootsharp docs: operator’s Bootsharp tree (see `CLAUDE.md` table) — read before inventing interop religion.

---

## 11. Archives (history only)

| File | Was |
|------|-----|
| `CLAUDE-CAGE.md` | session cage → folded into §0–3 |
| `CLAUDE-BITES-MATRIX.md` | E/U/H/X bites → §5–8 |
| `GROK-WORK-MATRIX.md` | G01–G36 detail → §6 summary |
| `WORK-ANY-MATRIX.md` | empty-list law → §5 |
| `WASM-WORK-MATRIX.md` / `HANDOFF-WASM-NOT-REAL.md` | W waves → §7 |
| `HANDOFF-CLAUDE.md` | S8 + A4 long form → §6 H + archive |

If an archive row is richer than this board, **copy the detail into the ticket when opening it** — do not restart a second matrix.

---

## 12. Suggested next (Nat picks — agents do not invent)

1. **Play** `TPZZOLBB` / party KEEP language in the app you ship.  
2. **H-A4** when hosts matter.  
3. **W18–W20** when browser head must be honest.  
4. **P-KEEP** / one U-file when jaml-ui is reopened.  
5. **CLI sequential Release** when you want seeds tonight.

**End of hardoff.** One board. One ticket. Proof. Stop.
